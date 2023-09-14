using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Utils;
using System.CommandLine;
using System.CommandLine.Binding;

namespace M65Converter.Sources.Runners.Options;

/// <summary>
/// Manages global command line options.
/// </summary>
public class GlobalOptionsBinder : BaseOptionsBinder<GlobalOptions>
{
	/// <summary>
	/// Global options require different handling, hence we need global instance. See <see cref="BaseOptionsBinder{T}"/> for details.
	/// </summary>
	public static GlobalOptionsBinder Instance { get; private set; } = null!;

	#region Initialization & Disposal

	public GlobalOptionsBinder() : base(global: true)
	{
		Instance = this;
	}

	#endregion

	#region Command line

	private readonly Option<Logger.VerbosityType> Verbosity = new(
		aliases: new[] { "-v", "--verbosity" },
		description: "Verbosity level",
		getDefaultValue: () => Logger.Verbosity
	);

	private readonly Option<CharColourMode> colourMode = new(
		name: "--colour",
		description: "Colour mode",
		getDefaultValue: () => CharColourMode.FCM
	);

	private readonly Option<string> screenSize = new(
		name: "--screen",
		description: "Screen size measured in characters. Valid formats: \"<width> x <height>\", \"<width>\"",
		getDefaultValue: () => "40x25"
	);

	private readonly Option<string> screenBaseAddress = new(
		name: "--screen-address",
		description: "Base address where screen data will be loaded into on Mega 65",
		getDefaultValue: () => "$0800"
	);

	private readonly Option<string> charBaseAddress = new(
		name: "--chars-address",
		description: "Base address where characters will be loaded into on Mega 65",
		getDefaultValue: () => "$10000"
	);

	private readonly Option<int> infoImageScale = new(
		name: "--info",
		description: "Info image scale, 1 or greater to enable, 0 to disable. This is global scale/on/off switch, you still need to enable specific images generation - see options for individual commands. NOTE: info images generation can be quite slow!",
		getDefaultValue: () => 0
	);

	#endregion

	#region Overrides

	protected override Command OnCreateCommand()
	{
		return new RootCommand(description: "Converter for various mega 65 related files");
	}

	protected override void OnAssignOptions(GlobalOptions options, DataContainer data)
	{
		// Global options are handled differently, we assign them to data container in `BaseOptionsBinder` for each command. Under normal circumstances (aka some commands are passed to cmd line), this method is not invoked at all for global options.
	}

	protected override BaseRunner OnCreateRunner(GlobalOptions options, DataContainer data)
	{
		// Note this is only invoked if user provides no cmd line arguments.
		return new GlobalRunner
		{
			Data = data,
		};
	}

	protected override GlobalOptions GetBoundValue(BindingContext bindingContext)
	{
		// Verbosity is applied immediately.
		Logger.Verbosity = bindingContext.ParseResult.GetValueForOption(Verbosity);

		// Other global options are returned via global options class.
		return new GlobalOptions
		{
			ColourMode = bindingContext.ParseResult.GetValueForOption(colourMode),
			ScreenSize = bindingContext.ParseResult.GetValueForOption(screenSize)?.ParseAsSize() ?? new Size(40, 25),
			ScreenBaseAddress = bindingContext.ParseResult.GetValueForOption(screenBaseAddress)?.ParseAsInt() ?? 0x800,
			CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)?.ParseAsInt() ?? 0x10000,
			InfoImageRenderingScale = bindingContext.ParseResult.GetValueForOption(infoImageScale)
		};
	}

	#endregion

	#region Declarations

	public class GlobalRunner : BaseRunner
	{
		protected override void OnRun()
		{
			// Nothing to do...
		}
	}

	#endregion
}

#region Options

public class GlobalOptions
{
	/// <summary>
	/// The renderding scale for the info images. 0 to prevent rendering.
	/// </summary>
	public int InfoImageRenderingScale { get; init; }

	/// <summary>
	/// Base address where the screen data will be loaded into on Mega 65.
	/// </summary>
	public int ScreenBaseAddress { get; init; }

	/// <summary>
	/// Base address where the characters will be loaded into on Mega 65.
	/// </summary>
	public int CharsBaseAddress { get; init; }

	/// <summary>
	/// Screen size in terms of character columns and rows.
	/// </summary>
	public Size ScreenSize { get; init; }

	/// <summary>
	/// Colour mode to use for characters related data.
	/// </summary>
	public CharColourMode ColourMode { get; init; }

	/// <summary>
	/// Tile information, depends on <see cref="CharColour"/>.
	/// </summary>
	public CharInfo CharInfo
	{
		get
		{
			_charInfo ??= ColourMode switch
			{
				CharColourMode.FCM => new CharInfo
				{
					Width = 8,
					Height = 8,
					BytesPerWidth = 2,
					BytesPerCharData = 64,
					ColoursPerChar = 256
				},

				CharColourMode.NCM => new CharInfo
				{
					Width = 16,
					Height = 8,
					BytesPerWidth = 2,
					BytesPerCharData = 64,
					ColoursPerChar = 16
				},

				_ => throw new ArgumentException($"Unknown tile type {ColourMode}")
			};

			return _charInfo!;
		}
	}
	private CharInfo? _charInfo = null;
}

#endregion
