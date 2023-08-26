using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners;

namespace M65Converter.Sources.Exporting;

public class LDtkExporter
{
	public OptionsType Options { get; set; } = null!;

	#region Public

	public void Export()
	{
		// Note: the order of exporting is not important from generated data point of view, but the order below was chosen to keep relevant data together in the logs. For example layer data which is followed immediately with chars data, and then chars data followed by palette - with verbose logging the two output pairs are close together to be able to compare visually.
		Logger.Verbose.Separator();
		Logger.Info.Message("Exporting LDtk data");

		// Export the colour data.
		ExportColourData(CreateExporter("colour ram", "colour.bin"));

		// Export the layer data.
		ExportLayerData(CreateExporter("layers", "layer.bin"));

		// Export the characters.
		ExportCharsData(CreateExporter("chars", "chars.bin"));

		// Export the palette.
		ExportPalette(CreateExporter("palette", "chars.pal"));

		Logger.Verbose.Separator();
	}

	#endregion

	#region Exporting

	private void ExportColourData(Exporter exporter)
	{
		var layer = Options.Layers.First();
		var image = layer.IndexedImage;

		exporter.Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"Each colour is {Options.ProgramOptions.CharInfo.CharBytes} bytes");
			Logger.Verbose.Option("Top-to-down, left-to-right order");

			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					MinValueLength = 4,
				}
				: null;

			for (var y = 0; y < image.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < image.Width; x++)
				{
					var index = image[x, y];
					var charData = Options.CharsContainer.Images[index];

					switch (exporter.Options.ProgramOptions.CharColour)
					{
						case LDtkRunner.OptionsType.CharColourType.FCM:
						{
							// For FCM colour ram is not important, we set both bytes to 0.
							var byte1 = 0b00000000;
							var byte2 = 0b00000000;

							writer.Write((byte)byte1);
							writer.Write((byte)byte2);

							// Note: we flip the bytes so the hex output will be in little endian format.
							formatter?.AppendData((byte1 << 8) | byte2);

							break;
						}

						case LDtkRunner.OptionsType.CharColourType.NCM:
						{
							var colourBank = charData.IndexedImage.Bank;

							//            +-------------- vertically flip character
							//            |+------------- horizontally flip character
							//            ||+------------ alpha blend mode
							//            |||+----------- gotox
							//            ||||+---------- use 4-bits per pixel and 16x8 chars
							//            |||||+--------- trim pixels from right char side
							//            |||||| +------- number of pixels to trim
							//            |||||| |
							//            ||||||-+
							var byte1 = 0b00001000;

							//            +-------------- underline
							//            |+-------------- bold
							//            ||+------------- reverse
							//            |||+------------ blink
							//            |||| +---------- colour bank 0-16
							//            |||| |
							//            ||||-+--
							var byte2 = 0b00000000;
							byte2 |= (colourBank & 0x0f);

							// No sure why colour bank needs to be in high nibble. According to documentation this is needed if VIC II multi-colour-mode is enabled, however in my code this is also needed if VIC III extended attributes are enabled (AND VIC II MCM is disabled).
							byte2 = ((byte)byte2).SwapNibble();

							writer.Write((byte)byte1);
							writer.Write((byte)byte2);

							// Note: we flip the bytes so the hex output will be in little endian format.
							formatter?.AppendData((byte1 << 8) | byte2);

							break;
						}
					}
				}
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Exported colours (little endian hex values):");
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportLayerData(Exporter exporter)
	{
		exporter.Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"Copy to memory ${Options.ProgramOptions.CharsBaseAddress:X}");
			Logger.Verbose.Option($"Char start index {Options.ProgramOptions.CharIndexInRam(0)} (${Options.ProgramOptions.CharIndexInRam(0):X})");
			Logger.Verbose.Option("All pixels as char indices");
			Logger.Verbose.Option($"Each pixel is {Options.ProgramOptions.CharInfo.CharBytes} bytes");
			Logger.Verbose.Option("Top-to-down, left-to-right order");

			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
				}
				: null;

			var layer = Options.Layers.First();
			var image = layer.IndexedImage;

			for (var y = 0; y < image.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < image.Width; x++)
				{
					var index = image[x, y];
					var charIndex = Options.ProgramOptions.CharIndexInRam(index);

					formatter?.AppendData(charIndex);

					// Note: at the moment we only support 2-byte chars.
					writer.Write((byte)(charIndex & 0xff));
					writer.Write((byte)((charIndex >> 8) & 0xff));
				}
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Exported layer (big endian hex char indices adjusted to base address ${Options.ProgramOptions.CharsBaseAddress:X}):");
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportCharsData(Exporter exporter)
	{
		exporter.Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"{Options.CharsContainer.Images.Count} characters");
			switch (Options.ProgramOptions.CharColour)
			{
				case LDtkRunner.OptionsType.CharColourType.NCM:
					Logger.Verbose.Option("Each character is 16x8 pixels");
					Logger.Verbose.Option("Each pixel is 4 bits, 2 successive pixels form 1 byte");
					break;
				case LDtkRunner.OptionsType.CharColourType.FCM:
					Logger.Verbose.Option("Each character is 8x8 pixels");
					Logger.Verbose.Option("Each pixel is 8 bits / 1 byte");
					break;
			}
			Logger.Verbose.Option("All pixels as palette indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			Logger.Verbose.Option($"Character size is {Options.ProgramOptions.CharInfo.CharDataSize} bytes");

			var charData = Logger.Verbose.IsEnabled ? new List<byte>() : null;
			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					Headers = new[] { "Address", "Index", $"Data ({Options.ProgramOptions.CharInfo.CharDataSize} bytes)" },
					Prefix = " $",
					Suffix = " "
				}
				: null;

			var charIndex = -1;
			foreach (var character in Options.CharsContainer.Images)
			{
				charIndex++;

				var startingFilePosition = (int)writer.BaseStream.Position;

				charData?.Clear();
				formatter?.StartNewLine();

				for (var y = 0; y < character.IndexedImage.Height; y++)
				{
					switch (Options.ProgramOptions.CharColour)
					{
						case LDtkRunner.OptionsType.CharColourType.NCM:
							for (var x = 0; x < character.IndexedImage.Width; x += 2)
							{
								var colour1 = character.IndexedImage[x, y];
								var colour2 = character.IndexedImage[x + 1, y];
								var colour = (byte)(((colour1 & 0x0f) << 4) | (colour2 & 0x0f));
								var swapped = colour.SwapNibble();
								charData?.Add(swapped);
								writer.Write(swapped);
							}
							break;

						case LDtkRunner.OptionsType.CharColourType.FCM:
							for (var x = 0; x < character.IndexedImage.Width; x++)
							{
								var colour = (byte)(character.IndexedImage[x, y] & 0xff);
								charData?.Add(colour);
								writer.Write(colour);
							}
							break;
					}
				}

				formatter?.AppendData(Options.ProgramOptions.CharsBaseAddress + startingFilePosition);
				formatter?.AppendData(Options.ProgramOptions.CharIndexInRam(charIndex));
				if (charData != null)
				{
					var dataArray = charData.ToArray();
					var first8 = string.Join("", dataArray[0..7].Select(x => x.ToString("X2")));
					var last8 = string.Join("", dataArray[^8..].Select(x => x.ToString("X2")));
					formatter?.AppendString($"{first8}...{last8}");
				}
			}

			Logger.Verbose.Separator();
			formatter?.Log(Logger.Verbose.Option);
		});
	}

	private void ExportPalette(Exporter exporter)
	{
		exporter.Export(writer =>
		{
			new PaletteExporter().Export(
				palette: Options.CharsContainer.GlobalPalette,
				writer: writer
			);
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
