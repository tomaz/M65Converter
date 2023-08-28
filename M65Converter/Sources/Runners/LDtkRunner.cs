using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Exporting;
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
public class LDtkRunner : BaseRunner
{
	private OptionsType Options { get; set; } = null!;
	private ImagesContainer CharsContainer { get; } = new();
	private LevelData MergedLayers { get; set; } = null!;
	private ExportLevelData ExportData { get; } = new();

	#region Overrides

	protected override void OnValidate()
	{
		base.OnValidate();

		if ((Options.CharsBaseAddress % Options.CharInfo.CharDataSize) != 0)
		{
			var prev = (Options.CharsBaseAddress / Options.CharInfo.CharDataSize) * Options.CharInfo.CharDataSize;
			var next = prev + Options.CharInfo.CharDataSize;
			throw new ArgumentException($"Char base address must start on {Options.CharInfo.CharDataSize} byte boundary. C" +
				$"For example ${prev:X} or ${next:X}");
		}
	}

	protected override void OnRun()
	{
		LogCmdLineOptions();

		ParseBaseChars();
		ParseInputs();

		// The order of these methods is important - we first need to tackle palette since this is where we adjust colours and banks which are then needed to actually generate the output data.
		PrepareExportPalette();
		PrepareExportData();
		ValidateParsedData();

		// Note: the order of exports is not important from generated data perspective, but the given order results in nicely grouped log data, especially when verbose logging is enabled. This way it's simpler to compare related data as it's printed close together.
		ExportColoursData();
		ExportLayerData();
		ExportCharsData();
		ExportPaletteData();
		ExportLayerInfo();
	}

	#endregion

	#region Parsing

	/// <summary>
	/// Parses base characters image to establish base set of chars to use.
	/// </summary>
	private void ParseBaseChars()
	{
		if (Options.BaseCharsImage == null) return;

		new TimeRunner().Run(() =>
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {Options.BaseCharsImage}");
			Logger.Debug.Message($"Adding characters from base image {Options.BaseCharsImage.Name}");

			// Load the image.
			var image = Image.Load<Argb32>(Options.BaseCharsImage.FullName);

			// For base characters we keep all transparents to achieve consistent results. With these characters it's responsibility of the creator to trim source image. Same for duplicates, we want to leave all characters to preserve positions, however when matching them on layers, it will always take the first match.
			var result = new ImageSplitter
			{
				ItemWidth = Options.CharInfo.Width,
				ItemHeight = Options.CharInfo.Height,
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
	private void ParseInputs()
	{
		void MergeLayers(LevelData data)
		{
			var options = new LayerMerger.OptionsType
			{
				IsRasterRewriteBufferSupported = Options.IsRasterRewriteBufferSupported
			};

			MergedLayers = LayerMerger
				.Create(options)
				.Merge(data);
		}

		void AppendExtraCharsFromLayers()
		{
			// Add fully transparent character if we don't yet have one. We need to have at least one fully transparent character so that we can properly setup indexed layers that contain transparent characters. If we already have transparent character (either from base characters set, or from previous layers), this will not create additional one.
			var transparentCharAddResult = CharsContainer.AddTransparentImage(
				width: Options.CharInfo.Width,
				height: Options.CharInfo.Height
			);

			foreach (var layer in MergedLayers.Layers)
			{
				Logger.Verbose.Separator();
				Logger.Verbose.Message($"{layer.Path}");
				Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Path)}");

				// Log transparent character addition.
				if (transparentCharAddResult.WasAdded)
				{
					Logger.Verbose.Message("Adding transparent character");
				}

				// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
				var result = new ImageSplitter
				{
					ItemWidth = Options.CharInfo.Width,
					ItemHeight = Options.CharInfo.Height,
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
			Sources = Options.Inputs
		}
		.Run(input =>
		{
			// Parse LDtk input data.
			var inputData = LevelData.ParseLDtk(input);

			// Prepare all layers we need to extract chars from.
			MergeLayers(inputData);

			// Add all extra characters from individual layers.
			AppendExtraCharsFromLayers();
		});

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{CharsContainer.Images.Count} characters found");
	}

	#endregion

	#region Converting

	/// <summary>
	/// Merges all different colours from all layers into a single "global" palette to make it ready for exporting.
	/// </summary>
	private void PrepareExportPalette()
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
	}

	/// <summary>
	/// Converts layers data into format suitable for exporting.
	/// </summary>
	private void PrepareExportData()
	{
		var screen = new ExportLevelData.Layer();
		var colour = new ExportLevelData.Layer();

		void AddScreenBytes(ExportLevelData.Row row, int index, ImageData data)
		{
			var charIndex = Options.CharIndexInRam(index);

			// Char index is always the same regardless of mode.
			byte byte1 = (byte)(charIndex & 0xff);
			byte byte2 = (byte)((charIndex >> 8) & 0xff);

			row.AddColumn(byte1, byte2);
		}

		void AddScreenDelimiterBytes(ExportLevelData.Row row)
		{
		}

		void AddColourBytes(ExportLevelData.Row row, int index, ImageData data)
		{
			switch (Options.CharColour)
			{
				case OptionsType.CharColourType.FCM:
				{
					// For FCM colours are not important (until we implement char flipping for example), we always use 0.
					row.AddColumn(0x00, 0x00);
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

					row.AddColumn(byte1, byte2);
					break;
				}
			}
		}

		void AddColourDelimiterBytes(ExportLevelData.Row row)
		{
		}

		foreach (var layer in MergedLayers.Layers)
		{
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
				if (y > 0)
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
				}
			}
		}

		// Store the data.
		ExportData.LevelName = MergedLayers.LevelName;
		ExportData.RootFolder = MergedLayers.RootFolder;
		ExportData.Screen = screen;
		ExportData.Colour = colour;
	}

