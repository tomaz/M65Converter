using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Images;

using System.IO.Compression;
using System.Text;

namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Contains and parses data from Aseprite file
///
/// Aseprite file format specs: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
/// </summary>
/// <remarks>
/// Requires SixLabors.ImageSharp dependency!
/// </remarks>
public class Aseprite
{
	public AsepriteHeaderType AsepriteHeader { get; init; } = null!;
	public List<AsepriteFrameType> AsepriteFrames { get; init; } = new();

	public List<FrameData> GeneratedFrames { get; } = new();

	#region Initialization & Disposal

	/// <summary>
	/// Parses data from the given Aseprite file and returns an instance of <see cref="Aseprite"/>.
	/// </summary>
	public static Aseprite Parse(IStreamProvider provider)
	{
		var reader = new BinaryReader(provider.GetStream(FileMode.Open));

		var header = AsepriteHeaderType.Parse(reader);
		var frames = new List<AsepriteFrameType>();

		for (var i = 0; i < header.FramesCount; i++)
		{
			var frameStartPosition = reader.BaseStream.Position;
			var frame = AsepriteFrameType.Parse(reader);
			frames.Add(frame);

			// Note: we could read chunks inside `FrameType` but I chose to do it here to keep type classes simpler - this is quite a complex part due to many different types.
			for (var n = 0; n < frame.ChunksCount; n++)
			{
				var chunkStartPosition = reader.BaseStream.Position;
				var chunkHeader = AsepriteChunkHeaderType.Parse(reader);

				switch (chunkHeader.Type)
				{
					case 0x2004:
						frame.Chunks.Add(AsepriteLayerChunkType.Parse(reader));
						break;

					case 0x2005:
						frame.Chunks.Add(AsepriteCelChunkType.Parse(reader, header));
						break;

					case 0x2006:
						frame.Chunks.Add(AsepriteCelExtraChunkType.Parse(reader));
						break;

					case 0x2019:
						frame.Chunks.Add(AsepritePaletteChunkType.Parse(reader));
						break;

					case 0x2022:
						// Do we need to handle slice chunk?? For now we just skip it.
						break;

					case 0x2023:
						frame.Chunks.Add(AsepriteTilesetChunkType.Parse(reader, header));
						break;
				}

				// This takes care of 2 use cases: unimplemented chunk types and chunks that "share data" (some of the chunks, for example `CelChunkType` read data way past their size, so without this we'd reach the end of the stream while reading subsequent chunks).
				reader.BaseStream.Position = chunkStartPosition + chunkHeader.Size;
			}

			// Similar to chunks, let's make sure we never continue past frame size.
			reader.BaseStream.Position = frameStartPosition + frame.Size;
		}

		// Prepare the result.
		var result = new Aseprite
		{
			AsepriteHeader = header,
			AsepriteFrames = frames
		};

		// Pre-generate all data.
		result.GenerateFramesData();

		return result;
	}

	#endregion

	#region Rendering

