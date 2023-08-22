using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners;

namespace M65Converter.Sources.Exporting;

public abstract class LDtkExporter
{
	public List<LayerData> Layers { get; set; } = new();
	public ImagesContainer CharsContainer { get; set; } = null!;
	public LDtkRunner.OptionsType Options { get; set; } = null!;

	#region Subclass

	public abstract void Export();

	#endregion

	#region Exporting

	protected void ExportLayer(IndexedImage layer, string? filename = null)
	{
		// Prepare output folder base name and the final name for this layer.
		var outputInfo = PrepareOutputFolder();

		// Export the layer data.
		var layerPath = PrepareOutputFilename(outputInfo, filename, "layer.bin");
		ExportLayerData(layer, layerPath);

		// Export the characters.
		var charsPath = PrepareOutputFilename(outputInfo, filename, "chars.bin");
		ExportCharsData(charsPath);

		// Export the palette.
		var palettePath = PrepareOutputFilename(outputInfo, filename, "chars.pal");
		ExportPalette(palettePath);
	}

	private void ExportLayerData(IndexedImage layer, string path)
	{
		Export("layer", path, writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option("All pixels as char indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			Logger.Verbose.Option($"Each pixel is {Options.CharWidth} byte(s)");

			var formatter = Logger.Verbose.IsEnabled ? new ChangesTableFormatter { IsHex = true } : null;

			for (var y = 0; y < layer.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < layer.Width; x++)
				{
					var index = layer[x, y];
					var charIndex = (Options.CharsBaseAddress + index * Options.CharInfo.CharSize) / Options.CharInfo.CharSize;

					switch (Options.CharWidth)
					{
						case 1:
							writer.Write((byte)(charIndex & 0xff));
							formatter?.AppendNoChange(charIndex);
							break;

						case 2:
							writer.Write((byte)(charIndex & 0xff));
							writer.Write((byte)((charIndex >> 8) & 0xff));
							formatter?.AppendNoChange(charIndex);
							break;
					}
				}
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Exported layer (big endian hex char indices adjusted to base address ${Options.CharsBaseAddress:X}):");
			formatter?.ExportLines(Logger.Verbose.Option);
		});
	}

	private void ExportCharsData(string path)
	{
		Export("characters", path, writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option("All pixels as palette indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			switch (Options.CharInfo.ColoursPerTile)
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

			foreach (var character in CharsContainer.Images)
			{
				for (var y = 0; y < character.IndexedImage.Height; y++)
				{
					switch (Options.CharInfo.ColoursPerTile)
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

	private void ExportPalette(string path)
	{
		Export("palette", path, writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"First all {CharsContainer.GlobalPalette.Count} red values");
			Logger.Verbose.Option($"Followed by {CharsContainer.GlobalPalette.Count} green values");
			Logger.Verbose.Option($"Followed by {CharsContainer.GlobalPalette.Count} blue values");
			Logger.Verbose.Option("Each RGB component is 1 byte");

			void Export(Func<Argb32, byte> picker)
			{
				foreach (var colour in CharsContainer.GlobalPalette)
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

	private static void Export(string description, string path, Action<BinaryWriter> handler)
	{
		Logger.Verbose.Separator();
		Logger.Debug.Message($"Exporting {description} to {Path.GetFileName(path)}");
		Logger.Verbose.Message($"{path}");

		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		using var writer = new BinaryWriter(new FileStream(path, FileMode.Create));
		handler(writer);

		Logger.Debug.Message($"{writer.BaseStream.Length} bytes");
	}

	private Tuple<string, string> PrepareOutputFolder()
	{
		// Get first source layer folder (all layers are contained in the same folder, so it doesn't matter which one we take).
		var levelFolder = Path.GetDirectoryName(Layers[0].SourcePath);

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
		var root = Options.OutputFolder?.FullName ?? rootFolder!;
		var level = new DirectoryInfo(levelFolder!).Name;
		return new Tuple<string, string>(root, level);
	}

	private string PrepareOutputFilename(Tuple<string, string> pathLevel, string? name, string suffix)
	{
		var filename = Options.OutputNameTemplate
			.Replace("{level}", pathLevel.Item2)
			.Replace("{name}", name ?? pathLevel.Item2)
			.Replace("{suffix}", suffix);

		return Path.Combine(pathLevel.Item1, filename);
	}

	#endregion

	#region Declarations

	public class LayerData
	{
		public string SourcePath { get; set; } = null!;
		public IndexedImage IndexedImage { get; set; } = null!;
	}

	#endregion
}

public class LDtkMergedExporter : LDtkExporter
{
	#region Overrides

	public override void Export()
	{
		Logger.Verbose.Separator();
		Logger.Info.Message("Merging into single layer");

		var mergerLayer = CreateEmptyMergedLayer();
		MergeLayers(mergerLayer);
		ExportLayer(mergerLayer);
	}

	#endregion

	#region Helpers

	private IndexedImage CreateEmptyMergedLayer()
	{
		// Find the largest layer in case they differ in size.
		var width = 0;
		var height = 0;
		foreach (var layer in Layers)
		{
			if (layer.IndexedImage.Width > width) width = layer.IndexedImage.Width;
			if (layer.IndexedImage.Height > height) height = layer.IndexedImage.Height;
		}

		// Prepare merged layer prefilled with transparent character.
		var result = new IndexedImage();
		result.Prefill(width, height, CharsContainer.TransparentImageIndex);

		return result;
	}

	private void MergeLayers(IndexedImage destination)
	{
		Logger.Verbose.Message("Merging layers");

		var isFirstLayer = true;

		// Layers are exported in order bottom to top, so we need to iterate them reversed.
		foreach (var layer in Layers)
		{
			Logger.Verbose.Separator();
			Logger.Verbose.Option($"{Path.GetFileName(layer.SourcePath)}");

			var isChangeLogged = false;
			var formatter = Logger.Verbose.IsEnabled ? new ChangesTableFormatter() : null;

			for (var y = 0; y < layer.IndexedImage.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < layer.IndexedImage.Width; x++)
				{
					var layerCharIndex = layer.IndexedImage[x, y];

					if (layerCharIndex != CharsContainer.TransparentImageIndex)
					{
						var original = isFirstLayer ? layerCharIndex : destination[x, y];
						isChangeLogged = true;
						formatter?.AppendChange(original, layerCharIndex);
						destination[x, y] = layerCharIndex;
					}
					else
					{
						formatter?.AppendNoChange(layerCharIndex);
					}
				}
			}

			if (isChangeLogged && formatter != null)
			{
				formatter.ExportLines(Logger.Verbose.Option);
			}

			isFirstLayer = false;
		}
	}

	#endregion
}
