using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners;

using System.CommandLine;
using System.CommandLine.Binding;

namespace M65Converter.Sources.Helpers.Inputs;

/// <summary>
/// Manages command line options for chars runner.
/// </summary>
public class CharsOptionsBinder : BaseOptionsBinder<CharOptions>
{
	#region Command line

	private readonly Argument<FileInfo[]> inputs = new(
		name: "--inputs",
		description: "Zero or more input files with base characters"
	)
	{
		Arity = ArgumentArity.ZeroOrMore
	};

	private readonly Option<FileInfo?> outputChars = new(
		name: "--out-chars",
		description: "Path and filename of the generated characters output, relative to current folder. If missing, chars are not exported"
	);

	private readonly Option<FileInfo?> outputPalette = new(
		name: "--out-palette",
		description: "Path and filename of the generated palette output, relative to current folder. If missing, palette is not exported"
	);

	#endregion

	#region Overrides

	protected override Command OnCreateCommand()
	{
		return new Command(
			name: "chars",
			description: "Sets up options for characters and optionally parses base characters set from inputs"
		);
	}

	protected override void OnAssignOptions(CharOptions options, DataContainer data)
	{
		data.CharOptions = options;
	}

	protected override BaseRunner OnCreateRunner(CharOptions options, DataContainer data)
	{
		return new CharsRunner
		{
			Data = data
		};
	}

	protected override CharOptions GetBoundValue(BindingContext bindingContext)
	{
		return new CharOptions
		{
			Inputs = Providers(bindingContext.ParseResult.GetValueForArgument(inputs)),
			OutputCharsStream = Provider(bindingContext.ParseResult.GetValueForOption(outputChars)),
			OutputPaletteStream = Provider(bindingContext.ParseResult.GetValueForOption(outputPalette))
		};
	}

	#endregion
}

#region Options

public class CharOptions
{
	/// <summary>
	/// Optional images with base characters.
	/// 
	/// Useful when characters order is important and should be preserved. For example to predictably setup digits and letters or animate individual charcters. If this file is provided, it will be split into characters as is, including all fully transparent tiles. These characters will be generated first, then all additional characters required to render individual layers will be added afterwards (this is mainly needed where semi-transparent characters are present in layers and layers need to be merged).
	/// 
	/// If this image is not provided, then all characters will be auto-generated from source images. This is the simplest way of using the converter, however characters order may vary between different calls, according to changes in source images.
	/// </summary>
	public IStreamProvider[]? Inputs { get; init; }

	/// <summary>
	/// Optional output characters stream provider. If null, characters are not exported.
	/// </summary>
	public IStreamProvider? OutputCharsStream { get; init; }

	/// <summary>
	/// Optional output palette stream provider. If null, palette is not exported.
	/// </summary>
	public IStreamProvider? OutputPaletteStream { get; init; }
}

#endregion