	private void GenerateFramesData()
	{
		Image<Argb32> CreateEmptyImage() => new Image<Argb32>(AsepriteHeader.Width, AsepriteHeader.Height);

		List<Argb32> ExtractPalette(AsepriteFrameType frame)
		{
			var result = new List<Argb32>();

			foreach (var chunk in frame.Chunks)
			{
				switch (chunk)
				{
					case AsepritePaletteChunkType palette:
					{
						foreach (var colour in palette.Colours)
						{
							result.Add(new Argb32(r: colour.Red, b: colour.Blue, g: colour.Green, a: colour.Alpha));
						}
						break;
					}
				}
			}

			return result;
		}

		void SetupTransparentColour(List<Argb32> palette)
		{
			// Aseprite always uses 255 for alpha value for all colours in palette. This doesn't matter for RGBA mode since pixel data contains all 4 components and it correctly uses 0 for alpha for transparent colour. But in indexed colour mode where pixel is just an index into the actual palette, we have to adjust alpha from 255 to 0 for the colour that's marked transparent. Ohterwise we will get opaque pixels.
			if (AsepriteHeader.ColourDepth != AsepriteColourDepthType.Indexed) return;
			if (AsepriteHeader.TransparentColourIndex >= palette.Count) return;

			// If transparent colour already has alpha component 0, we're fine.
			var existingColour = palette[AsepriteHeader.TransparentColourIndex];
			if (existingColour.A == 0) return;

			// Otherwise we need to replace it with transparent colour.
			palette[AsepriteHeader.TransparentColourIndex] = existingColour.WithAlpha(0);
		}

		List<T> ExtractChunks<T>(AsepriteFrameType frame) where T: AsepriteBaseChunkType
		{
			var result = new List<T>();

			foreach (var chunk in frame.Chunks)
			{
				if (chunk is T t)
				{
					result.Add(t);
				}
			}

			return result;
		}

		List<Tuple<AsepriteLayerChunkType, Image<Argb32>>> GenerateImages(
			AsepriteFrameType frame, 
			List<Argb32> palette, 
			List<AsepriteLayerChunkType> layers,
			List<AsepriteTilesetChunkType> tilesets
		)
		{
			Argb32 Colour(byte[] data)
			{
				switch (AsepriteHeader.ColourDepth)
				{
					case AsepriteColourDepthType.RGBA:
						return new Argb32(r: data[0], g: data[1], b: data[2], a: data[3]);

					case AsepriteColourDepthType.Indexed:
						return palette[data[0]];

					default:
						return new Argb32();	// we don't support grayscale rendering atm
				}
			}

			void CopyPixelsImage(IImageProcessingContext mutator, AsepriteCelChunkType source)
			{
				for (var y = 0; y < source.Height; y++)
				{
					for (var x = 0; x < source.Width; x++)
					{
						// For image, we simply copy from source to destination.
						var bytes = source.Data[x, y];
						var colour = Colour(bytes);

						mutator.SetPixel(colour, x + source.X, y + source.Y);
					}
				}
			}

			void CopyPixelsTileset(IImageProcessingContext mutator, AsepriteCelChunkType source)
			{
				var tileset = tilesets[source.LayerIndex];

				// For tileset, source width and height represent tiles not pixels
				for (var ty = 0; ty < source.Height; ty++)
				{
					var iy = ty * tileset.TileHeight + source.Y;

					for (var tx = 0; tx < source.Width; tx++)
					{
						var ix = tx * tileset.TileWidth + source.X;
						var tileIndex = source.Data[tx, ty];

						// For tileset, we need to copy the whole tilemap image.
						int tileTop = (int)(tileset.TileHeight * tileIndex.ToUInt());

						for (var y = 0; y < tileset.TileHeight; y++)
						{
							for (var x = 0; x < tileset.TileWidth; x++)
							{
								var bytes = tileset.TilesetImage[x, tileTop + y];
								var colour = Colour(bytes);

								mutator.SetPixel(colour, ix + x, iy + y);
							}
						}
					}
				}
			}

			Action<IImageProcessingContext, AsepriteCelChunkType> DecideCopyMethod(AsepriteCelChunkType source)
			{
				return source.Type switch
				{
					AsepriteCelChunkType.CelType.CompressedTilemap => CopyPixelsTileset,
					_ => CopyPixelsImage
				};
			}

			var result = new List<Tuple<AsepriteLayerChunkType, Image<Argb32>>>();

			foreach (var chunk in frame.Chunks)
			{
				switch (chunk)
				{
					case AsepriteCelChunkType source:
					{
						var layer = layers[source.LayerIndex];
						var copyMethod = DecideCopyMethod(source);

						// Ignore hidden layers.
						if (!layer.IsVisible) continue;
						if (layer.Opacity == 0) continue;

						// Prepare empty image and add it to resulting array.
						var image = CreateEmptyImage();
						result.Add(new Tuple<AsepriteLayerChunkType, Image<Argb32>>(layer, image));

						// Copy all pixels.
						image.Mutate(mutator =>
						{
							copyMethod(mutator, source);
						});

						break;
					}
				}
			}

			return result;
		}

		Image<Argb32> GenerateCompositeImage(List<Tuple<AsepriteLayerChunkType, Image<Argb32>>> images)
		{
			var result = CreateEmptyImage();

			var pixelOperator = new PixelOperations<Argb32>();

			PixelBlender<Argb32> GetPixelBlender(AsepriteLayerChunkType layer)
			{
				// Note: we only support a subset of blending modes, all unsupported are rendered as normal.
				var blendMode = layer.BlendMode switch
				{
					AsepriteLayerChunkType.BlendModeType.Multiply => PixelColorBlendingMode.Multiply,
					AsepriteLayerChunkType.BlendModeType.Addition => PixelColorBlendingMode.Add,
					AsepriteLayerChunkType.BlendModeType.Subtract => PixelColorBlendingMode.Subtract,
					AsepriteLayerChunkType.BlendModeType.Screen => PixelColorBlendingMode.Screen,
					AsepriteLayerChunkType.BlendModeType.Darken => PixelColorBlendingMode.Darken,
					AsepriteLayerChunkType.BlendModeType.Lighten => PixelColorBlendingMode.Lighten,
					AsepriteLayerChunkType.BlendModeType.Overlay => PixelColorBlendingMode.Overlay,
					AsepriteLayerChunkType.BlendModeType.HardLight => PixelColorBlendingMode.HardLight,
					_ => PixelColorBlendingMode.Normal
				};

				var options = new GraphicsOptions
				{
					Antialias = false,
					ColorBlendingMode = blendMode,
				};

				return pixelOperator!.GetPixelBlender(options);
			}

			result.Mutate(mutator =>
			{
				foreach (var data in images)
				{
					var layer = data.Item1;
					var image = data.Item2;
					var opacity = layer.Opacity / 255.0f;

					var pixelBlender = GetPixelBlender(layer);

					for (var y = 0; y < result.Height; y++)
					{
						for (var x = 0; x < result.Width; x++)
						{
							var backgroundColour = result[x, y];
							var layerColour = image[x, y];

							var mixedColour = pixelBlender.Blend(backgroundColour, layerColour, opacity);

							mutator.SetPixel(mixedColour, x, y);
						}
					}
				}
			});

			return result;
		}

		// Some data is shared and accumulated between frames. For these, we maintain common list.
		var layers = new List<AsepriteLayerChunkType>();
		var palette = new List<Argb32>();

		foreach (var frame in AsepriteFrames)
		{
			// Since we're dealing with external format, we shouldn't rely on specific order of chunks. However we do need to have data subseqent chunks depend on (for example we need palette in order to render colours etc). So we parse chunks in several steps to ensure we have all prerequisite data ready once we need it.

			// Append all accumulative data shared between frames.
			palette.AddRange(ExtractPalette(frame));
			layers.AddRange(ExtractChunks<AsepriteLayerChunkType>(frame));

			// Ensure transparent colour is correctly represented in palette.
			SetupTransparentColour(palette);

			// Extract all other data.
			var tilesets = ExtractChunks<AsepriteTilesetChunkType>(frame);
			var images = GenerateImages(frame, palette, layers, tilesets);
			var composite = GenerateCompositeImage(images);

			GeneratedFrames.Add(new FrameData
			{
				CompositeImage = composite,
				LayerImages = images.Select(x => x.Item2).ToList(),
				LayerNames = images.Select(x => x.Item1.Name).ToList(),
				Palette = palette
			});
		}
	}

