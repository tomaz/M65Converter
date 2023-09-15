using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Options;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses LDtk or Aseprite background screen and colour data into mega 65 compatible files.
/// </summary>
public class ScreensRunner : BaseRunner
{
	public ScreenOptions Options { get; init; } = null!;

	private List<Level> MergedLevels { get; } = new();
	private static bool IsScreensExported { get; set; } = false;

	#region Overrides

	public override string? Title() => "Screen and colour data";

	public override void OnDescribeStep()
	{
		if (Options.IsRasterRewriteBufferSupported)
		{
			Logger.Debug.Message("Individual layers of each input will be exported as RRB");
		}
		else
		{
			Logger.Debug.Message("Layers of each input will be merged");
		}

		if (Data.GlobalOptions.InfoImageRenderingScale > 0 && Options.OutputInfoTemplate != null)
		{
			Logger.Debug.Message($"Info image scaled at {Data.GlobalOptions.InfoImageRenderingScale}x will be generated");
		}

		if (Options.OutputScreenTemplate == null)
		{
			Logger.Debug.Message("--out-screen not provided, no screen output will be generated");
		}

		if (Options.OutputColourTemplate == null)
		{
			Logger.Debug.Message("--out-colour not provided, no colour output will be generated");
		}
	}

	public override void OnParseInputs()
	{
		// Parse all inputs.
		new InputFilesHandler
		{
			Sources = Options.Inputs
		}
		.Run((index, input) =>
		{
			Level? mergedLevel = null;

			try
			{
				// First attempt to use parsed composite image. This respects layer transparency and blending modes, so yields more accurate results. However it can easily overflow the palette. If this fails, we'll fall-down to manual layer merging in catch below.
				// Note: only certain types of inputs support composite images. If that's not supported for current input, layer merging will be used here as well.
				// Note: we set restore point before each additinal parsing. This way we will always revert to last valid data if first attempt fails.
				Data.CharsContainer.SetRestorePoint();
				mergedLevel = ParseInputAndMergeLayers(input, isCompositeImageAllowed: true);
				AppendExtraCharsFromLayers(mergedLevel);
			}
			catch (Exception e)
			{
				// Composite image failed, let's fall down to manual layer merging. This works perfectly fine when layers don't use transparency or blending modes, but can
				Logger.Info.Box(e, "WARNING",
					$"Most common reasons are layer transparency or blending mode",
					$"Attemting to manually merge layers (potentially less accurate output)"
				);

				// After failed first attempt we restore characters data and retry, this time manually merging layers.
				Data.CharsContainer.ResetDataToRestorePoint();
				mergedLevel = ParseInputAndMergeLayers(input, isCompositeImageAllowed: false);
				AppendExtraCharsFromLayers(mergedLevel);
			}

			// If all went well, we should add the level to the temporary list.
			MergedLevels.Add(mergedLevel);
		});
	}

	public override void OnValidateParsedData()
	{
		int charsAddress = Data.GlobalOptions.CharsBaseAddress;
		int charSize = Data.GlobalOptions.CharInfo.BytesPerCharData;
		if ((charsAddress % charSize) != 0)
		{
			var prev = (charsAddress / charSize) * charSize;
			var next = prev + charSize;

			throw new InvalidDataException(
				Tools.MultilineString(
					$"Char base address must start on {charSize} byte boundary.",
					$"Consider changing to previous (${prev:X}) or next (${next:X})"
				)
			);
		}
	}

	public override void OnPrepareExportData()
	{
		// If we are asked to prepare export data, we can assume screens are not exported yet. This flag is used to only export once since all screens from all commands are registered into a single list. We have to communicate this accross all screen runners, hence we use static property which we need to reset.
		IsScreensExported = false;

		// After all levels are ready, we should prepare screen and colour data for each merged level.
		foreach (var mergedLevel in MergedLevels)
		{
			Logger.Debug.Separator();
			Logger.Debug.Message($"Preparing data for {mergedLevel.LevelName}");

			// Prepare the data for export.
			var screenData = PrepareExportData(mergedLevel);

			// If all went well (aka no exception thrown), add screen data to global results for later export. We don't export just yet since we may have additional steps that will append data later on.
			Data.Screens.Add(screenData);
		}

		Logger.Debug.Separator();
	}

	public override void OnExportData()
	{
		// All screens from all commands are registered into a single array, so we only need to export once.
		if (IsScreensExported) return;
		IsScreensExported = true;

		foreach (var screen in Data.Screens)
		{
			ExportScreenColour(screen);
			ExportScreenData(screen);
			ExportLookupTable(screen);
			ExportInfoImage(screen);
		}

		// Add an empty line below last individual export timing from the loop above and overall screen runner export timing.
		Logger.Debug.Separator();
	}

	#endregion

	#region Parsing

	/// <summary>
	/// <summary>
	/// Merges all layers from the given input.
	/// </summary>
	private Level ParseInputAndMergeLayers(IStreamProvider input, bool isCompositeImageAllowed)
	{
		// Parse input data.
		var inputLayers = Level.Parse(input);

		// Prepare all layers we need to extract chars from.
		var options = new LayerMerger.OptionsType
		{
			IsRasterRewriteBufferSupported = Options.IsRasterRewriteBufferSupported,
			IsCompositeImageAllowed = isCompositeImageAllowed,
		};

		return LayerMerger
			.Create(options)
			.Merge(inputLayers);
	}

