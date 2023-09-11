using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting.Images;
using M65Converter.Sources.Exporting.Utils;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses LDtk or Aseprite background screen and colour data into mega 65 compatible files.
/// </summary>
public class ScreensRunner : BaseRunner
{
	#region Overrides

	protected override string? Title() => "Parsing layer files";

	protected override void OnValidate()
	{
		base.OnValidate();

		if ((Data.ScreenOptions.CharsBaseAddress % Data.ScreenOptions.CharData.CharDataSize) != 0)
		{
			var prev = (Data.ScreenOptions.CharsBaseAddress / Data.ScreenOptions.CharData.CharDataSize) * Data.ScreenOptions.CharData.CharDataSize;
			var next = prev + Data.ScreenOptions.CharData.CharDataSize;
			throw new ArgumentException($"Char base address must start on {Data.ScreenOptions.CharData.CharDataSize} byte boundary. C" +
				$"For example ${prev:X} or ${next:X}");
		}
	}

	protected override void OnRun()
	{
		LogCmdLineDataOptions();
		ClearData();

		ParseBaseChars();
		ParseInputs();
	}

	#endregion

	#region Parsing

	private void ClearData()
	{
		Data.CharsContainer.Clear();
	}

	/// <summary>
	/// Parses base characters image to establish base set of chars to use.
	/// </summary>
	private void ParseBaseChars()
	{
		if (Data.ScreenOptions.BaseCharsImage == null) return;

		new TimeRunner
		{
			Title = "Base characters"
		}
		.Run(() =>
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {Data.ScreenOptions.BaseCharsImage}");
			Logger.Debug.Message($"Adding characters from base image {Data.ScreenOptions.BaseCharsImage.GetFilename()}");

			// Load the image.
			var image = Image.Load<Argb32>(Data.ScreenOptions.BaseCharsImage.GetStream(FileMode.Open));

			// For base characters we keep all transparents to achieve consistent results. With these characters it's responsibility of the creator to trim source image. Same for duplicates, we want to leave all characters to preserve positions, however when matching them on layers, it will always take the first match.
			var result = new ImageSplitter
			{
				ItemWidth = Data.ScreenOptions.CharData.Width,
				ItemHeight = Data.ScreenOptions.CharData.Height,
				TransparencyOptions = TransparencyOptionsType.KeepAll,
				DuplicatesOptions = DuplicatesOptionsType.KeepAll
			}
			.Split(image, Data.CharsContainer);

			// After we parse base characters we set the restore point so we can later revert charset to base.
			Data.CharsContainer.SetRestorePoint();

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
		void AppendTransparentChar()
		{
			// Add fully transparent character if we don't yet have one. We need to have at least one fully transparent character so that we can properly setup indexed layers that contain transparent characters. If we already have transparent character (either from base characters set, or from previous layers), this will not create additional one.
			var transparentCharAddResult = Data.CharsContainer.AddTransparentImage(
				width: Data.ScreenOptions.CharData.Width,
				height: Data.ScreenOptions.CharData.Height
			);

			// Log transparent character addition.
			if (transparentCharAddResult.WasAdded)
			{
				Logger.Verbose.Message("Adding transparent character");
			}
		}

		void SetupLayerData(IStreamProvider input, bool isCompositeImageAllowed)
		{
			void MergeLayers(LevelData data)
			{
				var options = new LayerMerger.OptionsType
				{
					IsRasterRewriteBufferSupported = Data.ScreenOptions.IsRasterRewriteBufferSupported,
					IsCompositeImageAllowed = isCompositeImageAllowed,
				};

				Data.MergedLayers = LayerMerger
					.Create(options)
					.Merge(data);
			}

			void AppendExtraCharsFromLayers()
			{
				foreach (var layer in Data.MergedLayers.Layers)
				{
					Logger.Verbose.Separator();
					Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Name)}");

					// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
					var result = new ImageSplitter
					{
						ItemWidth = Data.ScreenOptions.CharData.Width,
						ItemHeight = Data.ScreenOptions.CharData.Height,
						TransparencyOptions = TransparencyOptionsType.OpaqueOnly,
						DuplicatesOptions = DuplicatesOptionsType.UniqueOnly
					}
					.Split(layer.Image, Data.CharsContainer);

					// Assign indexed image to layer.
					layer.IndexedImage = result.IndexedImage;

					Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
				}
			}

			// Characters (and consequently underlying palette) are updated with each input, but the rest of the export data is reset every time.
			ClearParsedData();

			// Parse input data.
			var inputData = LevelData.Parse(input);

			// Prepare all layers we need to extract chars from.
			MergeLayers(inputData);

			// Add all extra characters from individual layers.
			AppendExtraCharsFromLayers();

			Logger.Verbose.Separator();
			Logger.Debug.Message($"{Data.CharsContainer.Images.Count} characters found");
		}

		void ConvertLayersToExportData(bool isCompositeImageAllowed)
		{
			// The order of these methods is important - we first need to tackle palette since this is where we adjust colours and banks which are then needed to actually generate the output data.
			PrepareExportPalette(isCompositeImage: isCompositeImageAllowed);
			PrepareExportData();
			Data.ValidateParsedData();
		}

		// We only need 1 fully transparent character. If we don't yet have it from base chars, this is where we'll add it.
		AppendTransparentChar();