	#endregion

	#region Declarations - Generated

	public class FrameData
	{
		public Image<Argb32> CompositeImage { get; init; } = null!;
		public List<Image<Argb32>> LayerImages { get; init; } = null!;
		public List<string> LayerNames { get; init; } = null!;
		public List<Argb32> Palette { get; init; } = null!;
	}

	#endregion

	#region Declarations - Aseprite Format

	public enum AsepriteColourDepthType
	{
		RGBA = 32,
		Grayscale = 16,
		Indexed = 8
	}

	public class AsepriteHeaderType
	{
		public uint FileSize { get; private set; }
		public ushort FramesCount { get; private set; }
		public ushort Width { get; private set; }
		public ushort Height { get; private set; }
		public byte TransparentColourIndex { get; private set; }
		public ushort ColoursCount { get; private set; }
		public byte PixelWidth { get; private set; }
		public byte PixelHeight { get; private set; }
		public short GridX { get; private set; }
		public short GridY { get; private set; }
		public ushort GridWidth { get; private set; }
		public ushort GridHeight { get; private set; }
		public AsepriteColourDepthType ColourDepth { get; private set; }

		public int PixelSizeBytes
		{
			get => ColourDepth switch
			{
				AsepriteColourDepthType.RGBA => 4,
				AsepriteColourDepthType.Grayscale => 2,
				_ => 1,
			};
		}

