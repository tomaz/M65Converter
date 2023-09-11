using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses LDtk or Aseprite background screen and colour data into mega 65 compatible files.
/// </summary>
public class ScreensRunner : BaseRunner
{
	#region Overrides

	protected override string? Title() => "Parsing screen and colour data";

	protected override void OnValidate()
	{
		base.OnValidate();

		int charsAddress = Data.ScreenOptions.CharsBaseAddress;
		int charSize = Data.GlobalOptions.CharInfo.CharDataSize;
		if ((charsAddress % charSize) != 0)
		{
			var prev = (charsAddress / charSize) * charSize;
			var next = prev + charSize;

			throw new ArgumentException($"Char base address must start on {charSize} byte boundary. Consider changing to previous (${prev:X}) or next (${next:X})");
		}
	}

	protected override void OnRun()
	{
		LogCmdLineDataOptions();

		ParseInputs();
	}

	#endregion

	#region Parsing

	/// <summary>
	/// Parses all inputs from cmd line arguments.
	/// 
	/// The result is all data needed for exporting is compiled into <see cref="MergedLayers"/> property. From here on, this is what should be used.
	/// </summary>
	private void ParseInputs()
	{
		var mergedLevels = new List<LevelData>();

		// Parse all inputs.
		new InputFilesHandler
		{
			TitlePrefix = "Parsing layers from",
			Sources = Data.ScreenOptions.Inputs
		}
		.Run((index, input) =>
		{
			LevelData? mergedLevel = null;

			try
			{
				// First attempt to use parsed composite image. This respects layer transparency and blending modes, so yields more accurate results. However it can easily overflow the palette. If this fails, we'll fall-down to manual layer merging in catch below.
				// Note: only certain types of inputs support composite images. If that's not supported for current input, layer merging will be used here as well.
				// Note: we set restore point before each additinal parsing. This way we will always revert to last valid data if first attempt fails.
				Data.CharsContainer.SetRestorePoint();
				mergedLevel = ConvertInputToExportData(input, isCompositeImageAllowed: true);
			}
			catch (Exception e)
			{
				// Composite image failed, let's fall down to manual layer merging. This works perfectly fine when layers don't use transparency or blending modes, but can
				Logger.Info.Separator();
				Logger.Info.Message(" ==============================================================================");
				Logger.Info.Message($"|| WARNING:");
				Logger.Info.Message($"|| {e.Message}");
				Logger.Info.Message($"|| Most common reasons are layer transparency or blending mode");
				Logger.Info.Message($"|| Attemting to manually merge layers (potentially less accurate output)");
				Logger.Info.Message(" ==============================================================================");

				// After failed first attempt we restore characters data and retry, this time manually merging layers.
				Data.CharsContainer.ResetDataToRestorePoint();
				mergedLevel = ConvertInputToExportData(input, isCompositeImageAllowed: false);
			}

			// If all went well, we should add the level to the temporary list.
			mergedLevels.Add(mergedLevel);
		});

		// After all layers are parsed, we should prepare the rest of the data - palette and then all the screens. Note that the order is important, we should first prepare palette.
		PrepareExportPalette();

		// After palette is ready, we should prepare screen and colour data for each merged level.
		foreach (var mergedLevel in mergedLevels)
		{
			// Prepare the data for export.
			var screenData = new ScreenData();
			PrepareExportData(mergedLevel, destination: screenData);

			// If all went well (aka no exception thrown), add screen data to global results for later export. We don't export just yet since we may have additional steps that will append data later on.
			Data.Screens.Add(screenData);
		}
	}

	#endregion

	#region Converting

