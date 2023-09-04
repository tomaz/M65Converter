using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Exporting.Images;
using M65Converter.Sources.Exporting.Utils;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

using System.CommandLine;
using System.CommandLine.Binding;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses LDtk simplified output into mega 65 compatible files
/// </summary>
public class CharsRunner : BaseRunner
{
	private OptionsType Options { get; set; } = null!;
	private ImagesContainer CharsContainer { get; } = new();
	private LevelData MergedLayers { get; set; } = null!;
	private LayersData ExportData { get; } = new();

	#region Overrides

	protected override string? Title() => "Parsing layer files";

	protected override void OnValidate()
	{
		base.OnValidate();

		if ((Options.CharsBaseAddress % Options.CharData.CharDataSize) != 0)
		{
			var prev = (Options.CharsBaseAddress / Options.CharData.CharDataSize) * Options.CharData.CharDataSize;
			var next = prev + Options.CharData.CharDataSize;
			throw new ArgumentException($"Char base address must start on {Options.CharData.CharDataSize} byte boundary. C" +
				$"For example ${prev:X} or ${next:X}");
		}
	}

	protected override void OnRun()
	{
		void Parse(bool allowCompositeImage)
		{
			ClearData();
			ParseBaseChars();
			ParseInputs(allowCompositeImage);

			// The order of these methods is important - we first need to tackle palette since this is where we adjust colours and banks which are then needed to actually generate the output data.
			PrepareExportPalette();
			PrepareExportData();
			ValidateParsedData();
		}

		LogCmdLineOptions();

		try
		{
			Parse(allowCompositeImage: true);
		}
		catch (Invalid4BitPaletteException e)
		{
			Logger.Info.Separator();
			Logger.Info.Message(" ==============================================================================");
			Logger.Info.Message($"|| WARNING:");
			Logger.Info.Message($"|| {e.Message}");
			Logger.Info.Message($"|| Most common reasons are layer transparency or blending mode");
			Logger.Info.Message($"|| Attemting to manually merge layers (potentially less accurate output)");
			Logger.Info.Message(" ==============================================================================");

			Parse(allowCompositeImage: false);
		}

		// Note: the order of exports is not important from generated data perspective, but the given order results in nicely grouped log data, especially when verbose logging is enabled. This way it's simpler to compare related data as it's printed close together.
		ExportColoursData();
		ExportLayerData();
		ExportCharsData();
		ExportPaletteData();
		ExportLayerInfo();
		ExportInfoImage();
	}

	#endregion

	#region Parsing

	private void ClearData()
	{
		CharsContainer.Clear();
	}