		#region Initialization & Disposal

		private AsepriteHeaderType()
		{
		}

		public static AsepriteHeaderType Parse(BinaryReader reader)
		{
			var result = new AsepriteHeaderType();

			result.FileSize = reader.ReadDWord();

			var magic = reader.ReadWord();
			if (magic != 0xA5E0) throw new InvalidDataException($"Magic number in header not matching at offset {AsepriteExtensions.PosBeforeLastRead}");

			result.FramesCount = reader.ReadWord();
			result.Width = reader.ReadWord();
			result.Height = reader.ReadWord();
			result.ColourDepth = (AsepriteColourDepthType)reader.ReadWord();
			reader.ReadDWord(); // flags
			reader.ReadWord();  // speed
			reader.ReadDWord(); // 0
			reader.ReadDWord(); // 0
			result.TransparentColourIndex = reader.ReadByte();
			reader.Skip(3);     // ignored
			result.ColoursCount = reader.ReadWord();
			result.PixelWidth = reader.ReadByte();
			result.PixelHeight = reader.ReadByte();
			result.GridX = reader.ReadShort();
			result.GridY = reader.ReadShort();
			result.GridWidth = reader.ReadWord();
			result.GridHeight = reader.ReadWord();
			reader.Skip(84);

			return result;
		}

		#endregion
	}

	public class AsepriteFrameType
	{
		public uint Size { get; private set; }
		public uint ChunksCount { get; private set; }
		public ushort FrameDuration { get; private set; }
		public List<AsepriteBaseChunkType> Chunks { get; } = new();

		#region Initialization & Disposal

		private AsepriteFrameType()
		{
		}

		public static AsepriteFrameType Parse(BinaryReader reader)
		{
			var result = new AsepriteFrameType();

			result.Size = reader.ReadDWord();

			var magic = reader.ReadWord();
			if (magic != 0xF1FA) throw new InvalidDataException($"Magic number in header not matching at offset {AsepriteExtensions.PosBeforeLastRead}");

			var oldChunksCount = reader.ReadWord();
			result.FrameDuration = reader.ReadWord();
			reader.Skip(2);
			var chunksCount = reader.ReadDWord();

			// Typically old and new chunks counts are the same number, but docs say to use old if new is 0. Or use new if old is 0xFFFF, but we don't have to check for that since new value won't be 0 in this case.
			result.ChunksCount = chunksCount == 0 ? oldChunksCount : chunksCount;

			return result;
		}

		#endregion
	}

	public class AsepriteChunkHeaderType
	{
		public uint Size { get; private set; }
		public ushort Type { get; private set; }

		public uint SizeWithoutHeader { get => Size - 6; }

		#region Initialization & Disposal

		private AsepriteChunkHeaderType()
		{
		}

		public static AsepriteChunkHeaderType Parse(BinaryReader reader)
		{
			var result = new AsepriteChunkHeaderType();

			result.Size = reader.ReadDWord();
			result.Type = reader.ReadWord();

			return result;
		}

		#endregion
	}

	public class AsepriteLayerChunkType : AsepriteBaseChunkType
	{
		public ushort Flags { get; private set; }
		public bool IsVisible { get => (Flags & 1) != 0; }
		public bool IsEditable { get => (Flags & 2) != 0; }
		public bool IsLockMovement { get => (Flags * 4) != 0; }
		public bool IsBackground { get => (Flags & 8) != 0; }
		public bool IsPreferLinkedCels { get => (Flags & 16) != 0; }
		public bool IsLayerGroupDisplayedCollapsed { get => (Flags & 32) != 0; }
		public bool IsReferenceLayer { get => (Flags & 64) != 0; }

		public ushort ChildLevel { get; private set; }
		public byte Opacity { get; private set; }
		public uint TilesetIndex { get; private set; }
		public string Name { get; private set; } = null!;