	/// <summary>
	/// Appends all extra characters needed for rendering the given merged layers.
	/// </summary>
	private void AppendExtraCharsFromLayers(Level mergedLayers)
	{
		foreach (var layer in mergedLayers.Layers)
		{
			// We only need 1 fully transparent character. If we don't yet have it from base chars, this is where we'll add it. We could call this function outside the loop, but this way we get more meaningful log.
			AppendTransparentChar();

			Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Name)}");

			// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
			var result = new ImageSplitter
			{
				ItemWidth = Data.GlobalOptions.CharInfo.Width,
				ItemHeight = Data.GlobalOptions.CharInfo.Height,
				TransparencyOptions = TransparencyOptions.OpaqueOnly,
				DuplicatesOptions = DuplicatesOptions.UniqueOnly
			}
			.Split(layer.Image, Data.CharsContainer);

			// Assign indexed image to layer.
			layer.IndexedImage = result.IndexedImage;

			Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
		}

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{Data.CharsContainer.Images.Count} total characters found");
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

	#endregion

	#region Converting

	/// <summary>
	/// Converts layers data into format suitable for exporting.
	/// </summary>
	private ScreenExportData PrepareExportData(Level mergedLayers)
	{
		var result = new ScreenExportData();

		var screen = new ScreenExportData.Layer();
		var colour = new ScreenExportData.Layer();

		string? layerName = null;   // by keeping this variable outside loop we can use it in all helper methods
		ScreenExportData.Column.DataType dataType;

		void AddScreenBytes(ScreenExportData.Row row, int index, ImageData data)
		{
			var charAddress = Data.CharIndexInRam(index);

			row.AddScreenDataColumn(
				charIndex: index,
				charAddressInRAM: charAddress,
				tag: layerName,
				type: dataType
			);
		}

		void AddColourBytes(ScreenExportData.Row row, int index, ImageData data)
		{
			row.AddColourDataColumn(
				colourMode: Data.GlobalOptions.ColourMode,
				paletteBank: data.IndexedImage.Bank,
				tag: layerName,
				type: dataType
			);
		}

		void AddScreenDelimiterBytes(ScreenExportData.Row row)
		{
			row.AddScreenAttributesColumn();
		}

		void AddColourDelimiterBytes(ScreenExportData.Row row)
		{
			row.AddColourAttributesColumn();
		}

		for (var i = 0; i < mergedLayers.Layers.Count; i++)
		{
			var layer = mergedLayers.Layers[i];

			// Setup layer name and data type - the first column we'll add is marked as "first data" for later handling.
			layerName = layer.Name;
			dataType = ScreenExportData.Column.DataType.FirstData;

			// First layer name is assigned to our one-and-only result layer, for both, screen and colour data.
			if (i == 0)
			{
				screen.Name = layerName;
				colour.Name = layerName;
			}

			// Adjust width and height of exported layers.
			if (layer.IndexedImage.Width > result.LayerWidth) result.LayerWidth = layer.IndexedImage.Width;
			if (layer.IndexedImage.Height > result.LayerHeight) result.LayerHeight = layer.IndexedImage.Height;

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
					dataType = ScreenExportData.Column.DataType.Data;
					layerName = null;
				}
			}
		}

		// Store the data.
		result.LevelName = mergedLayers.LevelName;
		result.RootFolder = mergedLayers.RootFolder;
		result.Screen = screen;
		result.Colour = colour;

		Logger.Debug.Message($"{mergedLayers.Layers.Count} source layers");
		Logger.Debug.Message($"{result.Screen.Width * Data.GlobalOptions.CharInfo.BytesPerCharIndex}x{result.Screen.Height} screen & colour data size");

		return result;
	}

	#endregion

	#region Exporting

	private void ExportScreenColour(ScreenExportData screen)
	{
		var streamProvider = Data.ScreenOutputStreamProvider(screen, () => Options.OutputColourTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.ScreenColourDataStreams.Add(streamProvider);

		CreateExporter("colour ram", streamProvider).Export(writer =>
		{
			new ScreenColoursExporter
			{
				Data = Data,
				Screen = screen
			}
			.Export(writer);
		});
	}

	private void ExportScreenData(ScreenExportData screen)
	{
		var streamProvider = Data.ScreenOutputStreamProvider(screen, () => Options.OutputScreenTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.ScreenScreenDataStreams.Add(streamProvider);

		CreateExporter("screen data", streamProvider).Export(writer =>
		{
			new ScreenDataExporter
			{
				Data = Data,
				Screen = screen
			}
			.Export(writer);
		});
	}

	private void ExportLookupTable(ScreenExportData screen)
	{
		var streamProvider = Data.ScreenOutputStreamProvider(screen, () => Options.OutputLookupTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.ScreenLookupDataStreams.Add(streamProvider);

		CreateExporter("layer info", streamProvider).Export(writer =>
		{
			new ScreenLookupExporter
			{
				Data = Data,
				Screen = screen
			}
			.Export(writer);
		});
	}

	private void ExportInfoImage(ScreenExportData screen)
	{
		if (Data.GlobalOptions.InfoImageRenderingScale <= 0) return;

		var streamProvider = Data.ScreenOutputStreamProvider(screen, () => Options.OutputInfoTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.ScreenInfoImageStreams.Add(streamProvider);

		CreateExporter("info image", streamProvider).Prepare(stream =>
		{
			new ScreenInfoImageExporter
			{
				Data = Data,
				Screen = screen
			}
			.Export(stream);
		});
	}

	#endregion
}