		// Parse all input folders.
		new InputFilesHandler
		{
			Title = "Parsing",
			Sources = Data.ScreenOptions.InputsOutputs.Select(x => x.Input).ToArray()
		}
		.Run((index, input) =>
		{
			try
			{
				// First attempt to use parsed composite image. This respects layer transparency and blending modes, so yields more accurate results. However it can easily overflow the palette. If this fails, we'll fall-down to manual layer merging in catch below.
				// Note: only certain types of inputs support composite images. If that's not supported for current input, layer merging will be used here as well.
				SetupLayerData(input, isCompositeImageAllowed: true);
				ConvertLayersToExportData(isCompositeImageAllowed: true);
			}
			catch (InvalidCompositeImageDataException e)
			{
				// Composite image failed, let's fall down to manual layer merging. This works perfectly fine when layers don't use transparency or blending modes, but can
				Logger.Info.Separator();
				Logger.Info.Message(" ==============================================================================");
				Logger.Info.Message($"|| WARNING:");
				Logger.Info.Message($"|| {e.Message}");
				Logger.Info.Message($"|| Most common reasons are layer transparency or blending mode");
				Logger.Info.Message($"|| Attemting to manually merge layers (potentially less accurate output)");
				Logger.Info.Message(" ==============================================================================");

				SetupLayerData(input, isCompositeImageAllowed: false);
				ConvertLayersToExportData(isCompositeImageAllowed: false);
			}
		});
	}

	#endregion

	#region Converting

	/// <summary>
	/// Merges all different colours from all layers into a single "global" palette to make it ready for exporting.
	/// </summary>
	private void PrepareExportPalette(bool isCompositeImage)
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
				Is4Bit = Data.ScreenOptions.CharColour == ScreenOptionsType.CharColourType.NCM,
				IsCompositeImage = isCompositeImage,
				IsUsingTransparency = true,
				Images = Data.CharsContainer.Images,
			};

			// Note: merging not only prepares the final palette for export, but also remaps all character images colours to point to this generated palette.
			Data.ExportData.Palette = PaletteMerger
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
				var charAddress = Data.ScreenOptions.CharIndexInRam(index);

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

				switch (Data.ScreenOptions.CharColour)
				{
					case ScreenOptionsType.CharColourType.FCM:
					{
						// For FCM colours are not important (until we implement char flipping for example), we always use 0.
						column = row.AddColumn(0x00, 0x00);
						break;
					}

					case ScreenOptionsType.CharColourType.NCM:
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

			for (var i = 0; i < Data.MergedLayers.Layers.Count; i++)
			{
				var layer = Data.MergedLayers.Layers[i];

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
				if (layer.IndexedImage.Width > Data.ExportData.LayerWidth) Data.ExportData.LayerWidth = layer.IndexedImage.Width;
				if (layer.IndexedImage.Height > Data.ExportData.LayerHeight) Data.ExportData.LayerHeight = layer.IndexedImage.Height;

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
						var charData = Data.CharsContainer.Images[charIndex];

						AddScreenBytes(screenRow, charIndex, charData);
						AddColourBytes(colourRow, charIndex, charData);

						// After adding data at (0,0) of each layer, switch to normal data type and reset layer name.
						dataType = LayersData.Column.DataType.Data;
						layerName = null;
					}
				}
			}

			// Store the data.
			Data.ExportData.LevelName = Data.MergedLayers.LevelName;
			Data.ExportData.RootFolder = Data.MergedLayers.RootFolder;
			Data.ExportData.Screen = screen;
			Data.ExportData.Colour = colour;

			Logger.Debug.Message($"{Data.MergedLayers.Layers.Count} source layers");
			Logger.Debug.Message($"{Data.ExportData.Screen.Width * Data.ScreenOptions.CharData.PixelDataSize}x{Data.ExportData.Screen.Height} screen & colour data size");
		});
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Logs important input Data.ScreenOptions and describes what actions will occur.
	/// 
	/// This is mainly useful for debugging purposes.
	/// </summary>
	private void LogCmdLineDataOptions()
	{
		Logger.Debug.Separator();

		if (Data.ScreenOptions.BaseCharsImage != null)
		{
			Logger.Debug.Option($"Base characters will be generated from: {Path.GetFileName(Data.ScreenOptions.BaseCharsImage.GetFilename())}");
			Logger.Debug.Option("Additional characters will be generated from layer images");
		}
		else
		{
			Logger.Debug.Option("Characters will be generated from layer images");
		}

		if (Data.ScreenOptions.IsRasterRewriteBufferSupported)
		{
			Logger.Debug.Option("Individual layers will be exported as RRB");
		}
		else
		{
			Logger.Debug.Option("Layers will be merged");
			if (Data.ScreenOptions.BaseCharsImage != null)
			{
				Logger.Info.Option("NOTE: merging layers may result in extra characters to be generated on top of base character set. Especially if layers use characters with transparent pixels.");
			}
		}

		Logger.Debug.Option(string.Join("", new string[]
		{
			$"Character type: {Data.ScreenOptions.CharColour} (",
			$"{Data.ScreenOptions.CharData.Width}x{Data.ScreenOptions.CharData.Height} pixels, ",
			$"{Data.ScreenOptions.CharData.ColoursPerChar} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Data.ScreenOptions.CharData.PixelDataSize} bytes");

		var firstChar = Data.ScreenOptions.CharIndexInRam(0);
		Logger.Debug.Option($"Characters base address: ${Data.ScreenOptions.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");

		if (Data.ScreenOptions.InfoRenderingScale > 0)
		{
			Logger.Debug.Option($"Info image scaled at {Data.ScreenOptions.InfoRenderingScale}x will be generated");
		}
	}

	/// <summary>
	/// Clears parsed data before each input.
	/// </summary>
	private void ClearParsedData()
	{
		Data.MergedLayers = new();
		Data.ExportData = new();
	}

	#endregion
}
