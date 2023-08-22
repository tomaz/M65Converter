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

		if (Options.CharWidth < 1) Options.CharWidth = 1;
		if (Options.CharWidth > 2) Options.CharWidth = 2;

		if (Options.CharWidth == 1 && Options.CharsBaseAddress <= 0x3fc0) {
			throw new ArgumentException($"Chars base address must be $3fc0 or less when single byte is used!");
		}

		if ((Options.CharsBaseAddress % Options.CharInfo.CharSize) != 0)
		{
			var prev = (Options.CharsBaseAddress / Options.CharInfo.CharSize) * Options.CharInfo.CharSize;
			var next = prev + Options.CharInfo.CharSize;
			throw new ArgumentException($"Char base address must start on 64 byte boundary. Consider changing to ${prev:X} or ${next:X}");
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
			var splitter = new ImageSplitter
			{
				ItemWidth = Options.CharInfo.Width,
				ItemHeight = Options.CharInfo.Height,
				TransparencyOptions = TransparencyOptionsType.KeepAll,
				DuplicatesOptions = DuplicatesOptionsType.KeepAll
			};

			// Split base characters into container.
			var result = splitter.Split(
				source: image,
				container: CharsContainer
			);

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
				var splitter = new ImageSplitter
				{
					ItemWidth = Options.CharInfo.Width,
					ItemHeight = Options.CharInfo.Height,
					TransparencyOptions = TransparencyOptionsType.OpaqueOnly,
					DuplicatesOptions = DuplicatesOptionsType.UniqueOnly
				};

				var result = splitter.Split(
					source: layer.Image,
					container: CharsContainer
				);

				ExportLayers.Add(new LDtkExporter.LayerData
				{
					SourcePath = layer.Path,
					IndexedImage = result.IndexedImage
				});

				Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
			}
		});

		Logger.Debug.Message($"{CharsContainer.Images.Count} characters found");
	}

	#endregion

	#region Adjusting

	private void MergePalette()
	{
		Logger.Debug.Message("Merging palette");

		var merger = new PaletteMerger
		{
			Images = CharsContainer.Images.ToList()
		};

		CharsContainer.GlobalPalette = merger.Merge();

		Logger.Debug.Message($"{CharsContainer.GlobalPalette.Count} palette colours found");
	}

	private void ValidateParsedData()
	{
		switch (Options.CharWidth)
		{
			case 1:
				if (CharsContainer.Images.Count <= 256) break;
				throw new ArgumentException("Too many characters to fit 1 byte, use \"--char-size 2\"");

			case 2:
				if (CharsContainer.Images.Count <= 65536) break;
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
		if (Options.IsRasterRewriteBufferSupported)
		{

		}
		else
		{
			var exporter = new LDtkMergedExporter
			{
				Layers = ExportLayers,
				CharsContainer = CharsContainer,
				Options = Options
			};

			exporter.Export();
		}
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
			$"Character type: {Options.CharType} (",
			$"{Options.CharInfo.Width}x{Options.CharInfo.Height} pixels, ",
			$"{Options.CharInfo.ColoursPerTile} colours per character)"
		}));

		Logger.Debug.Option($"Character size: {Options.CharWidth} byte(s)");
		Logger.Debug.Option($"Characters base address: ${Options.CharsBaseAddress:X}");
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
		/// The width of a single character in bytes.
		/// </summary>
		public int CharWidth { get; set; }

		/// <summary>
		/// Base address where the characters will be loaded into on Mega 65.
		/// </summary>
		public int CharsBaseAddress { get; set; }

		/// <summary>
		/// Tile type. Changing this value will change <see cref="CharInfo"/> as well.
		/// </summary>
		public CharTypeType CharType {
			get => _charType;
			set
			{
				_charType = value;
				_charInfo = null;
			}
		}
		private CharTypeType _charType;

		/// <summary>
		/// Tile information, depends on <see cref="CharType"/>.
		/// </summary>
		public CharInfoType CharInfo {
			get
			{
				_charInfo ??= CharType switch
				{
					CharTypeType.FullColour => new CharInfoType
					{
						Width = 8,
						Height = 8,
						CharSize = 64,
						ColoursPerTile = 256
					},

					CharTypeType.NibbleColour => new CharInfoType
					{
						Width = 16,
						Height = 8,
						CharSize = 64,
						ColoursPerTile = 16
					},

					_ => throw new ArgumentException($"Unknown tile type {CharType}")
				};

				return _charInfo!;
			}
		}
		private CharInfoType? _charInfo = null;

		#region Declarations

		public enum CharTypeType
		{
			FullColour,
			NibbleColour
		}

		public class CharInfoType
		{
			public int Width { get; set; }
			public int Height { get; set; }
			public int CharSize { get; set; }
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
			aliases: new[] { "-b", "--base" },
			description: "Optional base characters image"
		);

		private Option<FileInfo?> outputFolder = new(
			name: "--out-folder",
			description: "Folder to generate output in; input folder if not specified"
		);

		private Option<string?> outputFileTemplate = new(
			name: "--out-name",
			description: string.Join("\n", new string[] 			
			{
				"Name prefix to use for output generation. Can also include subfolder(s). Placeholders:",
				"- {level} placeholder is replaced with level (input) name",
				"- {name} placeholder is replaced by output file name",
				"- {suffix} placeholder is replaced by output file suffix and extension",
				"If not provided \"{level}-{name}\" is used"
			})
		);

		private Option<OptionsType.CharTypeType> tileType = new(
			aliases: new[] { "-t", "--type" },
			description: "Type of characters to generate",
			getDefaultValue: () => OptionsType.CharTypeType.FullColour
		);

		private Option<int> charWidth = new(
			name: "--char-width",
			description: "Character width (1 or 2)",
			getDefaultValue: () => 2
		);

		private Option<string> charBaseAddress = new(
			aliases: new[] { "-a", "--chars-address" },
			description: "Base address where characters will be loaded into on Mega 65",
			getDefaultValue: () => "$10000"
		);

		private Option<bool> rasterRewriteBuffer = new(
			aliases: new[] { "-r", "--rrb" },
			description: "Raster rewrite buffer support",
			getDefaultValue: () => true
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
				OutputNameTemplate = bindingContext.ParseResult.GetValueForOption(outputFileTemplate) ?? "{layer}-{name}",
				CharsBaseAddress = bindingContext.ParseResult.GetValueForOption(charBaseAddress)!.ParseAsInt(),
				CharWidth = bindingContext.ParseResult.GetValueForOption(charWidth),
				CharType = bindingContext.ParseResult.GetValueForOption(tileType),
				IsRasterRewriteBufferSupported = bindingContext.ParseResult.GetValueForOption(rasterRewriteBuffer)
			};
		}

		#endregion
	}

	#endregion
}
