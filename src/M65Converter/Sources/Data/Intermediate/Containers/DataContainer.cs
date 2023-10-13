using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Helpers;
using M65Converter.Sources.Runners.Options;

namespace M65Converter.Sources.Data.Intermediate.Containers;

/// <summary>
/// Container for all parsed data.
/// 
/// Besides being a container, this class also validates and exports common data.
/// </summary>
public class DataContainer
{
	/// <summary>
	/// Global options.
	/// </summary>
	public GlobalOptions GlobalOptions { get; set; } = new();

	/// <summary>
	/// All the parsed characters.
	/// </summary>
	public ImagesContainer CharsContainer { get; } = new();

	/// <summary>
	/// All colours needed for all characters.
	/// </summary>
	public List<ColourData> Palette { get; set; } = new();

	/// <summary>
	/// Screen and colour data in a form suitable for exporting.
	/// </summary>
	public List<ScreenExportData> Screens { get; } = new();

	/// <summary>
	/// Sprite data in a form suitable for exporting.
	/// </summary>
	public List<SpriteExportData> Sprites { get; } = new();

	/// <summary>
	/// Contains all stream providers used for output.
	/// 
	/// Only valid after all data export is completed. This is mainly used for unit testing reasons.
	/// </summary>
	public OutputStreams UsedOutputStreams { get; private set; } = new();

	/// <summary>
	/// The register where all runners for this session are registered to.
	/// </summary>
	public RunnersRegister Runners { get; } = new();

	#region Subclass

	/// <summary>
	/// Prepares the output stream for the given screen using the given template.
	/// 
	/// A subclass that wants to change how all output streams are prepared can override this and customize as needed. Default implementation prepares file stream from the template returned from template picker.
	/// </summary>
	public virtual IStreamProvider? ScreenOutputStreamProvider(ScreenExportData data, Func<FileInfo?> templatePicker)
	{
		// If template is not provided, user doesn't want to export this data.
		var template = templatePicker();
		if (template == null) return null;

		var path = ScreenPathFromTemplate(template.FullName, data);

		return new FileStreamProvider
		{
			FileInfo = new FileInfo(path)
		};
	}

	/// <summary>
	/// Prepares the output stream for the given sprite using the given template.
	/// 
	/// A subclass that wants to change how all output streams are prepared can override this and customize as needed. Default implementation prepares file stream from the template returned from template picker.
	/// </summary>
	public virtual IStreamProvider? SpriteOutputStreamProvider(SpriteExportData data, Func<FileInfo?> templatePicker)
	{
		// If template is not provided, user doesn't want to export this data.
		var template = templatePicker();
		if (template == null) return null;

		var path = SpritePathFromTemplate(template.FullName, data);

		return new FileStreamProvider
		{
			FileInfo = new FileInfo(path)
		};
	}

	/// <summary>
	/// Converts the given template path into the actual one.
	/// 
	/// A subclass that only needs to change how templates are converted into proper paths, can override this and customize the path.
	/// </summary>
	protected virtual string ScreenPathFromTemplate(string template, ScreenExportData screen)
	{
		return template
			.Replace("{level}", screen.LevelName)
			.Replace("%level%", screen.LevelName);
	}

	protected virtual string SpritePathFromTemplate(string template,  SpriteExportData sprite)
	{
		return template
			.Replace("{name}", sprite.SpriteName)
			.Replace("%name%", sprite.SpriteName);
	}

	#endregion

	#region Data handling

	/// <summary>
	/// This is the entry point for invoking all commands and generating data for export.
	/// </summary>
	public void Run()
	{
		// Print dry run warning. If dry run is instructed, we already add a seprator, but otherwise we have to add one here.
		if (!PrintDryRunWarning())
		{
			Logger.Info.Separator();
		}

		// Clear all data before running. Just in case this gets called more than once.
		ClearData();

		// Before doing and running, ask all runners to to validate their position and adjust common runtime options.
		ExecuteStep(
			title: "Validating and adjusting run",
			handler: runner =>
			{
				runner.OnValidateRunPosition(Runners);
				runner.OnAdjustRunOptions();
			}
		);

		// Parse and then validate all inputs.
		ExecuteStep(
			title: "Parsing inputs",
			handler: runner =>
			{
				runner.OnDescribeStep();
				runner.OnParseInputs();
				runner.OnValidateParsedData();
			}
		);

		// After all runners parsed their data, we ask them to prepare export data.
		ExecuteStep(
			title: "Preparing export data",
			handler: runner =>
			{
				runner.OnPrepareExportData();
			}
		);

		// After all export data from all runners is generated, we give them a chance to post-process it.
		ExecuteStep(
			title: "Finalizing export data",
			handler: runner =>
			{
				runner.OnFinalizeExportData();
			}
		);

		// Otherwise, we can finally export (︶︶)
		ExecuteStep(
			title: "Validating and exporting data",
			handler: runner =>
			{
				runner.OnValidateExportData();

				if (!GlobalOptions.IsDryRun)
				{
					runner.OnExportData();
				}
			}
		);
	}

	private void ClearData()
	{
		CharsContainer.Clear();
		Palette.Clear();
		Screens.Clear();
		Sprites.Clear();
		UsedOutputStreams = new();
	}

	#endregion

	#region Public

	/// <summary>
	/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
	/// </summary>
	public int CharIndexInRam(int relativeIndex) => GlobalOptions.CharInfo.CharIndexInRam(GlobalOptions.CharsBaseAddress, relativeIndex);

	#endregion

	#region Helpers

	/// <summary>
	/// Executes generic step not involving any runner.
	/// </summary>
	private void ExecuteStep(string title, Action handler)
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = title,
			Header = TimeRunner.DoubleLineHeader,
			Footer = TimeRunner.DoubleLineFooter,
			LoggerFunction = Logger.Info.Message
		}
		.Run(() =>
		{
			handler();

			Logger.Debug.Separator();
		});
	}

	/// <summary>
	/// Executes a single "step" with all runners.
	/// </summary>
	private void ExecuteStep(string title, Action<BaseRunner> handler)
	{
		ExecuteStep(title, () =>
		{
			Runners.Enumerate(runner =>
			{
				new TimeRunner
				{
					Title = runner.Title(),
					LoggerFunction = Logger.Debug.Message
				}
				.Run(() =>
				{
					handler(runner);
				});
			});
		});
	}

	/// <summary>
	/// Prints a warning about dry run if needed.
	/// 
	/// As convenience, method returns true if dry run is instructed, false otherwise.
	/// </summary>
	private bool PrintDryRunWarning()
	{
		if (GlobalOptions.IsDryRun)
		{
			Logger.Info.Box(
				"WARNING",
				string.Empty,
				"Dry run only, no output will be generated!"
			);
		}

		return GlobalOptions.IsDryRun;
	}

	#endregion

	#region Declarations

	public class OutputStreams
	{
		public IStreamProvider? CharsStream { get; set; }
		public IStreamProvider? PaletteStream { get; set; }

		public List<IStreamProvider> ScreenScreenDataStreams { get; } = new();
		public List<IStreamProvider> ScreenColourDataStreams { get; } = new();
		public List<IStreamProvider> ScreenLookupDataStreams { get; } = new();
		public List<IStreamProvider> ScreenInfoImageStreams { get; } = new();

		public List<IStreamProvider> SpriteFramesDataStreams { get; } = new();
		public List<IStreamProvider> SpriteLookupDataStreams { get; } = new();
		public List<IStreamProvider> SpriteInfoImagesStreams { get; } = new();
	}

	#endregion
}