		public LayerType Type { get; private set; }
		public BlendModeType BlendMode { get; private set; }

		#region Initialization & Disposal

		private AsepriteLayerChunkType()
		{
		}

		public static AsepriteLayerChunkType Parse(BinaryReader reader)
		{
			var result = new AsepriteLayerChunkType();

			result.Flags = reader.ReadWord();
			result.Type = (LayerType)reader.ReadWord();
			result.ChildLevel = reader.ReadWord();
			reader.ReadWord();  // default layer width (ignored)
			reader.ReadWord();  // default layer height (ignored)
			result.BlendMode = (BlendModeType)reader.ReadWord();
			result.Opacity = reader.ReadByte();
			reader.Skip(3);     // reserved for future use
			result.Name = reader.ReadUTF8String();

			if (result.Type == LayerType.Tilemap)
			{
				result.TilesetIndex = reader.ReadDWord();
			}

			return result;
		}

		#endregion

		#region Declarations

		public enum LayerType
		{
			Image,
			Group,
			Tilemap
		}

		public enum BlendModeType
		{
			Normal,
			Multiply,
			Screen,
			Overlay,
			Darken,
			Lighten,
			ColorDodge,
			ColorBurn,
			HardLight,
			SoftLight,
			Difference,
			Exclusion,
			Hue,
			Saturation,
			Color,
			Luminosity,
			Addition,
			Subtract,
			Divide,
		}

		#endregion
	}

	public class AsepriteCelChunkType : AsepriteBaseChunkType
	{
		public ushort LinkedFrame { get; private set; }
		public ushort LayerIndex { get; private set; }
		public ushort Width { get; private set; }   // pixels or tiles
		public ushort Height { get; private set; }  // pixels or tiles
		public short X { get; private set; }
		public short Y { get; private set; }
		public short ZIndex { get; private set; }
		public byte OpacityLevel { get; private set; }

		public ushort BitsPerTile { get; private set; }
		public uint BitmaskTile { get; private set; }
		public uint BitmaskTileXFlip { get; private set; }
		public uint BitmaskTileYFlip { get; private set; }
		public uint BitmaskTile90CW { get; private set; }

		public AsepriteImage Data { get; } = new();    // pixels or tiles, depending Type value

		public CelType Type { get; private set; }

		#region Initialization & Disposal

		private AsepriteCelChunkType()
		{
		}

		public static AsepriteCelChunkType Parse(BinaryReader reader, AsepriteHeaderType fileHeader)
		{
			var result = new AsepriteCelChunkType();

			result.LayerIndex = reader.ReadWord();
			result.X = reader.ReadShort();
			result.Y = reader.ReadShort();
			result.OpacityLevel = reader.ReadByte();
			result.Type = (CelType)reader.ReadWord();
			result.ZIndex = reader.ReadShort();
			reader.Skip(5);

			void AssignData(byte[] buffer, int? dataSize = null)
			{
				var pixelSize = dataSize ?? fileHeader.PixelSizeBytes;

				result.Data.Width = result.Width;
				result.Data.Height = result.Height;

				result.Data.CopyFrom(buffer, pixelSize);
			}

			switch (result.Type)
			{
				case CelType.RawImageData:
				{
					result.Width = reader.ReadWord();
					result.Height = reader.ReadWord();

					var buffer = reader.ReadBytes(result.Width * result.Height * fileHeader.PixelSizeBytes);

					AssignData(buffer);
					break;
				}

				case CelType.CompressedImage:
				{
					result.Width = reader.ReadWord();
					result.Height = reader.ReadWord();
					reader.Skip(2); // skip compressed data size word, DeflateStream requires data to start with actual compressed data

					var buffer = new byte[result.Width * result.Height * fileHeader.PixelSizeBytes];
					new DeflateStream(reader.BaseStream, CompressionMode.Decompress).Read(buffer, 0, buffer.Length);

					AssignData(buffer);
					break;
				}

				case CelType.CompressedTilemap:
				{
					result.Width = reader.ReadWord();
					result.Height = reader.ReadWord();
					result.BitsPerTile = reader.ReadWord();
					result.BitmaskTile = reader.ReadDWord();
					result.BitmaskTileXFlip = reader.ReadDWord();
					result.BitmaskTileYFlip = reader.ReadDWord();
					result.BitmaskTile90CW = reader.ReadDWord();
					reader.Skip(10);
					reader.Skip(2); // skip compressed data size word, DeflateStream requires data to start with actual compressed data

					var tileSize = result.BitsPerTile / 8;
					var buffer = new byte[result.Width * result.Height * tileSize];
					new DeflateStream(reader.BaseStream, CompressionMode.Decompress).Read(buffer, 0, buffer.Length);

					AssignData(buffer, tileSize);
					break;
				}

				case CelType.LinkedCel:
				{
					result.LinkedFrame = reader.ReadWord();
					break;
				}
			}

			return result;
		}

