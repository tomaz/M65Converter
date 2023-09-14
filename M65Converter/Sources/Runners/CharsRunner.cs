using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Runners;

public class CharsRunner : BaseRunner
{
	#region Overrides

	protected override string? Title() => "Parsing characters";

	protected override void OnRun()
	{
		LogCmdLineDataOptions();

		ParseInputs();
	}

	#endregion

	#region Parsing

	private void ParseInputs()
	{
		if (Data.CharOptions?.Inputs?.Length == 0) return;

		// Parse all inputs.
		new InputFilesHandler
		{
			TitlePrefix = "Parsing base chars from",
			Sources = Data.CharOptions?.Inputs,
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
				TransparencyOptions = TransparencyOptionsType.KeepAll,
				DuplicatesOptions = DuplicatesOptionsType.KeepAll
			}
			.Split(image, Data.CharsContainer);

			// Note: we ignore indexed image for base characters. We only need actual layers from LDtk.
			Logger.Debug.Message($"Found {result.ParsedCount}, added {result.AddedCount} characters");
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

		if (Data.CharOptions?.Inputs?.Length > 0)
		{
			Logger.Debug.Option($"Base characters will be generated from:");
			foreach (var input in Data.CharOptions.Inputs)
			{
				Logger.Debug.SubOption(input.GetFilename());
			}

			Logger.Debug.Option("Additional characters will be generated from layer images");
		}
		else
		{
			Logger.Debug.Option("No base character images provided");
			Logger.Debug.Option("Characters will be generated from layer images");
		}
	}
		#endregion
	}
