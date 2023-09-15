using M65Converter.Runners;
using M65Converter.Sources.Data.Providers;
using System.CommandLine.Binding;
using System.CommandLine;
using M65Converter.Sources.Data.Intermediate.Containers;

namespace M65Converter.Sources.Runners.Options;

/// <summary>
/// Manages command line options for screens runner.
/// </summary>
public class ScreensOptionsBinder : BaseOptionsBinder<ScreenOptions>
{
	#region Command line

	private readonly Argument<FileInfo[]> inputs = new(
		name: "inputs",
		description: "One or more input files or folders"
	)
	{
		Arity = ArgumentArity.OneOrMore,
	};

	private readonly Option<FileInfo?> outputScreen = new(
		name: "--out-screen",
		description: "Path and filename of the generated screen output, relative to current folder. If missing, screen is not exported. Optional {level} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputColour = new(
		name: "--out-colour",
		description: "Path and filename of the generated colour data output, relative to current folder. If missing, colour data is not exported. Optional {level} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputLookup = new(
		name: "--out-lookup",
		description: "Path and filename of the generated lookup data output, relative to current folder. If missing, lookup data is not exported. Optional {level} template is replaced with level name for each input"
	);

	private readonly Option<FileInfo?> outputInfo = new(
		name: "--out-info",
		description: "Path and filename of the generated info image output, relative to current folder. If missing (or global --info value is 0), image is not exported. Optional {level} template is replaced with level name for each input"
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

	protected override ScreenOptions OnCreateOptions(BindingContext bindingContext, DataContainer data)
	{
		return new ScreenOptions
		{
			Inputs = Providers(bindingContext.ParseResult.GetValueForArgument(inputs))!,
			OutputScreenTemplate = bindingContext.ParseResult.GetValueForOption(outputScreen),
			OutputColourTemplate = bindingContext.ParseResult.GetValueForOption(outputColour),
			OutputLookupTemplate = bindingContext.ParseResult.GetValueForOption(outputLookup),
			OutputInfoTemplate = bindingContext.ParseResult.GetValueForOption(outputInfo),
			IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer),
		};
	}

	protected override BaseRunner OnCreateRunner(ScreenOptions options, DataContainer data)
	{
		return new ScreensRunner
		{
			Data = data,
			Options = options
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
}

#endregion