		#endregion

		#region Declarations

		public enum CelType
		{
			RawImageData,
			LinkedCel,
			CompressedImage,
			CompressedTilemap
		}

		#endregion
	}

	public class AsepriteCelExtraChunkType : AsepriteBaseChunkType
	{
		public uint Flags { get; private set; }
		public decimal PreciseX { get; private set; }
		public decimal PreciseY { get; private set; }
		public decimal Width { get; private set; }
		public decimal Height { get; private set; }

		#region Initialization & Disposal

		private AsepriteCelExtraChunkType()
		{
		}

		public static AsepriteCelExtraChunkType Parse(BinaryReader reader)
		{
			var result = new AsepriteCelExtraChunkType();

			result.Flags = reader.ReadDWord();
			result.PreciseX = reader.ReadFixed();
			result.PreciseY = reader.ReadFixed();
			result.Width = reader.ReadFixed();
			result.Height = reader.ReadFixed();
			reader.Skip(16);

			return result;
		}

		#endregion
	}

	public class AsepritePaletteChunkType : AsepriteBaseChunkType
	{
		public uint NewSize { get; private set; }
		public uint FirstChangedColourIndex { get; private set; }
		public uint LastChangedColourIndex { get; private set; }
		public List<ColourType> Colours { get; } = new();

		#region Initialization & Disposal

		private AsepritePaletteChunkType()
		{
		}

		public static AsepritePaletteChunkType Parse(BinaryReader reader)
		{
			var result = new AsepritePaletteChunkType();

			result.NewSize = reader.ReadDWord();
			result.FirstChangedColourIndex = reader.ReadDWord();
			result.LastChangedColourIndex = reader.ReadDWord();
			reader.Skip(8);

			var count = result.LastChangedColourIndex - result.FirstChangedColourIndex + 1;
			for (var i = 0; i < count; i++)
			{
				result.Colours.Add(ColourType.Parse(reader));
			}

			return result;
		}

		#endregion

		#region Declarations

		public class ColourType
		{
			public ushort Flags { get; private set; }
			public bool IsNameAssigned { get => (Flags & 1) > 0; }
			public byte Red { get; private set; }
			public byte Green { get; private set; }
			public byte Blue { get; private set; }
			public byte Alpha { get; private set; }
			public string Name { get; private set; } = null!;

			public override string ToString() => $"a: {Alpha} r: {Red} g: {Green} b: {Blue} {Name}";

			#region Initialization & Disposal

			private ColourType()
			{
			}

			public static ColourType Parse(BinaryReader reader)
			{
				var result = new ColourType();

				result.Flags = reader.ReadWord();
				result.Red = reader.ReadByte();
				result.Green = reader.ReadByte();
				result.Blue = reader.ReadByte();
				result.Alpha = reader.ReadByte();
				result.Name = result.IsNameAssigned ? reader.ReadUTF8String() : string.Empty;

				return result;
			}

			#endregion
		}

		#endregion
	}

	public class AsepriteTilesetChunkType : AsepriteBaseChunkType
	{
		public uint ID { get; private set; }
		public uint Flags { get; private set; }
		public bool IsLinkToExternalFile { get => (Flags & 1) > 0; }	// this is not currently supported!
		public bool IsIncludedInsideThisFile { get => (Flags & 2) > 0; }
		public bool IsEmptyTileID0 { get => (Flags & 4) > 0; }
		public uint TilesCount { get; private set; }
		public ushort TileWidth { get; private set; }
		public ushort TileHeight { get; private set; }
		public short BaseIndex { get; private set; }    // just for UI purposes
		public string TilesetName { get; private set; } = null!;

