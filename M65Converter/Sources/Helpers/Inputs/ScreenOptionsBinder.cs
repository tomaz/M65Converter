using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners;
using System.CommandLine.Binding;
using System.CommandLine;
using M65Converter.Sources.Data.Models;

namespace M65Converter.Sources.Helpers.Inputs;

/// <summary>
/// Manages command line options for screens runner.
/// </summary>
public class ScreensOptionsBinder : BaseOptionsBinder<ScreenOptionsType>
{
	#region Command line

	private Argument<FileInfo[]> inputs = new(
		name: "input",
		description: "One or more input files or folders"
	)
	{
		Arity = ArgumentArity.OneOrMore
	};

	private Option<FileInfo?> baseCharsImage = new(
		aliases: new[] { "-c", "--chars" },
		description: "Optional base characters image"
	);

	private Option<ScreenOptionsType.CharColourType> tileType = new(
		aliases: new[] { "-m", "--colour" },
		description: "Colour mode",
		getDefaultValue: () => ScreenOptionsType.CharColourType.FCM
	);

	private Option<string> charBaseAddress = new(
		aliases: new[] { "-a", "--chars-address" },
		description: "Base address where characters will be loaded into on Mega 65",
		getDefaultValue: () => "$10000"
	);

	private Option<bool> rasterRewriteBuffer = new(
		aliases: new[] { "-r", "--rrb" },
		description: "Raster rewrite buffer support. If enabled, each layer is exported separately using RRB",
		getDefaultValue: () => false
	);

	private Option<int> imageInfoScale = new(
		name: "--info",
		description: "Info image scale (1 or greater to enable, 0 to disable). NOTE: this can be quite slow!",
		getDefaultValue: () => 0
	);

	#endregion

	#region Overrides

	protected override Command OnCreateCommand()
	{
		return new Command(
			name: "chars",
			description: "Converts simplified LDtk export or Aseprite file into screen data"
		);
	}

	protected override void OnAssignOptions(ScreenOptionsType options, DataContainer data)
	{
		data.ScreenOptions = options;
	}

	protected override BaseRunner OnCreateRunner(ScreenOptionsType options, DataContainer data)
	{
		return new ScreensRunner
		{
			Data = data
		};
	}

	protected override ScreenOptionsType GetBoundValue(BindingContext bindingContext)
	{
		IStreamProvider? Provider(FileInfo? info)
		{
			return info != null
				? (IStreamProvider)new FileStreamProvider { FileInfo = info }
				: null;
		}

		ScreenOptionsType.InputOutput CreateStreamProviders(FileInfo input)
		{
			IStreamProvider OutputProvider(string filename)
			{
				var isFolder = (input.Attributes & FileAttributes.Directory) != 0;

				// Prepare folder name. This is either the input if it's a folder, or given input's parent folder.
				var folder = isFolder
					? input.FullName
					: Path.GetDirectoryName(input.FullName);

				// Prepare path without filename. If we have a folder, then that's that, otherwise we add a subfolder with the input name into which we'll create the outputs.
				var path = isFolder
					? folder
					: Path.Combine(folder!, Path.GetFileNameWithoutExtension(input.FullName));

				// Prepare stream provider for a file with the given name inside parent folder.
				return Provider(new FileInfo(Path.Combine(path!, filename)))!;
			}

			return new ScreenOptionsType.InputOutput
			{
				Input = new FileStreamProvider { FileInfo = input },
				OutputCharsStream = OutputProvider("chars.bin"),
				OutputPaletteStream = OutputProvider("chars.pal"),
				OutputScreenStream = OutputProvider("screen.bin"),
				OutputColourStream = OutputProvider("colour.bin"),
				OutputInfoDataStream = OutputProvider("screen.inf"),
				OutputInfoImageStream = OutputProvider("info.png")
			};
		}

		return new ScreenOptionsType
		{
			InputsOutputs = bindingContext.ParseResult.GetValueForArgument(inputs).Select(CreateStreamProviders).ToArray(),
			BaseCharsImage = Provider(bindingContext.ParseResult.GetValueForOption(baseCharsImage)),
			CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)!.ParseAsInt(),
			CharColour = bindingContext.ParseResult.GetValueForOption(tileType),
			IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer),
			InfoRenderingScale = bindingContext.ParseResult.GetValueForOption(imageInfoScale)
		};
	}

	#endregion
}

