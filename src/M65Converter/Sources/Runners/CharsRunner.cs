using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Exporting;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Helpers;
using M65Converter.Sources.Runners.Options;

namespace M65Converter.Sources.Runners;

public class CharsRunner : BaseRunner
{
	public CharOptions Options { get; init; } = null!;

	#region Overrides

	public override string? Title() => "Characters";

	public override void OnValidateRunPosition(RunnersRegister runners)
	{
		// We allow single instance of characters runner. Since all characters are registered into a single chars container, allowing multiple char runners may result in multiple sets of character outputs generated (if multiple chars commands would specify output to different files for example). If different sets of chars are needed, user should run the converter multiple times.
		runners.ValidateMaxInstances(this, 1, count => 
			Tools.MultilineString(
				$"Only 1 chars command is supported, {count} found",
				"If multiple sets of characters are needed, run M65Converter multiple times"
			)
		);

		// Characters runner MUST be the first runner in the list! This is where we merge palette from all characters which is the prerequisite for generating export data.
		runners.ValidatePosition(this, 0, position =>
			Tools.MultilineString(
				$"Chars must be the first command of M65Converter run, found it at {position}"
			)
		);
	}

	public override void OnDescribeStep()
	{
		var firstChar = Data.CharIndexInRam(0);
		Logger.Debug.Message($"Character mode: {Data.GlobalOptions.ColourMode} ({Data.GlobalOptions.CharInfo})");
		Logger.Debug.Message($"Characters base address: ${Data.GlobalOptions.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");

		if (Options.Inputs?.Length <= 0)
		{
			Logger.Debug.Message("No base character images provided, characters will be generated from screen and sprite sources");
		}

		if (Options.OutputCharsStream == null)
		{
			Logger.Debug.Message("--out-chars not provided, this may result in undefined data displayed on screen");
		}

		if (Options.OutputPaletteStream == null)
		{
			Logger.Debug.Message("--out-palette not provided, colours may be out of sync for generated characters");
		}
	}

	public override void OnParseInputs()
	{
		if (Options.Inputs?.Length == 0) return;

		// Parse all inputs.
		new InputFilesHandler
		{
			Sources = Options.Inputs,
		}
		.Run((index, input) =>
		{
			// Load the image.
			var image = Image.Load<Argb32>(input.GetStream(FileMode.Open));

			// For base characters we keep all transparents to achieve consistent results. With these characters it's responsibility of the creator to trim source image. Same for duplicates, we want to leave all characters to preserve positions, however when matching them on layers, it will always take the first match.
			var result = new ImageSplitter
			{
				ItemWidth = Data.GlobalOptions.CharInfo.Width,
				ItemHeight = Data.GlobalOptions.CharInfo.Height,
				TransparencyOptions = TransparencyOptions.KeepAll,
				DuplicatesOptions = DuplicatesOptions.KeepAll
			}
			.Split(image, Data.CharsContainer);

			// Note: we ignore indexed image for base characters. We only need actual layers from LDtk.
			Logger.Debug.Message($"Found {result.ParsedCount}, added {result.AddedCount} characters");
		});
	}

	public override void OnPrepareExportData()
	{
		// IMPORTANT: it's crucial to run this first since merged palette is the prerequisite for preparing all other data!
		var options = new PaletteMerger.OptionsType
		{
			Images = Data.CharsContainer.Images,
			SamePaletteBankImages = Data.CharsContainer.SamePaletteBankImages,
			Is4Bit = Data.GlobalOptions.ColourMode == CharColourMode.NCM,
			IsUsingTransparency = true,
		};

		// Note: merging not only prepares the final palette for export, but also remaps all character images colours to point to this generated palette.
		Data.Palette = PaletteMerger
			.Create(options)
			.Merge();
	}

	public override void OnValidateExportData()
	{
		if (Data.CharsContainer.Images.Count > 8192)
		{
			throw new InvalidDataException("Too many characters to fit 2 bytes, adjust source files");
		}

		if (Data.Palette.Count > 256)
		{
			throw new InvalidDataException("Too many colours in the palette, adjust source files");
		}
	}

	public override void OnExportData()
	{
		ExportChars();
		ExportPalette();
	}

	#endregion

	#region Helpers

	private void ExportChars()
	{
		Data.UsedOutputStreams.CharsStream = Options.OutputCharsStream;

		if (Options.OutputCharsStream == null) return;

		CreateExporter("chars", Options.OutputCharsStream).Export(writer =>
		{
			new CharsExporter
			{
				Data = Data,
				Chars = Data.CharsContainer
			}
			.Export(writer);
		});
	}

	private void ExportPalette()
	{
		Data.UsedOutputStreams.PaletteStream = Options.OutputPaletteStream;

		if (Options.OutputPaletteStream == null) return;

		CreateExporter("palette", Options.OutputPaletteStream).Export(writer =>
		{
			new PaletteExporter
			{
				Data = Data,
				Palette = Data.Palette.Select(x => x.Colour).ToList(),
			}
			.Export(writer);
		});
	}

	#endregion
}