		public AsepriteImage TilesetImage { get; } = new();

		#region Initialization & Disposal

		private AsepriteTilesetChunkType()
		{
		}

		public static AsepriteTilesetChunkType Parse(BinaryReader reader, AsepriteHeaderType fileHeader)
		{
			var result = new AsepriteTilesetChunkType();

			result.ID = reader.ReadDWord();
			result.Flags = reader.ReadDWord();

			// We don't support external files!
			if (result.IsLinkToExternalFile) throw new InvalidDataException("External files tilesets are not supported!");

			result.TilesCount = reader.ReadDWord();
			result.TileWidth = reader.ReadWord();
			result.TileHeight = reader.ReadWord();
			result.BaseIndex = reader.ReadShort();
			reader.Skip(14);
			result.TilesetName = reader.ReadUTF8String();

			// We should always have data included, but we nonetheless handle it conditionally.
			if (result.IsIncludedInsideThisFile)
			{
				reader.Skip(4);		// skip 4 bytes length data.
				reader.Skip(2);		// we must also skip zlib header

				var buffer = new byte[result.TileWidth * result.TileHeight * result.TilesCount];
				new DeflateStream(reader.BaseStream, CompressionMode.Decompress).Read(buffer, 0, buffer.Length);

				result.TilesetImage.Width = result.TileWidth;
				result.TilesetImage.Height = (int)(result.TileHeight * result.TilesCount);
				result.TilesetImage.CopyFrom(buffer, fileHeader.PixelSizeBytes);
			}

			return result;
		}

		#endregion
	}

	public abstract class AsepriteBaseChunkType
	{
	}

	public class AsepriteImage
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public List<Row> Rows { get; } = new();

		public byte[] this[int x, int y]
		{
			get => Rows[y].Columns[x];
		}

		public void Add(Row row)
		{
			Rows.Add(row);
		}

		public class Row
		{
			public List<byte[]> Columns { get; } = new();

			public void Add(byte[] value)
			{
				Columns.Add(value);
			}
		}
	}

	#endregion
}

#region Extensions

internal static class AsepriteExtensions
{
	public static long PosBeforeLastRead = 0L;

	public static void Skip(this BinaryReader reader, int bytes) => reader.ReadData(() => reader.ReadBytes(bytes));
	public static uint ReadDWord(this BinaryReader reader) => reader.ReadData(() => reader.ReadUInt32());
	public static ushort ReadWord(this BinaryReader reader) => reader.ReadData(() => reader.ReadUInt16());
	public static short ReadShort(this BinaryReader reader) => reader.ReadData(() => reader.ReadInt16());
	
	public static decimal ReadFixed(this BinaryReader reader) => reader.ReadData(() =>
	{
		// fixed = 16 bits whole, 16 bits decimal part.
		var data = reader.ReadDWord();
		decimal result = data;
		return result / 0x10000;
	});

	public static string ReadUTF8String(this BinaryReader reader) => reader.ReadData(() =>
	{
		var length = reader.ReadWord();
		var bytes = reader.ReadBytes(length);
		return Encoding.UTF8.GetString(bytes);
	});

	private static T ReadData<T>(this BinaryReader reader, Func<T> handler)
	{
		PosBeforeLastRead = reader.BaseStream.Position;
		return handler();
	}

	public static uint ToUInt(this byte[] bytes)
	{
		uint result = 0;
		int offset = 0;

		// Bytes are expected in little endian format.
		foreach (var b in bytes)
		{
			uint value = b;
			uint shifted = value << offset;

			result |= shifted;
			shifted += 8;
		}

		return result;
	}

	public static void CopyFrom(this Aseprite.AsepriteImage image, byte[] buffer, int pixelSize)
	{
		var offset = 0;

		for (var y = 0; y < image.Height; y++)
		{
			var row = new Aseprite.AsepriteImage.Row();
			image.Add(row);

			for (var x = 0; x < image.Width; x++)
			{
				var slice = buffer[offset..(offset + pixelSize)];

				row.Add(slice);

				offset += pixelSize;
			}
		}
	}

}

#endregion