	#endregion

	#region Exporting

	private void ExportColoursData()
	{
		CreateExporter("colour ram", "colour.bin").Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"Each colour is {Options.CharInfo.PixelDataSize} bytes");
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
			Logger.Verbose.Option($"Each pixel is {Options.CharInfo.PixelDataSize} bytes");
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
			Logger.Verbose.Option($"Character size is {Options.CharInfo.CharDataSize} bytes");

			var charData = Logger.Verbose.IsEnabled ? new List<byte>() : null;
			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					Headers = new[] { "Address", "Index", $"Data ({Options.CharInfo.CharDataSize} bytes)" },
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
				palette: ExportData.Palette,
				writer: writer
			);
		});
	}

	private void ExportLayerInfo()
	{
		CreateExporter("layer info", "layer.inf").Export(writer =>
		{
			var charSize = Options.CharInfo.PixelDataSize;

			var layerWidth = ExportData.LayerWidth;
			var layerHeight = ExportData.LayerHeight;
			var layerSizeChars = layerWidth * layerHeight;
			var layerSizeBytes = layerSizeChars * charSize;
			var layerRowSize = layerWidth * charSize;

			var screenColumns = new[] { 40, 80 };
			var screenCharColumns = new[]
			{
				Options.CharInfo.CharsPerScreenWidth40Columns,
				Options.CharInfo.CharsPerScreenWidth80Columns,
			};

			Logger.Verbose.Separator();
			Logger.Verbose.Message("Format (hex values in little endian):");
			if (Logger.Verbose.IsEnabled)
			{
				var formatter = TableFormatter.CreateFileFormatter();

				writer.Write((byte)charSize);
				formatter.AddFileFormat(size: 1, value: charSize, description: "Character size in bytes");

				writer.Write((byte)0xff);
				formatter.AddFileFormat(size: 1, value: 0xff, description: "Unused");

				formatter.AddFileSeparator();
				
				writer.Write((ushort)layerWidth);
				formatter.AddFileFormat(size: 2, value: layerWidth, description: "Layer width in characters");

				writer.Write((ushort)layerHeight);
				formatter.AddFileFormat(size: 2, value: layerHeight, description: "Layer height in characters");
				
				writer.Write((ushort)layerRowSize);
				formatter.AddFileFormat(size: 2, value: layerRowSize, description: "Layer row size in bytes (logical row size)");
				
				writer.Write((uint)layerSizeChars);
				formatter.AddFileFormat(size: 4, value: layerSizeChars, description: "Layer size in characters (width * height)");
				
				writer.Write((uint)layerSizeBytes);
				formatter.AddFileFormat(size: 4, value: layerSizeBytes, description: "Layer size in bytes (width * height * char size)");

				for (var i = 0; i < screenColumns.Length; i++)
				{
					var columns = screenColumns[i];
					var width = screenCharColumns[i];
					var height = Options.CharInfo.CharsPerScreenHeight;	// height is always the same

					formatter.AddFileSeparator();

					writer.Write((byte)width);
					formatter.AddFileFormat(size: 1, value: width, description: $"Characters per {columns} column screen width");

					writer.Write((byte)height);
					formatter.AddFileFormat(size: 1, value: height, description: "Characters per screen height");

					writer.Write((ushort)(width * charSize));
					formatter.AddFileFormat(size: 2, value: width * charSize, description: "Screen row size in bytes");

					writer.Write((ushort)(width * height));
					formatter.AddFileFormat(size: 2, value: width * height, description: "Screen size in characters");

					writer.Write((ushort)(width * height * charSize));
					formatter.AddFileFormat(size: 2, value: width * height * charSize, description: "Screen size in bytes");
				}

				formatter.Log(Logger.Verbose.Option);
			}
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
		Logger.Info.Message("Parsing LDtk files");

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
			$"{Options.CharInfo.Width}x{Options.CharInfo.Height} pixels, ",
			$"{Options.CharInfo.ColoursPerChar} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Options.CharInfo.PixelDataSize} bytes");

		var firstChar = Options.CharIndexInRam(0);
		Logger.Debug.Option($"Characters base address: ${Options.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");
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

	/// <summary>
	/// Enumerates level data and calls the given action for each coordinate.
	/// 
	/// If raster-rewrite-buffer is used, this will iterate over all layers in correct order for example.
	/// </summary>
	private void EnumerateLevelData(Action<int, int> handler)
	{
		var y = 0;
		var x = 0;
	}

	#endregion

	#region Declarations

	private class ExportLevelData
	{
		/// <summary>
		/// The width of the longest layer.
		/// </summary>
		public int LayerWidth { get; set; }

		/// <summary>
		/// The height of the tallest layer.
		/// </summary>
		public int LayerHeight { get; set; }

		/// <summary>
		/// Level name.
		/// </summary>
		public string LevelName { get; set; } = null!;

		/// <summary>
		/// Root folder where level source files are located.
		/// </summary>
		public string RootFolder { get; set; } = null!;

		/// <summary>
		/// The screen RAM data.
		/// </summary>
		public Layer Screen { get; set; } = new();

		/// <summary>
		/// The colour RAM data.
		/// </summary>
		public Layer Colour { get; set; } = new();

		/// <summary>
		/// All colours.
		/// </summary>
		public List<Argb32> Palette { get; set; } = new();

		#region Declarations

		/// <summary>
		/// The "data" - this can be anything that uses rows and columns format.
		/// </summary>
		public class Layer
		{
			/// <summary>
			/// All rows of the data.
			/// </summary>
			public List<Row> Rows { get; } = new();
		}

		/// <summary>
		/// Data for individual row.
		/// </summary>
		public class Row
		{
			/// <summary>
			/// All columns of this row.
			/// </summary>
			public List<Column> Columns { get; } = new();

			/// <summary>
			/// Convenience for adding a new <see cref="Column"/> with the given values to the end of <see cref="Columns"/> list.
			/// </summary>
			/// <param name="values"></param>
			public void AddColumn(params byte[] values)
			{
				Columns.Add(new()
				{
					Values = values.ToList()
				});
			}
		}

		/// <summary>
		/// Data for individual column.
		/// </summary>
		public class Column
		{
			/// <summary>
			/// All bytes needed to describe this column, in little endian format.
			/// </summary>
			public List<byte> Values { get; init; } = new();

			/// <summary>
			/// Returns all values as single little endian value (only supports up to 4 bytes!)
			/// </summary>
			public int LittleEndianData
			{
				get
				{
					var result = 0;

					// Values are already in little endian order.
					foreach (var value in Values)
					{
						result <<= 8;
						result |= value;
					}

					return result;
				}
			}

			/// <summary>
			/// Returns all values as single big endian value (only supports up to 4 digits!)
			/// </summary>
			public int BigEndianData
			{
				get
				{
					var result = 0;

					// Values are little endian order, so we need to reverse the array.
					for (var i = Values.Count - 1; i >= 0; i--)
					{
						result <<= 8;
						result |= Values[i];
					}

					return result;
				}
			}
		}

		#endregion
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
		/// Tile type. Changing this value will change <see cref="CharInfo"/> as well.
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
		public CharInfoType CharInfo {
			get
			{
				_charInfo ??= CharColour switch
				{
					CharColourType.FCM => new CharInfoType
					{
						Width = 8,
						Height = 8,
						PixelDataSize = 2,
						CharDataSize = 64,
						ColoursPerChar = 256,
						CharsPerScreenWidth80Columns = 80,
					},

					CharColourType.NCM => new CharInfoType
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
		private CharInfoType? _charInfo = null;

		#region Helpers

		/// <summary>
		/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
		/// </summary>
		public int CharIndexInRam(int relativeIndex)
		{
			return (CharsBaseAddress + relativeIndex * CharInfo.CharDataSize) / CharInfo.CharDataSize;
		}

		#endregion

		#region Declarations

		public enum CharColourType
		{
			FCM,
			NCM
		}

		public class CharInfoType
		{
			/// <summary>
			/// Width of the character in pixels.
			/// </summary>
			public int Width { get; init; }

			/// <summary>
			/// Height of character in pixels.
			/// </summary>
			public int Height { get; init; }

			/// <summary>
			/// Number of bytes each pixel requires.
			/// </summary>
			public int PixelDataSize { get; init; }

			/// <summary>
			/// Number of bytes each character (aka all pixels) require.
			/// </summary>
			public int CharDataSize { get; init; }

			/// <summary>
			/// Number of colours each character can have.
			/// </summary>
			public int ColoursPerChar { get; init; }

			/// <summary>
			/// Number of characters that can be rendered in each line when 80 column mode is used.
			/// </summary>
			public int CharsPerScreenWidth80Columns { get; init; }

			/// <summary>
			/// Number of characters that can be rendered in each line when 40 column mode is used.
			/// </summary>
			public int CharsPerScreenWidth40Columns { get => CharsPerScreenWidth80Columns / 2; }

			/// <summary>
			/// Screen height in characters.
			/// </summary>
			public int CharsPerScreenHeight { get => 25; }
		}

		#endregion
	}

	public class OptionsBinder : BaseOptionsBinder<OptionsType>
	{
		#region Options

		private Argument<FileInfo[]> inputFolders = new(
			name: "input",
			description: "One or more input folders with LDtk simplified exported files"
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

		#endregion

		#region Overrides

		protected override Command OnCreateCommand()
		{
			return new Command(
				name: "ldtk",
				description: "Converts simplified LDtk output into raw data"
			);
		}

		protected override BaseRunner OnCreateRunner(OptionsType options)
		{
			return new LDtkRunner
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
				IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer)
			};
		}

		#endregion
	}

	#endregion
}
