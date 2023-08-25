using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Exporting;
using M65Converter.Sources.Helpers.Converters;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

using System.CommandLine;
using System.CommandLine.Binding;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses LDtk simplified output into mega 65 compatible files
/// </summary>
public class LDtkRunner : BaseRunner
{
	private OptionsType Options { get; set; } = null!;
	private ImagesContainer CharsContainer { get; } = new();
	private List<LDtkExporter.LayerData> ExportLayers { get; } = new();

	#region Overrides

	protected override void OnValidate()
	{
		base.OnValidate();

		if ((Options.CharsBaseAddress % Options.CharInfo.CharDataSize) != 0)
		{
			var prev = (Options.CharsBaseAddress / Options.CharInfo.CharDataSize) * Options.CharInfo.CharDataSize;
			var next = prev + Options.CharInfo.CharDataSize;
			throw new ArgumentException($"Char base address must start on {Options.CharInfo.CharDataSize} byte boundary. C" +
				$"For example ${prev:X} or ${next:X}");
		}
	}

	protected override void OnRun()
	{
		PrintOptions();

		ParseBaseChars();
		ParseInputs();

		MergePalette();
		ValidateParsedData();
		
		Export();
	}

	#endregion

	#region Parsing

	private void ParseBaseChars()
	{
		if (Options.BaseCharsImage == null) return;

		new TimeRunner().Run(() =>
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {Options.BaseCharsImage}");
			Logger.Debug.Message($"Adding characters from base image {Options.BaseCharsImage.Name}");

			// Load the image.
			var image = Image.Load<Argb32>(Options.BaseCharsImage.FullName);

			// For base characters we keep all transparents to achieve consistent results. With these characters it's responsibility of the creator to trim source image. Same for duplicates, we want to leave all characters to preserve positions, however when matching them on layers, it will always take the first match.
			var result = new ImageSplitter
			{
				ItemWidth = Options.CharInfo.Width,
				ItemHeight = Options.CharInfo.Height,
				TransparencyOptions = TransparencyOptionsType.KeepAll,
				DuplicatesOptions = DuplicatesOptionsType.KeepAll
			}
			.Split(image, CharsContainer);

			// Note: we ignore indexed image for base characters. We only need actual layers from LDtk.
			Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} characters");
		});
	}

	private void ParseInputs()
	{
		// Add fully transparent item if we don't yet have one. We need to have at least one fully transparent character so that we can properly setup indexed layers that contain transparent characters.
		CharsContainer.AddTransparentImage(
			width: Options.CharInfo.Width,
			height: Options.CharInfo.Height
		);
		
		// Parse all input folders.
		new InputFilesHandler
		{
			InputFolders = Options.InputFolders
		}
		.Run(input =>
		{
			// Parse JSON file data.
			var data = LDtkData.Parse(input);

			// Add all extra characters from individual layers.
			foreach (var layer in data.Layers)
			{
				Logger.Verbose.Separator();
				Logger.Verbose.Message($"{layer.Path}");
				Logger.Debug.Message($"Adding characters from {Path.GetFileName(layer.Path)}");

				// For extra characters we ignore all transparent ones. These "auto-added" characters are only added if they are opaque and unique. No fully transparent or duplicates allowed. This works the same regardless of whether base chars image was used or not.
				var result = new ImageSplitter
				{
					ItemWidth = Options.CharInfo.Width,
					ItemHeight = Options.CharInfo.Height,
					TransparencyOptions = TransparencyOptionsType.OpaqueOnly,
					DuplicatesOptions = DuplicatesOptionsType.UniqueOnly
				}
				.Split(layer.Image, CharsContainer);

				ExportLayers.Add(new LDtkExporter.LayerData
				{
					SourcePath = layer.Path,
					IndexedImage = result.IndexedImage
				});

				Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
			}
		});

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{CharsContainer.Images.Count} characters found");
	}

	#endregion

	#region Adjusting

	private void MergePalette()
	{
		Logger.Verbose.Separator();
		Logger.Debug.Message("Merging palette");

		var options = new PaletteMerger.OptionsType
		{
			Is4Bit = Options.CharColour == OptionsType.CharColourType.NCM,
			IsUsingTransparency = true,
			Images = CharsContainer.Images,
		};

		CharsContainer.GlobalPalette = PaletteMerger
			.Create(options)
			.Merge();

		Logger.Debug.Message($"{CharsContainer.GlobalPalette.Count} palette colours used");
	}

	private void ValidateParsedData()
	{
		if (CharsContainer.Images.Count > 8192)
		{
			throw new ArgumentException("Too many characters to fit 2 bytes, adjust source files");
		}

		if (CharsContainer.GlobalPalette.Count > 256)
		{
			throw new ArgumentException("Too many palette entries, adjust source files to use less colours");
		}
	}

	#endregion

	#region Exporting

	private void Export()
	{
		var options = new LDtkExporter.OptionsType
		{
			Layers = ExportLayers,
			CharsContainer = CharsContainer,
			ProgramOptions = Options
		};

		LDtkExporter
			.Create(options)
			.Export();
	}

	#endregion

	#region Helpers

	private void PrintOptions()
	{
		Logger.Info.Message("Parsing LDtk files");

		Logger.Debug.Separator();

		if (Options.BaseCharsImage != null)
		{
			Logger.Debug.Option($"Base characters will be generated from: {Path.GetFileName(Options.BaseCharsImage.FullName)}");
			Logger.Debug.Option("Additional characters will be generated from layer images");
		}
		else
		{
			Logger.Debug.Option("Characters will be generated from layer images");
		}

		if (Options.IsRasterRewriteBufferSupported)
		{
			Logger.Debug.Option("Individual layers will be exported as RRB");
		}
		else
		{
			Logger.Debug.Option("Layers will be merged");
		}

		Logger.Debug.Option(string.Join("", new string[]
		{
			$"Character type: {Options.CharColour} (",
			$"{Options.CharInfo.Width}x{Options.CharInfo.Height} pixels, ",
			$"{Options.CharInfo.ColoursPerTile} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Options.CharInfo.CharBytes} bytes");

		var firstChar = Options.CharIndexInRam(0);
		Logger.Debug.Option($"Characters base address: ${Options.CharsBaseAddress:X}, first char index {firstChar} (${firstChar:X})");
	}

	#endregion

	#region Options

	public class OptionsType
	{
		/// <summary>
		/// Array of input folders
		/// </summary>
		public FileInfo[] InputFolders { get; set; } = null!;

		/// <summary>
		/// Optional external tiles image.
		/// 
		/// Useful when tiles setup is important and should be preserved. For example to predictably setup digits and letters or animate individual charcters. If this file is provided, it will be split into tiles as is, including all fully transparent tiles. These tiles will be generated first, then all non-matching tiles appended afterwards.
		/// 
		/// If this image is not provided, then tiles will be auto-generated from source images. This is the simplest way of using the converter, however tiles setup may vary between different calls, based on source images changes.
		/// </summary>
		public FileInfo? BaseCharsImage { get; set; }

		/// <summary>
		/// Output folder.
		/// </summary>
		public FileInfo? OutputFolder { get; set; } = null!;

		/// <summary>
		/// Template for output filenames.
		/// </summary>
		public string OutputNameTemplate { get; set; } = null!;

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
		/// Tile type. Changing this value will change <see cref="CharInfo"/> as well.
		/// </summary>
		public CharColourType CharColour {
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
		public CharInfoType CharInfo {
			get
			{
				_charInfo ??= CharColour switch
				{
					CharColourType.FCM => new CharInfoType
					{
						Width = 8,
						Height = 8,
						CharBytes = 2,
						CharDataSize = 64,
						ColoursPerTile = 256
					},

					CharColourType.NCM => new CharInfoType
					{
						Width = 16,
						Height = 8,
						CharBytes = 2,
						CharDataSize = 64,
						ColoursPerTile = 16
					},

					_ => throw new ArgumentException($"Unknown tile type {CharColour}")
				};

				return _charInfo!;
			}
		}
		private CharInfoType? _charInfo = null;

		#region Helpers

		/// <summary>
		/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
		/// </summary>
		public int CharIndexInRam(int relativeIndex)
		{
			return (CharsBaseAddress + relativeIndex * CharInfo.CharDataSize) / CharInfo.CharDataSize;
		}

		#endregion

		#region Declarations

		public enum CharColourType
		{
			FCM,
			NCM
		}

		public class CharInfoType
		{
			public int Width { get; set; }
			public int Height { get; set; }
			public int CharBytes { get; set; }
			public int CharDataSize { get; set; }
			public int ColoursPerTile { get; set; }
		}

		#endregion
	}

	public class OptionsBinder : BaseOptionsBinder<OptionsType>
	{
		#region Options

		private Argument<FileInfo[]> inputFolders = new(
			name: "input",
			description: "One or more input folders with LDtk simplified exported files"
		)
		{
			Arity = ArgumentArity.OneOrMore
		};

		private Option<FileInfo?> baseCharsImage = new(
			aliases: new[] { "-c", "--chars" },
			description: "Optional base characters image"
		);

		private Option<FileInfo?> outputFolder = new(
			aliases: new[] { "-o", "--out-folder" },
			description: "Folder to generate output in; input folder if not specified"
		);

		private Option<string> outputFileTemplate = new(
			aliases: new[] { "-n", "--out-name" },
			description: string.Join("\n", new string[] 			
			{
				"Name prefix to use for output generation. Can also include subfolder(s). Placeholders:",
				"- {level} placeholder is replaced with level (input) name",
				"- {name} placeholder is replaced by output file name",
				"- {suffix} placeholder is replaced by output file suffix and extension",
				""	// this is used so default value is written in new line
			}),
			getDefaultValue: () => "{level}-{name}-{suffix}"
		);

		private Option<OptionsType.CharColourType> tileType = new(
			aliases: new[] { "-m", "--colour" },
			description: "Colour mode",
			getDefaultValue: () => OptionsType.CharColourType.FCM
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

		#endregion

		#region Overrides

		protected override Command OnCreateCommand()
		{
			return new Command(
				name: "ldtk",
				description: "Converts simplified LDtk output into raw data"
			);
		}

		protected override BaseRunner OnCreateRunner(OptionsType options)
		{
			return new LDtkRunner
			{
				Options = options,
			};
		}

		protected override OptionsType GetBoundValue(BindingContext bindingContext)
		{
			return new OptionsType
			{
				InputFolders = bindingContext.ParseResult.GetValueForArgument(inputFolders),
				BaseCharsImage = bindingContext.ParseResult.GetValueForOption(baseCharsImage),
				OutputFolder = bindingContext.ParseResult.GetValueForOption(outputFolder),
				OutputNameTemplate = bindingContext.ParseResult.GetValueForOption(outputFileTemplate)!,
				CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)!.ParseAsInt(),
				CharColour = bindingContext.ParseResult.GetValueForOption(tileType),
				IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer)
			};
		}

		#endregion
	}

	#endregion
}
