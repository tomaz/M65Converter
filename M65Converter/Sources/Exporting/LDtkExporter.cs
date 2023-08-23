using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Exporting.LDtk;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners;

namespace M65Converter.Sources.Exporting;

public abstract class LDtkExporter
{
	public OptionsType Options { get; set; } = null!;

	#region Initialization & Disposal

	public static LDtkExporter Create(OptionsType options)
	{
		LDtkExporter result = options.ProgramOptions.IsRasterRewriteBufferSupported
			? throw new NotImplementedException()
			: new LDtkExporterMergedLayers();

		result.Options = options;

		return result;
	}

	protected LDtkExporter()
	{
	}

	#endregion

	#region Subclass

	/// <summary>
	/// Called before any exporting takes place. Subclass can prepare and assign data that will be used throughout the export afterwards.
	/// </summary>
	protected virtual void OnPrepareExportData()
	{
		// Noting to do by default.
	}

	/// <summary>
	/// Called when layers need to be exported.
	/// </summary>
	protected abstract void OnExportLayerData(Exporter exporter);

	/// <summary>
	/// Called when colours RAM needs to be exported.
	/// </summary>
	protected abstract void OnExportColourData(Exporter exporter);

	#endregion

	#region Public

	public void Export()
	{
		Logger.Verbose.Separator();
		Logger.Info.Message("Exporting LDtk data");

		// Ask subclass to prepare common data.
		OnPrepareExportData();

		// Export the palette.
		ExportPalette(CreateExporter("palette", "chars.pal"));

		// Export the characters.
		ExportCharsData(CreateExporter("chars", "chars.bin"));

		// Export the layer data.
		OnExportLayerData(CreateExporter("layers", "layer.bin"));

		// Export the colour data.
		OnExportColourData(CreateExporter("colour ram", "colour.bin"));

		Logger.Verbose.Separator();
	}

	#endregion

	#region Exporting

	private void ExportCharsData(Exporter exporter)
	{
		exporter.Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option("All pixels as palette indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			switch (Options.ProgramOptions.CharInfo.ColoursPerTile)
			{
				case 16:
					Logger.Verbose.Option("Each character is 16x8 pixels");
					Logger.Verbose.Option("Each pixel is 4 bits, 2 pixels form 1 byte");
					break;
				case 256:
					Logger.Verbose.Option("Each character is 8x8 pixels");
					Logger.Verbose.Option("Each pixel is 8 bits / 1 byte");
					break;
			}

			foreach (var character in Options.CharsContainer.Images)
			{
				for (var y = 0; y < character.IndexedImage.Height; y++)
				{
					switch (Options.ProgramOptions.CharInfo.ColoursPerTile)
					{
						case 16:
							for (var x = 0; x < character.IndexedImage.Width; x += 2)
							{
								var colour1 = character.IndexedImage[x, y];
								var colour2 = character.IndexedImage[x + 1, y];
								var colour = colour1 & 0x0f << 4 | colour2 & 0x0f;
								writer.Write((byte)colour);
							}
							break;

						case 256:
							for (var x = 0; x < character.IndexedImage.Width; x++)
							{
								var colour = character.IndexedImage[x, y];
								writer.Write((byte)(colour & 0xff));
							}
							break;
					}
				}
			}
		});
	}

	private void ExportPalette(Exporter exporter)
	{
		exporter.Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"First all {Options.CharsContainer.GlobalPalette.Count} red values");
			Logger.Verbose.Option($"Followed by {Options.CharsContainer.GlobalPalette.Count} green values");
			Logger.Verbose.Option($"Followed by {Options.CharsContainer.GlobalPalette.Count} blue values");
			Logger.Verbose.Option("Each RGB component is 1 byte");

			void Export(Func<Argb32, byte> picker)
			{
				foreach (var colour in Options.CharsContainer.GlobalPalette)
				{
					writer.Write(picker(colour));
				}
			}

			Export((colour) => colour.R);
			Export((colour) => colour.G);
			Export((colour) => colour.B);
		});
	}

	#endregion

	#region Helpers

	private Exporter CreateExporter(string description, string suffix)
	{
		return new Exporter
		{
			Options = Options,
			LogDescription = description,
			FileSuffix = suffix,
		};
	}

	#endregion

	#region Declarations

	public class LayerData
	{
		public string SourcePath { get; set; } = null!;
		public IndexedImage IndexedImage { get; set; } = null!;
	}

	public class OptionsType
	{
		public List<LayerData> Layers { get; set; } = new();
		public ImagesContainer CharsContainer { get; set; } = null!;
		public LDtkRunner.OptionsType ProgramOptions { get; set; } = null!;
	}

	protected class Exporter
	{
		public OptionsType Options { get; init; } = null!;
		public string LogDescription { get; init; } = null!;
		public string FileSuffix { get; init; } = null!;

		#region Exporting

		public void Export(Action<BinaryWriter> handler)
		{
			// Prepare filename.
			var outputInfo = PrepareOutputFolder();
			var path = PrepareOutputFilename(outputInfo, null, FileSuffix);

			Logger.Verbose.Separator();
			Logger.Debug.Message($"Exporting {LogDescription} to {Path.GetFileName(path)}");
			Logger.Verbose.Message($"{path}");

			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			using var writer = new BinaryWriter(new FileStream(path, FileMode.Create));
			handler(writer);

			Logger.Debug.Message($"{writer.BaseStream.Length} bytes");
		}

		#endregion

		#region Helpers

		private Tuple<string, string> PrepareOutputFolder()
		{
			// Get first source layer folder (all layers are contained in the same folder, so it doesn't matter which one we take).
			var levelFolder = Path.GetDirectoryName(Options.Layers[0].SourcePath);

			// Get the root folder of the level. Simplified export saves each level into its own folder with data.json file inside "{levelName}/simplified/AutoLayer" subfolder. So we remove 3 folders from layer image file to get to the root where LDtk source file is contained. Note how we dynamically swap source and root folder so that we still can get a valid result if `GetDirectoryName` returns null (aka folder is root).
			var rootFolder = levelFolder;
			for (int i = 0; i < 3; i++)
			{
				levelFolder = rootFolder;
				rootFolder = Path.GetDirectoryName(rootFolder);
				if (rootFolder == null)
				{
					rootFolder = levelFolder;
					break;
				}
			}

			// At this point we have root and level folders. We need level folder either way, but for root we prefer explicit output folder and falldown to root folder (where LDtk file is saved).
			var root = Options.ProgramOptions.OutputFolder?.FullName ?? rootFolder!;
			var level = new DirectoryInfo(levelFolder!).Name;
			return new Tuple<string, string>(root, level);
		}

		private string PrepareOutputFilename(Tuple<string, string> pathLevel, string? name, string suffix)
		{
			var filename = Options.ProgramOptions.OutputNameTemplate
				.Replace("{level}", pathLevel.Item2)
				.Replace("{name}", name ?? pathLevel.Item2)
				.Replace("{suffix}", suffix);

			return Path.Combine(pathLevel.Item1, filename);
		}

		#endregion
	}

	#endregion
}