	/// <summary>
	/// Merges all layers from the given input.
	/// </summary>
	private LevelData ConvertInputToExportData(IStreamProvider input, bool isCompositeImageAllowed)
	{
		// Parse input data.
		var inputLayers = LevelData.Parse(input);

		// Prepare all layers we need to extract chars from.
		var mergedLayers = MergeLayers(inputLayers, isCompositeImageAllowed);

		// Add all extra characters from layers.
		AppendExtraCharsFromLayers(mergedLayers);

		// Validate all newly parsed data. This will throw exception as soon as the data becomes invalid so user can identify the input that causes the issue.
		Data.ValidateData();

		return mergedLayers;
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Merges all layers from the given level.
	/// </summary>
	private LevelData MergeLayers(LevelData data, bool isCompositeImageAllowed)
	{
		var options = new LayerMerger.OptionsType
		{
			IsRasterRewriteBufferSupported = Data.ScreenOptions.IsRasterRewriteBufferSupported,
			IsCompositeImageAllowed = isCompositeImageAllowed,
		};

		return LayerMerger
			.Create(options)
			.Merge(data);
	}

	/// <summary>
	/// Appens all extra characters needed for rendering the given merged layers.
	/// </summary>
	private void AppendExtraCharsFromLayers(LevelData mergedLayers)
	{
		foreach (var layer in mergedLayers.Layers)
		{
			Logger.Verbose.Separator();

			// We only need 1 fully transparent character. If we don't yet have it from base chars, this is where we'll add it. We could call this function outside the loop, but this way we get more meaningful log.
			AppendTransparentChar();

			Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Name)}");

			// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
			var result = new ImageSplitter
			{
				ItemWidth = Data.GlobalOptions.CharInfo.Width,
				ItemHeight = Data.GlobalOptions.CharInfo.Height,
				TransparencyOptions = TransparencyOptionsType.OpaqueOnly,
				DuplicatesOptions = DuplicatesOptionsType.UniqueOnly
			}
			.Split(layer.Image, Data.CharsContainer);

			// Assign indexed image to layer.
			layer.IndexedImage = result.IndexedImage;

			Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
		}

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{Data.CharsContainer.Images.Count} characters found");
	}

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
				Images = Data.CharsContainer.Images,
				Is4Bit = Data.GlobalOptions.ColourMode == CharColourMode.NCM,
				IsUsingTransparency = true,
			};

			// Note: merging not only prepares the final palette for export, but also remaps all character images colours to point to this generated palette.
			Data.Palette = PaletteMerger
				.Create(options)
				.Merge();

			// We should already validate the data while merging, but just in case do validate the final result again.
			Data.ValidateData();
		});
	}

	/// <summary>
	/// Converts layers data into format suitable for exporting.
	/// </summary>
	private void PrepareExportData(LevelData mergedLayers, ScreenData destination)
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = "Preparing screen and colour data"
		}
		.Run(() =>
		{
			var screen = new ScreenData.Layer();
			var colour = new ScreenData.Layer();

			string? layerName = null;
			ScreenData.Column.DataType dataType;

			void AddScreenBytes(ScreenData.Row row, int index, ImageData data)
			{
				var charAddress = Data.CharIndexInRam(index);

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

			void AddColourBytes(ScreenData.Row row, int index, ImageData data)
			{
				ScreenData.Column column = null!;

				switch (Data.GlobalOptions.ColourMode)
				{
					case CharColourMode.FCM:
					{
						// For FCM colours are not important (until we implement char flipping for example), we always use 0.
						column = row.AddColumn(0x00, 0x00);
						break;
					}

					case CharColourMode.NCM:
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

			void AddScreenDelimiterBytes(ScreenData.Row row)
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
				column.Type = ScreenData.Column.DataType.Attribute;
			}

			void AddColourDelimiterBytes(ScreenData.Row row)
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
				column.Type = ScreenData.Column.DataType.Attribute;
			}

			for (var i = 0; i < mergedLayers.Layers.Count; i++)
			{
				var layer = mergedLayers.Layers[i];

				// Setup layer name and data type - the first column we'll add is marked as "first data" for later handling.
				layerName = layer.Name;
				dataType = ScreenData.Column.DataType.FirstData;

				// First layer name is assigned to our one-and-only result layer, for both, screen and colour data.
				if (i == 0)
				{
					screen.Name = layerName;
					colour.Name = layerName;
				}

				// Adjust width and height of exported layers.
				if (layer.IndexedImage.Width > destination.LayerWidth) destination.LayerWidth = layer.IndexedImage.Width;
				if (layer.IndexedImage.Height > destination.LayerHeight) destination.LayerHeight = layer.IndexedImage.Height;

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
						dataType = ScreenData.Column.DataType.Data;
						layerName = null;
					}
				}
			}

			// Store the data.
			destination.LevelName = mergedLayers.LevelName;
			destination.RootFolder = mergedLayers.RootFolder;
			destination.Screen = screen;
			destination.Colour = colour;

			Logger.Debug.Message($"{mergedLayers.Layers.Count} source layers");
			Logger.Debug.Message($"{destination.Screen.Width * Data.GlobalOptions.CharInfo.PixelDataSize}x{destination.Screen.Height} screen & colour data size");
		});
	}

	/// <summary>
	/// Appends fully transparent character to global chars container.
	/// 
	/// If transparent character already exists, no change is performed.
	/// </summary>
	private void AppendTransparentChar()
	{
		// Add fully transparent character if we don't yet have one. We need to have at least one fully transparent character so that we can properly setup indexed layers that contain transparent characters. If we already have transparent character (either from base characters set, or from previous layers), this will not create additional one.
		var transparentCharAddResult = Data.CharsContainer.AddTransparentImage(
			width: Data.GlobalOptions.CharInfo.Width,
			height: Data.GlobalOptions.CharInfo.Height
		);

		// Log transparent character addition.
		if (transparentCharAddResult.WasAdded)
		{
			Logger.Verbose.Message("Adding transparent character");
		}
	}

	/// <summary>
	/// Logs important input Data.ScreenOptions and describes what actions will occur.
	/// 
	/// This is mainly useful for debugging purposes.
	/// </summary>
	private void LogCmdLineDataOptions()
	{
		Logger.Debug.Separator();

		if (Data.ScreenOptions.IsRasterRewriteBufferSupported)
		{
			Logger.Debug.Option("Individual layers will be exported as RRB");
		}
		else
		{
			Logger.Debug.Option("Layers will be merged");
			if (Data.CharOptions.Inputs?.Length > 0)
			{
				Logger.Info.Option("NOTE: merging layers may result in extra characters to be generated on top of base character set. Especially if layers use characters with transparent pixels.");
			}
		}

		Logger.Debug.Option(string.Join("", new string[]
		{
			$"Character mode: {Data.GlobalOptions.ColourMode} (",
			$"{Data.GlobalOptions.CharInfo.Width}x{Data.GlobalOptions.CharInfo.Height} pixels, ",
			$"{Data.GlobalOptions.CharInfo.ColoursPerChar} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Data.GlobalOptions.CharInfo.PixelDataSize} bytes");

		var firstChar = Data.CharIndexInRam(0);
		Logger.Debug.Option($"Characters base address: ${Data.ScreenOptions.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");

		if (Data.GlobalOptions.InfoImageRenderingScale > 0)
		{
			Logger.Debug.Option($"Info image scaled at {Data.GlobalOptions.InfoImageRenderingScale}x will be generated");
		}
	}

	#endregion
}