	/// <summary>
	/// Parses base characters image to establish base set of chars to use.
	/// </summary>
	private void ParseBaseChars()
	{
		if (Options.BaseCharsImage == null) return;

		new TimeRunner
		{
			Title = "Base characters"
		}
		.Run(() =>
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {Options.BaseCharsImage}");
			Logger.Debug.Message($"Adding characters from base image {Options.BaseCharsImage.Name}");

			// Load the image.
			var image = Image.Load<Argb32>(Options.BaseCharsImage.FullName);

			// For base characters we keep all transparents to achieve consistent results. With these characters it's responsibility of the creator to trim source image. Same for duplicates, we want to leave all characters to preserve positions, however when matching them on layers, it will always take the first match.
			var result = new ImageSplitter
			{
				ItemWidth = Options.CharData.Width,
				ItemHeight = Options.CharData.Height,
				TransparencyOptions = TransparencyOptionsType.KeepAll,
				DuplicatesOptions = DuplicatesOptionsType.KeepAll
			}
			.Split(image, CharsContainer);

			// Note: we ignore indexed image for base characters. We only need actual layers from LDtk.
			Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} characters");
		});
	}

	/// <summary>
	/// Parses all inputs from cmd line arguments.
	/// 
	/// The result is all data needed for exporting is compiled into <see cref="MergedLayers"/> property. From here on, this is what should be used.
	/// </summary>
	private void ParseInputs(bool isCompositeImageAllowed = true)
	{
		void MergeLayers(LevelData data)
		{
			var options = new LayerMerger.OptionsType
			{
				IsRasterRewriteBufferSupported = Options.IsRasterRewriteBufferSupported,
				IsCompositeImageAllowed = isCompositeImageAllowed,
			};

			MergedLayers = LayerMerger
				.Create(options)
				.Merge(data);
		}

		void AppendExtraCharsFromLayers()
		{
			// Add fully transparent character if we don't yet have one. We need to have at least one fully transparent character so that we can properly setup indexed layers that contain transparent characters. If we already have transparent character (either from base characters set, or from previous layers), this will not create additional one.
			var transparentCharAddResult = CharsContainer.AddTransparentImage(
				width: Options.CharData.Width,
				height: Options.CharData.Height
			);

			foreach (var layer in MergedLayers.Layers)
			{
				Logger.Verbose.Separator();
				Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Name)}");

				// Log transparent character addition.
				if (transparentCharAddResult.WasAdded)
				{
					Logger.Verbose.Message("Adding transparent character");
				}

				// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
				var result = new ImageSplitter
				{
					ItemWidth = Options.CharData.Width,
					ItemHeight = Options.CharData.Height,
					TransparencyOptions = TransparencyOptionsType.OpaqueOnly,
					DuplicatesOptions = DuplicatesOptionsType.UniqueOnly
				}
				.Split(layer.Image, CharsContainer);

				// Assign indexed image to layer.
				layer.IndexedImage = result.IndexedImage;

				Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
			}
		}

		// Parse all input folders.
		new InputFilesHandler
		{
			Title = "Parsing",
			Sources = Options.Inputs
		}
		.Run(input =>
		{
			// Parse input data.
			var inputData = LevelData.Parse(input);

			// Prepare all layers we need to extract chars from.
			MergeLayers(inputData);

			// Add all extra characters from individual layers.
			AppendExtraCharsFromLayers();

			Logger.Verbose.Separator();
			Logger.Debug.Message($"{CharsContainer.Images.Count} characters found");
		});
	}

	#endregion

	#region Converting

	/// <summary>
	/// Merges all different colours from all layers into a single "global" palette to make it ready for exporting.
	/// </summary>
	private void PrepareExportPalette()
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = "Merging palette"
		}
		.Run(() =>
		{
			var options = new PaletteMerger.OptionsType
			{
				Is4Bit = Options.CharColour == OptionsType.CharColourType.NCM,
				IsUsingTransparency = true,
				Images = CharsContainer.Images,
			};

			// Note: merging not only prepares the final palette for export, but also remaps all character images colours to point to this generated palette.
			ExportData.Palette = PaletteMerger
				.Create(options)
				.Merge();
		});
	}

	/// <summary>
	/// Converts layers data into format suitable for exporting.
	/// </summary>
	private void PrepareExportData()
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = "Preparing layers data"
		}
		.Run(() =>
		{
			var screen = new LayersData.Layer();
			var colour = new LayersData.Layer();

			string? layerName = null;
			LayersData.Column.DataType dataType;

			void AddScreenBytes(LayersData.Row row, int index, ImageData data)
			{
				var charAddress = Options.CharIndexInRam(index);

				// Char index is always the same regardless of mode.
				byte byte1 = (byte)(charAddress & 0xff);
				byte byte2 = (byte)((charAddress >> 8) & 0xff);

				var column = row.AddColumn(byte1, byte2);

				// Assign data type and layer name.
				column.Tag = layerName;
				column.Type = dataType;

				// For chars data1 is char index, data2 is "index in ram" or "address" (of sorts).
				column.Data1 = index;
				column.Data2 = charAddress;
			}

			void AddColourBytes(LayersData.Row row, int index, ImageData data)
			{
				LayersData.Column column = null!;

				switch (Options.CharColour)
				{
					case OptionsType.CharColourType.FCM:
					{
						// For FCM colours are not important (until we implement char flipping for example), we always use 0.
						column = row.AddColumn(0x00, 0x00);
						break;
					}

					case OptionsType.CharColourType.NCM:
					{
						// For NCM colours RAM is where we set FCM mode for the character as well as palette bank.

						//            +-------------- vertically flip character
						//            |+------------- horizontally flip character
						//            ||+------------ alpha blend mode
						//            |||+----------- gotox
						//            ||||+---------- use 4-bits per pixel and 16x8 chars
						//            |||||+--------- trim pixels from right char side
						//            |||||| +------- number of pixels to trim
						//            |||||| |
						//            ||||||-+
						byte byte1 = 0b00001000;

						//            +-------------- underline
						//            |+-------------- bold
						//            ||+------------- reverse
						//            |||+------------ blink
						//            |||| +---------- colour bank 0-16
						//            |||| |
						//            ||||-+--
						byte byte2 = 0b00000000;
						byte2 |= (byte)(data.IndexedImage.Bank & 0x0f);

						// No sure why colour bank needs to be in high nibble. According to documentation this is needed if VIC II multi-colour-mode is enabled, however in my code this is also needed if VIC III extended attributes are enabled (AND VIC II MCM is disabled).
						byte2 = byte2.SwapNibble();

						column = row.AddColumn(byte1, byte2);

						// For colours data1 represents colour bank (only meaningful for NCM).
						column.Type = dataType;
						column.Data1 = data.IndexedImage.Bank;

						break;
					}
				}

				// Assign data type and layer name.
				column.Tag = layerName;
				column.Type = dataType;
			}

			void AddScreenDelimiterBytes(LayersData.Row row)
			{
				// Byte 0 is lower 8 bits of new X position for upcoming layer. We set it to 0 which means "render over the top of left-most character".
				byte byte1 = 0;

				// Byte 1:
				//              +-------------- FCM char Y offset
				//              |  +----------- reserved
				//              |  |  +-------- upper 2 bits of X position
				//             /-\/+\|\
				byte byte2 = 0b00000000;

				var column = row.AddColumn(byte1, byte2);
				column.Type = LayersData.Column.DataType.Attribute;
			}

			void AddColourDelimiterBytes(LayersData.Row row)
			{
				// Byte 0:
				//             +-------------- 1 = don't draw transparent pixels
				//             |+------------- 1 = render following chars as background (sprites appear above)
				//             ||+------------ reserved
				//             |||+----------- GOTOX
				//             ||||+---------- 1 = use pixel row mask from byte2 
				//             |||||+--------- 1 = render following chars as foreground (sprites appear behind)
				//             |||||| +------- reserved
				//             |||||| |
				//             ||||||/\
				byte byte1 = 0b10010000;

				// Byte 1 = pixel row mask.
				byte byte2 = 0x00;

				var column = row.AddColumn(byte1, byte2);
				column.Type = LayersData.Column.DataType.Attribute;
			}

			for (var i = 0; i < MergedLayers.Layers.Count; i++)
			{
				var layer = MergedLayers.Layers[i];

				// Setup layer name and data type - the first column we'll add is marked as "first data" for later handling.
				layerName = layer.Name;
				dataType = LayersData.Column.DataType.FirstData;

				// First layer name is assigned to our one-and-only result layer, for both, screen and colour data.
				if (i == 0)
				{
					screen.Name = layerName;
					colour.Name = layerName;
				}

				// Adjust width and height of exported layers.
				if (layer.IndexedImage.Width > ExportData.LayerWidth) ExportData.LayerWidth = layer.IndexedImage.Width;
				if (layer.IndexedImage.Height > ExportData.LayerHeight) ExportData.LayerHeight = layer.IndexedImage.Height;

				for (var y = 0; y < layer.IndexedImage.Height; y++)
				{
					// Create new rows if needed. This should only happen during the first height iteration.
					if (y >= screen.Rows.Count) screen.Rows.Add(new());
					if (y >= colour.Rows.Count) colour.Rows.Add(new());

					// These two lines are where appending data to rows "happens" - for every subsequent layer we will reiterate the same y coordinates so we'll take existing row classes from the lists.
					var screenRow = screen.Rows[y];
					var colourRow = colour.Rows[y];

					// We must insert GOTOX delimiters between layers.
					if (i > 0)
					{
						AddScreenDelimiterBytes(screenRow);
						AddColourDelimiterBytes(colourRow);
					}

					// Handle all chars of the current row. This will append data to existing rows in RRB mode.
					for (var x = 0; x < layer.IndexedImage.Width; x++)
					{
						var charIndex = layer.IndexedImage[x, y];
						var charData = CharsContainer.Images[charIndex];

						AddScreenBytes(screenRow, charIndex, charData);
						AddColourBytes(colourRow, charIndex, charData);

						// After adding data at (0,0) of each layer, switch to normal data type and reset layer name.
						dataType = LayersData.Column.DataType.Data;
						layerName = null;
					}
				}
			}

			// Store the data.
			ExportData.LevelName = MergedLayers.LevelName;
			ExportData.RootFolder = MergedLayers.RootFolder;
			ExportData.Screen = screen;
			ExportData.Colour = colour;

			Logger.Debug.Message($"{MergedLayers.Layers.Count} source layers");
			Logger.Debug.Message($"{ExportData.Screen.Width * Options.CharData.PixelDataSize}x{ExportData.Screen.Height} screen & colour data size");
		});
	}

	#endregion

	#region Exporting

	private void ExportColoursData()
	{
		CreateExporter("colour ram", "colour.bin").Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"Each colour is {Options.CharData.PixelDataSize} bytes");
			Logger.Verbose.Option("Top-to-down, left-to-right order");

			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					MinValueLength = 4,
				}
				: null;

			for (var y = 0; y < ExportData.Colour.Rows.Count; y++)
			{
				var row = ExportData.Colour.Rows[y];

				formatter?.StartNewRow();

				for (var x = 0; x < row.Columns.Count; x++)
				{
					var column = row.Columns[x];

					formatter?.AppendData(column.LittleEndianData);

					foreach (var data in column.Values)
					{
						writer.Write(data);
					}
				}
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Exported colours (little endian hex values):");
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportLayerData()
	{
		CreateExporter("layers", "layer.bin").Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"Expected to be copied to memory address ${Options.CharsBaseAddress:X}");
			Logger.Verbose.Option($"Char start index {Options.CharIndexInRam(0)} (${Options.CharIndexInRam(0):X})");
			Logger.Verbose.Option("All pixels as char indices");
			Logger.Verbose.Option($"Each pixel is {Options.CharData.PixelDataSize} bytes");
			Logger.Verbose.Option("Top-to-down, left-to-right order");

			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
				}
				: null;

			for (var y = 0; y < ExportData.Screen.Rows.Count; y++)
			{
				var row = ExportData.Screen.Rows[y];

				formatter?.StartNewRow();

				for (var x = 0; x < row.Columns.Count; x++)
				{
					var column = row.Columns[x];

					// We log as big endian to potentially preserve 1-2 chars in the output. See comment in `TableFormatter.FormattedData()` method for more details.
					formatter?.AppendData(column.BigEndianData);

					foreach (var data in column.Values)
					{
						writer.Write(data);
					}
				}
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Exported layer (big endian hex char indices adjusted to base address ${Options.CharsBaseAddress:X}):");
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportCharsData()
	{
		CreateExporter("chars", "chars.bin").Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"{CharsContainer.Images.Count} characters");
			switch (Options.CharColour)
			{
				case OptionsType.CharColourType.NCM:
					Logger.Verbose.Option("Each character is 16x8 pixels");
					Logger.Verbose.Option("Each pixel is 4 bits, 2 successive pixels form 1 byte");
					break;
				case OptionsType.CharColourType.FCM:
					Logger.Verbose.Option("Each character is 8x8 pixels");
					Logger.Verbose.Option("Each pixel is 8 bits / 1 byte");
					break;
			}
			Logger.Verbose.Option("All pixels as palette indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			Logger.Verbose.Option($"Character size is {Options.CharData.CharDataSize} bytes");

			var charData = Logger.Verbose.IsEnabled ? new List<byte>() : null;
			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					Headers = new[] { "Address", "Index", $"Data ({Options.CharData.CharDataSize} bytes)" },
					Prefix = " $",
					Suffix = " "
				}
				: null;

			var charIndex = -1;
			foreach (var character in CharsContainer.Images)
			{
				charIndex++;

				var startingFilePosition = (int)writer.BaseStream.Position;

				charData?.Clear();
				formatter?.StartNewRow();

				for (var y = 0; y < character.IndexedImage.Height; y++)
				{
					switch (Options.CharColour)
					{
						case OptionsType.CharColourType.NCM:
							for (var x = 0; x < character.IndexedImage.Width; x += 2)
							{
								var colour1 = character.IndexedImage[x, y];
								var colour2 = character.IndexedImage[x + 1, y];
								var colour = (byte)(((colour1 & 0x0f) << 4) | (colour2 & 0x0f));
								var swapped = colour.SwapNibble();
								charData?.Add(swapped);
								writer.Write(swapped);
							}
							break;

						case OptionsType.CharColourType.FCM:
							for (var x = 0; x < character.IndexedImage.Width; x++)
							{
								var colour = (byte)(character.IndexedImage[x, y] & 0xff);
								charData?.Add(colour);
								writer.Write(colour);
							}
							break;
					}
				}

				formatter?.AppendData(Options.CharsBaseAddress + startingFilePosition);
				formatter?.AppendData(Options.CharIndexInRam(charIndex));
				if (charData != null)
				{
					var dataArray = charData.ToArray();
					var first8 = string.Join("", dataArray[0..7].Select(x => x.ToString("X2")));
					var last8 = string.Join("", dataArray[^8..].Select(x => x.ToString("X2")));
					formatter?.AppendString($"{first8}...{last8}");
				}
			}

			Logger.Verbose.Separator();
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportPaletteData()
	{
		CreateExporter("palette", "chars.pal").Export(writer =>
		{
			new PaletteExporter().Export(
				palette: ExportData.Palette.Select(x => x.Colour).ToList(),
				writer: writer
			);
		});
	}

	private void ExportLayerInfo()
	{
		CreateExporter("layer info", "layer.inf").Export(writer =>
		{
			var charSize = Options.CharData.PixelDataSize;

			var layerWidth = ExportData.Screen.Width;
			var layerHeight = ExportData.Screen.Height;
			var layerSizeChars = layerWidth * layerHeight;
			var layerSizeBytes = layerSizeChars * charSize;
			var layerRowSize = layerWidth * charSize;

			var screenColumns = new[] { 40, 80 };
			var screenCharColumns = new[]
			{
				Options.CharData.CharsPerScreenWidth40Columns,
				Options.CharData.CharsPerScreenWidth80Columns,
			};

			Logger.Verbose.Separator();
			Logger.Verbose.Message("Format (hex values in little endian):");

			// Character info.
			var formatter = Logger.Verbose.IsEnabled ? TableFormatter.CreateFileFormatter() : null;

			writer.Write((byte)charSize);
			formatter?.AddFileFormat(size: 1, value: charSize, description: "Character size in bytes");

			writer.Write((byte)0xff);
			formatter?.AddFileFormat(size: 1, value: 0xff, description: "Unused");

			// Layer info.
			formatter?.AddFileSeparator();
				
			writer.Write((ushort)layerWidth);
			formatter?.AddFileFormat(size: 2, value: layerWidth, description: "Layer width in characters");

			writer.Write((ushort)layerHeight);
			formatter?.AddFileFormat(size: 2, value: layerHeight, description: "Layer height in characters");
				
			writer.Write((ushort)layerRowSize);
			formatter?.AddFileFormat(size: 2, value: layerRowSize, description: "Layer row size in bytes (logical row size)");
				
			writer.Write((uint)layerSizeChars);
			formatter?.AddFileFormat(size: 4, value: layerSizeChars, description: "Layer size in characters (width * height)");
				
			writer.Write((uint)layerSizeBytes);
			formatter?.AddFileFormat(size: 4, value: layerSizeBytes, description: "Layer size in bytes (width * height * char size)");

			// Screen info.
			for (var i = 0; i < screenColumns.Length; i++)
			{
				var columns = screenColumns[i];
				var width = screenCharColumns[i];
				var height = Options.CharData.CharsPerScreenHeight;	// height is always the same

				formatter?.AddFileSeparator();

				writer.Write((byte)width);
				formatter?.AddFileFormat(size: 1, value: width, description: $"Characters per {columns} column screen width");

				writer.Write((byte)height);
				formatter?.AddFileFormat(size: 1, value: height, description: "Characters per screen height");

				writer.Write((ushort)(width * charSize));
				formatter?.AddFileFormat(size: 2, value: width * charSize, description: "Screen row size in bytes");

				writer.Write((ushort)(width * height));
				formatter?.AddFileFormat(size: 2, value: width * height, description: "Screen size in characters");

				writer.Write((ushort)(width * height * charSize));
				formatter?.AddFileFormat(size: 2, value: width * height * charSize, description: "Screen size in bytes");
			}

			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportInfoImage()
	{
		if (Options.InfoRenderingScale <= 0) return;

		CreateExporter("info image", "info.png").Prepare(path =>
		{
			new CharsImageExporter
			{
				Scale = Options.InfoRenderingScale,
				LayersData = ExportData,
				CharsContainer = CharsContainer,
				CharInfo = Options.CharData,
				CharsBaseAddress = Options.CharsBaseAddress
			}
			.Draw(path);
		});
	}

	private Exporter CreateExporter(string description, string filename)
	{
		// Prefer explicit output folder, but fall down to layers root folder.
		var path = Options.OutputFolder?.FullName ?? ExportData.RootFolder;

		// Prepare the filename from name template.
		var name = Options.OutputNameTemplate
			.Replace("{level}", ExportData.LevelName)
			.Replace("{filename}", filename);

		return new()
		{
			LogDescription = description,
			Filename = Path.Combine(path, name),
		};
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Logs important input options and describes what actions will occur.
	/// 
	/// This is mainly useful for debugging purposes.
	/// </summary>
	private void LogCmdLineOptions()
	{
		Logger.Debug.Separator();

		if (Options.BaseCharsImage != null)
		{
			Logger.Debug.Option($"Base characters will be generated from: {Path.GetFileName(Options.BaseCharsImage.FullName)}");
			Logger.Debug.Option("Additional characters will be generated from layer images");
		}
		else
		{
			Logger.Debug.Option("Characters will be generated from layer images");
		}

		if (Options.IsRasterRewriteBufferSupported)
		{
			Logger.Debug.Option("Individual layers will be exported as RRB");
		}
		else
		{
			Logger.Debug.Option("Layers will be merged");
			if (Options.BaseCharsImage != null)
			{
				Logger.Info.Option("NOTE: merging layers may result in extra characters to be generated on top of base character set. Especially if layers use characters with transparent pixels.");
			}
		}

		Logger.Debug.Option(string.Join("", new string[]
		{
			$"Character type: {Options.CharColour} (",
			$"{Options.CharData.Width}x{Options.CharData.Height} pixels, ",
			$"{Options.CharData.ColoursPerChar} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Options.CharData.PixelDataSize} bytes");

		var firstChar = Options.CharIndexInRam(0);
		Logger.Debug.Option($"Characters base address: ${Options.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");

		if (Options.InfoRenderingScale > 0)
		{
			Logger.Debug.Option($"Info image scaled at {Options.InfoRenderingScale}x will be generated");
		}
	}

	/// <summary>
	/// Validates input data to make sure we can export it.
	/// 
	/// Note: this doesn't take care of all possible issues. It checks for the most obvious issues while all specific details will be checked later on during export. This should be the last step beofre invoking export.
	/// </summary>
	private void ValidateParsedData()
	{
		if (CharsContainer.Images.Count > 8192)
		{
			throw new ArgumentException("Too many characters to fit 2 bytes, adjust source files");
		}

		if (ExportData.Palette.Count > 256)
		{
			throw new ArgumentException("Too many palette entries, adjust source files to use less colours");
		}
	}

	#endregion

	#region Options

	public class OptionsType
	{
		/// <summary>
		/// Array of input files or folders.
		/// 
		/// Each input can be one of:
		/// - folder where simplified level data is exported (`data.json` file is expected to be present in this folder)
		/// - path to simplified level `data.json` file (including `data.json` file name)
		/// </summary>
		public FileInfo[] Inputs { get; set; } = null!;

		/// <summary>
		/// Optional external characters image.
		/// 
		/// Useful when characters order is important and should be preserved. For example to predictably setup digits and letters or animate individual charcters. If this file is provided, it will be split into characters as is, including all fully transparent tiles. These characters will be generated first, then all additional characters required to render individual layers will be added afterwards (this is mainly needed where semi-transparent characters are present in layers and layers need to be merged).
		/// 
		/// If this image is not provided, then all characters will be auto-generated from source images. This is the simplest way of using the converter, however characters order may vary between different calls, according to changes in source images.
		/// </summary>
		public FileInfo? BaseCharsImage { get; set; }

		/// <summary>
		/// Output folder.
		/// 
		/// All generated data files will be generated in this folder. See also <see cref="OutputNameTemplate"/>.
		/// </summary>
		public FileInfo? OutputFolder { get; set; } = null!;

		/// <summary>
		/// Template for output filenames.
		/// 
		/// The following placeholders can be used:
		/// - {level} placeholder is replaced with level (input) name
		/// - {filename} placeholder is replaced by output file name
		/// 
		/// Placeholders are replaced with corresponding data based on parsed values.
		/// 
		/// Note: since each level is exported to multiple files, this approach is used to provide some flexibility in naming each file while keeping command line simple. For example user might want to save all files for each level into its own subfolder (for example: `{level}\{name}-{suffix}` which would generate something like: `level1\level-chars.bin` etc). In the future we could think of adding each specific filename as its own cmd line option.
		/// </summary>
		public string OutputNameTemplate { get; set; } = null!;

		/// <summary>
		/// Specifies whether multiple layers should result in RRB output.
		/// 
		/// If RRB is enabled, then each layer will be treated as its own RRB layer. Otherwise all layers will be squashed into a single layer. The latter option works best when characters are fully opaque so the ones in top layers fully cover the ones below.
		/// </summary>
		public bool IsRasterRewriteBufferSupported { get; set; }

		/// <summary>
		/// Base address where the characters will be loaded into on Mega 65.
		/// </summary>
		public int CharsBaseAddress { get; set; }

		/// <summary>
		/// Specifies the scale at which info image should be rendered. If less than or equal to 0, info image is not generated.
		/// </summary>
		public int InfoRenderingScale { get; set; }

		/// <summary>
		/// Tile type. Changing this value will change <see cref="CharData"/> as well.
		/// </summary>
		public CharColourType CharColour {
			get => _charColour;
			set
			{
				_charColour = value;
				_charInfo = null;
			}
		}
		private CharColourType _charColour;

		/// <summary>
		/// Tile information, depends on <see cref="CharColour"/>.
		/// </summary>
		public CharInfo CharData {
			get
			{
				_charInfo ??= CharColour switch
				{
					CharColourType.FCM => new CharInfo
					{
						Width = 8,
						Height = 8,
						PixelDataSize = 2,
						CharDataSize = 64,
						ColoursPerChar = 256,
						CharsPerScreenWidth80Columns = 80,
					},

					CharColourType.NCM => new CharInfo
					{
						Width = 16,
						Height = 8,
						PixelDataSize = 2,
						CharDataSize = 64,
						ColoursPerChar = 16,
						CharsPerScreenWidth80Columns = 40,
					},

					_ => throw new ArgumentException($"Unknown tile type {CharColour}")
				};

				return _charInfo!;
			}
		}
		private CharInfo? _charInfo = null;

		#region Helpers

		/// <summary>
		/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
		/// </summary>
		public int CharIndexInRam(int relativeIndex) => CharData.CharIndexInRam(CharsBaseAddress, relativeIndex);

		#endregion

		#region Declarations

		public enum CharColourType
		{
			FCM,
			NCM
		}

		#endregion
	}

	public class OptionsBinder : BaseOptionsBinder<OptionsType>
	{
		#region Options

		private Argument<FileInfo[]> inputFolders = new(
			name: "input",
			description: "One or more input files or folders"
		)
		{
			Arity = ArgumentArity.OneOrMore
		};

		private Option<FileInfo?> baseCharsImage = new(
			aliases: new[] { "-c", "--chars" },
			description: "Optional base characters image"
		);

		private Option<FileInfo?> outputFolder = new(
			aliases: new[] { "-o", "--out-folder" },
			description: "Folder to generate output in; input folder if not specified"
		);

		private Option<string> outputFileTemplate = new(
			aliases: new[] { "-n", "--out-name" },
			description: string.Join("\n", new string[] 			
			{
				"Name prefix to use for output generation. Can also include subfolder(s). Placeholders:",
				"- {level} placeholder is replaced with level name",
				"- {filename} placeholder is replaced by output file name",
				""	// this is used so default value is written in new line
			}),
			getDefaultValue: () => "{level}\\{filename}"
		);

		private Option<OptionsType.CharColourType> tileType = new(
			aliases: new[] { "-m", "--colour" },
			description: "Colour mode",
			getDefaultValue: () => OptionsType.CharColourType.FCM
		);

		private Option<string> charBaseAddress = new(
			aliases: new[] { "-a", "--chars-address" },
			description: "Base address where characters will be loaded into on Mega 65",
			getDefaultValue: () => "$10000"
		);

		private Option<bool> rasterRewriteBuffer = new(
			aliases: new[] { "-r", "--rrb" },
			description: "Raster rewrite buffer support. If enabled, each layer is exported separately using RRB",
			getDefaultValue: () => false
		);

		private Option<int> imageInfoScale = new(
			name: "--info",
			description: "Info image scale (1 or greater to enable, 0 to disable). NOTE: this can be quite slow!",
			getDefaultValue: () => 0
		);

		#endregion

		#region Overrides

		protected override Command OnCreateCommand()
		{
			return new Command(
				name: "chars",
				description: "Converts simplified LDtk export or Aseprite file into characters data"
			);
		}

		protected override BaseRunner OnCreateRunner(OptionsType options)
		{
			return new CharsRunner
			{
				Options = options,
			};
		}

		protected override OptionsType GetBoundValue(BindingContext bindingContext)
		{
			return new OptionsType
			{
				Inputs = bindingContext.ParseResult.GetValueForArgument(inputFolders),
				BaseCharsImage = bindingContext.ParseResult.GetValueForOption(baseCharsImage),
				OutputFolder = bindingContext.ParseResult.GetValueForOption(outputFolder),
				OutputNameTemplate = bindingContext.ParseResult.GetValueForOption(outputFileTemplate)!,
				CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)!.ParseAsInt(),
				CharColour = bindingContext.ParseResult.GetValueForOption(tileType),
				IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer),
				InfoRenderingScale = bindingContext.ParseResult.GetValueForOption(imageInfoScale)
			};
		}

		#endregion
	}

	#endregion
}
