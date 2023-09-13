using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using System.CommandLine.Binding;
using System.CommandLine;

namespace M65Converter.Sources.Runners.Options;

/// <summary>
/// Manages command line options for screens runner.
/// </summary>
public class ScreensOptionsBinder : BaseOptionsBinder<ScreenOptions>
{
	#region Command line

	private readonly Argument<FileInfo[]> inputs = new(
		name: "--inputs",
		description: "One or more input files or folders"
	)
	{
		Arity = ArgumentArity.OneOrMore,
	};

	private readonly Option<FileInfo?> outputScreen = new(
		name: "--out-screen",
		description: "Path and filename of the generated screen output, relative to current folder. If missing, screen is not exported. Optional {name} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputColour = new(
		name: "--out-colour",
		description: "Path and filename of the generated colour data output, relative to current folder. If missing, colour data is not exported. Optional {name} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputLookup = new(
		name: "--out-lookup",
		description: "Path and filename of the generated lookup data output, relative to current folder. If missing, lookup data is not exported. Optional {name} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputInfo = new(
		name: "--out-info",
		description: "Path and filename of the generated info image output, relative to current folder. If missing (or global --info value is 0), image is not exported. Optional {name} template is replaced with level name for each input"
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

	private readonly Option<bool> rasterRewriteBuffer = new(
		aliases: new[] { "-r", "--rrb" },
		description: "Raster rewrite buffer support. If enabled, each layer is exported separately using RRB",
		getDefaultValue: () => false
	);

	#endregion

	#region Overrides

	protected override Command OnCreateCommand()
	{
		return new Command(
			name: "screens",
			description: "Converts simplified LDtk export or Aseprite file into screen data"
		);
	}

	protected override void OnAssignOptions(ScreenOptions options, DataContainer data)
	{
		data.ScreenOptions = options;
	}

	protected override BaseRunner OnCreateRunner(ScreenOptions options, DataContainer data)
	{
		return new ScreensRunner
		{
			Data = data
		};
	}

	protected override ScreenOptions GetBoundValue(BindingContext bindingContext)
	{
		return new ScreenOptions
		{
			Inputs = Providers(bindingContext.ParseResult.GetValueForArgument(inputs))!,
			OutputScreenTemplate = bindingContext.ParseResult.GetValueForOption(outputScreen),
			OutputColourTemplate = bindingContext.ParseResult.GetValueForOption(outputColour),
			OutputLookupTemplate = bindingContext.ParseResult.GetValueForOption(outputLookup),
			OutputInfoTemplate = bindingContext.ParseResult.GetValueForOption(outputInfo),
			ScreenSize = bindingContext.ParseResult.GetValueForOption(screenSize)?.ParseAsSize() ?? new Size(40, 25),
			ScreenBaseAddress = bindingContext.ParseResult.GetValueForOption(screenBaseAddress)?.ParseAsInt() ?? 0x800,
			CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)?.ParseAsInt() ?? 0x10000,
			IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer),
		};
	}

	#endregion
}

#region Options

public class ScreenOptions
{
	/// <summary>
	/// The array of all inputs.
	/// </summary>
	public IStreamProvider[] Inputs { get; init; } = null!;

	/// <summary>
	/// Optional output screen file and path template.
	/// </summary>
	public FileInfo? OutputScreenTemplate { get; init; }

	/// <summary>
	/// Optional output screen file and path template.
	/// </summary>
	public FileInfo? OutputColourTemplate { get; init; }

	/// <summary>
	/// Optional output lookup file and path template.
	/// </summary>
	public FileInfo? OutputLookupTemplate { get; init; }

	/// <summary>
	/// Optional output info image file and path template.
	/// </summary>
	public FileInfo? OutputInfoTemplate { get; init; }

	/// <summary>
	/// Specifies whether multiple layers should result in RRB output.
	/// 
	/// If RRB is enabled, then each layer will be treated as its own RRB layer. Otherwise all layers will be squashed into a single layer. The latter option works best when characters are fully opaque so the ones in top layers fully cover the ones below.
	/// </summary>
	public bool IsRasterRewriteBufferSupported { get; init; }

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
}

#endregion