#region Options

public class ScreenOptionsType
{
	/// <summary>
	/// The array of all inputs and output.
	/// </summary>
	public InputOutput[] InputsOutputs { get; init; } = null!;

	/// <summary>
	/// Optional external characters image.
	/// 
	/// Useful when characters order is important and should be preserved. For example to predictably setup digits and letters or animate individual charcters. If this file is provided, it will be split into characters as is, including all fully transparent tiles. These characters will be generated first, then all additional characters required to render individual layers will be added afterwards (this is mainly needed where semi-transparent characters are present in layers and layers need to be merged).
	/// 
	/// If this image is not provided, then all characters will be auto-generated from source images. This is the simplest way of using the converter, however characters order may vary between different calls, according to changes in source images.
	/// </summary>
	public IStreamProvider? BaseCharsImage { get; set; }

	/// <summary>
	/// Specifies whether multiple layers should result in RRB output.
	/// 
	/// If RRB is enabled, then each layer will be treated as its own RRB layer. Otherwise all layers will be squashed into a single layer. The latter option works best when characters are fully opaque so the ones in top layers fully cover the ones below.
	/// </summary>
	public bool IsRasterRewriteBufferSupported { get; set; }

	/// <summary>
	/// Base address where the characters will be loaded into on Mega 65.
	/// </summary>
	public int CharsBaseAddress { get; set; }

	/// <summary>
	/// Specifies the scale at which info image should be rendered. If less than or equal to 0, info image is not generated.
	/// </summary>
	public int InfoRenderingScale { get; set; }

	/// <summary>
	/// Tile type. Changing this value will change <see cref="CharData"/> as well.
	/// </summary>
	public CharColourType CharColour
	{
		get => _charColour;
		set
		{
			_charColour = value;
			_charInfo = null;
		}
	}
	private CharColourType _charColour;

	/// <summary>
	/// Tile information, depends on <see cref="CharColour"/>.
	/// </summary>
	public CharInfo CharData
	{
		get
		{
			_charInfo ??= CharColour switch
			{
				CharColourType.FCM => new CharInfo
				{
					Width = 8,
					Height = 8,
					PixelDataSize = 2,
					CharDataSize = 64,
					ColoursPerChar = 256,
					CharsPerScreenWidth80Columns = 80,
				},

				CharColourType.NCM => new CharInfo
				{
					Width = 16,
					Height = 8,
					PixelDataSize = 2,
					CharDataSize = 64,
					ColoursPerChar = 16,
					CharsPerScreenWidth80Columns = 40,
				},

				_ => throw new ArgumentException($"Unknown tile type {CharColour}")
			};

			return _charInfo!;
		}
	}
	private CharInfo? _charInfo = null;

	#region Helpers

	/// <summary>
	/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
	/// </summary>
	public int CharIndexInRam(int relativeIndex) => CharData.CharIndexInRam(CharsBaseAddress, relativeIndex);

	#endregion

	#region Declarations

	public enum CharColourType
	{
		FCM,
		NCM
	}

	public class InputOutput
	{
		/// <summary>
		/// Input stream provider.
		/// </summary>
		public IStreamProvider Input { get; init; } = null!;


		/// <summary>
		/// Output character stream provider.
		/// </summary>
		public IStreamProvider OutputCharsStream { get; init; } = null!;

		/// <summary>
		/// Output palette stream provider.
		/// </summary>
		public IStreamProvider OutputPaletteStream { get; init; } = null!;

		/// <summary>
		/// Output screen data stream provider.
		/// </summary>
		public IStreamProvider OutputScreenStream { get; init; } = null!;

		/// <summary>
		/// Output colours data stream provider.
		/// </summary>
		public IStreamProvider OutputColourStream { get; init; } = null!;

		/// <summary>
		/// Output info data stream provider.
		/// </summary>
		public IStreamProvider OutputInfoDataStream { get; init; } = null!;

		/// <summary>
		/// Output info image stream provider.
		/// </summary>
		public IStreamProvider OutputInfoImageStream { get; init; } = null!;
	}

	#endregion
}

#endregion
