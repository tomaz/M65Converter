using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;

using System.CommandLine;
using System.CommandLine.Binding;

namespace M65Converter.Sources.Runners.Options;

/// <summary>
/// Manages command line options for RRB sprites runner.
/// </summary>
public class RRBSpritesOptionsBinder : BaseOptionsBinder<RRBSpritesOptions>
{
	#region Command line

	private readonly Argument<FileInfo[]> inputs = new(
		name: "inputs",
		description: "One or more input files, each file defines a set of sprite frames. See also --frame-size."
	)
	{
		Arity = ArgumentArity.OneOrMore,
	};

	private readonly Option<FileInfo?> outputSprite = new(
		name: "--out-frames",
		description: "Path and filename of the generated frames data output, relative to current folder. If missing, frames data is not exported. Optional {name} template is replaced with input file name. Typically each input corresponds to one sprite animation and is composed of 1 or more frames"
	);

	private readonly Option<FileInfo?> outputLookup = new(
		name: "--out-lookup",
		description: "Path and filename of the generated frames lookup data output, relative to current folder. If missing, lookup data is not exported. Optional {name} template is replaced with input file name."
	);

	private readonly Option<FileInfo?> outputInfoImage = new(
		name: "--out-info",
		description: "Path and filename of the info image output, relative to current folder. All sprites parsed with this command will be added to this image. If missing, info image is not exported."
	);

	private readonly Option<string[]?> positions = new(
		name: "--position",
		description: "One or more sprite positions in the form of \"X,Y\" (no spacing!). Multiple positions can be provided, separated by a space, or through multiple --position options. In this case positions are taken in the given order for each sprite in the list. The last position is then repeated for all subsequent sprites if there are more than positions. Only applicable if --append-screens is enabled"
	)
	{
		AllowMultipleArgumentsPerToken = true,
	};

	private readonly Option<string?> frameSize = new(
		name: "--frame-size",
		description: Tools.MultilineString
		(
			"Optional size of frame in the form of \"<w>x<h>\" (no spacing!). Usage:",
			"- Aseprite input: ignored, each source frame is taken as a single output frame in its full size",
			"- Other inputs: source image is split by the given widht and height into frames",
			"- Only full sized frames are taken, right and bottom edges are ignored if not enough pixels",
			"- Top-to-bottom, left-to-right order is used for frames"
		)
	);

	private readonly Option<bool> appendScreen = new(
		name: "--append-screens",
		description: "Enables or disables sprite data being appended to the end of screen layers, for all sprites parsed with this command. Useful when frames for a single sprite are declared in multiple files (idle animation, running animation etc); we typically only want to have one sprite layer added and then replace characters based on runtime changes",
		getDefaultValue: () => false
	);

	#endregion

	#region Overrides

	protected override Command OnCreateCommand()
	{
		return new Command(
			name: "rrbsprites",
			description: "Converts image files into RRB sprites data. Can be used multiple times in single cmd line. Must be provided AFTER `screens` if used together!"
		);
	}

	protected override RRBSpritesOptions OnCreateOptions(BindingContext bindingContext, DataContainer data)
	{
		return new RRBSpritesOptions
		{
			Inputs = Providers(bindingContext.ParseResult.GetValueForArgument(inputs))!,
			OutputFramesTemplate = bindingContext.ParseResult.GetValueForOption(outputSprite),
			OutputLookupTemplate = bindingContext.ParseResult.GetValueForOption(outputLookup),
			OutputInfoImageStream = Provider(bindingContext.ParseResult.GetValueForOption(outputInfoImage)),
			SpritePositions = Positions(bindingContext.ParseResult.GetValueForOption(positions)),
			FrameSize = bindingContext.ParseResult.GetValueForOption(frameSize)?.ParseAsSize(),
			IsAppendingToScreenDataEnabled = bindingContext.ParseResult.GetValueForOption(appendScreen),
		};
	}

	protected override BaseRunner OnCreateRunner(RRBSpritesOptions options, DataContainer data)
	{
		return new RRBSpritesRunner
		{
			Data = data,
			Options = options,
		};
	}

	#endregion

	#region Helpers

	private Point[] Positions(string[]? positions)
	{
		if (positions == null) return Array.Empty<Point>();

		var result = new List<Point>();

		foreach (var position in positions)
		{
			result.Add(position.ParseAsPoint());
		}

		return result.ToArray();
	}

	#endregion
}

#region Options

public class RRBSpritesOptions
{
	/// <summary>
	/// The array of all inputs.
	/// </summary>
	public IStreamProvider[] Inputs { get; init; } = null!;

	/// <summary>
	/// Optional output frames file and path template.
	/// </summary>
	public FileInfo? OutputFramesTemplate { get; init; }

	/// <summary>
	/// Optional output lookup table file and path template.
	/// </summary>
	public FileInfo? OutputLookupTemplate { get; init; }

	/// <summary>
	/// Optional output info image file and path.
	/// </summary>
	public IStreamProvider? OutputInfoImageStream { get; init; }

	/// <summary>
	/// The position for all sprites.
	/// 
	/// Null is default position at top right of the screen, just outside visible area.
	/// 
	/// Only applicable if <see cref="IsAppendingToScreenDataEnabled"/> is true.
	/// </summary>
	public Point[] SpritePositions { get; init; } = Array.Empty<Point>();

	/// <summary>
	/// Optional frame size.
	/// 
	/// Note: usage for this value depends on the source image type and may be ignored even if provided.
	/// </summary>
	public Size? FrameSize { get; init; }

	/// <summary>
	/// Specifies whether sprite data should be appended to the end of the screen data.
	/// 
	/// User might want to disable screen data appending if they intend to add sprites manually on the fly.
	/// 
	/// Note: sprite characters are always added to characters though, regardless of whether appending screen data is enabled or disabled.
	/// </summary>
	public bool IsAppendingToScreenDataEnabled { get; init; } = false;
}

#endregion
