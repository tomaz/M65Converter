using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting.LDtk;

public class LDtkExporterMergedLayers : LDtkExporter
{
	private IndexedImage MergedLayer { get; set; } = null!;

	#region Overrides

	protected override void OnPrepareExportData()
	{
		MergedLayer = CreateEmptyMergedLayer();

		MergeLayersInto(MergedLayer);
	}

	protected override void OnExportLayerData(Exporter exporter)
	{
		// Export merged layer.
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

			for (var y = 0; y < MergedLayer.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < MergedLayer.Width; x++)
				{
					var index = MergedLayer[x, y];
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

	protected override void OnExportColourData(Exporter exporter)
	{
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

			for (var y = 0; y < MergedLayer.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < MergedLayer.Width; x++)
				{
					var index = MergedLayer[x, y];
					var charData = Options.CharsContainer.Images[index];

					switch (exporter.Options.ProgramOptions.CharColour)
					{
						case Runners.LDtkRunner.OptionsType.CharColourType.FCM:
						{
							//            +-------------- vertically flip character
							//            |+------------- horizontally flip character
							//            ||+------------ alpha blend mode
							//            |||+----------- gotox
							//            ||||+---------- use 4-bits per pixel and 16x8 chars
							//            |||||+--------- trim pixels from right char side
							//            |||||| +------- number of pixels to trim
							//            |||||| |
							//            ||||||-+
							var byte1 = 0b00000000;

							//            +-------------- underline
							//            |+-------------- bold
							//            ||+------------- reverse
							//            |||+------------ blink
							//            |||| +---------- colour bank 0-16
							//            |||| |
							//            ||||-+--
							var byte2 = 0b00000000;

							writer.Write((byte)byte1);
							writer.Write((byte)byte2);

							// Note: we flip the bytes so the hex output will be in little endian format.
							formatter?.AppendData((byte1 << 8) | byte2);

							break;
						}

						case Runners.LDtkRunner.OptionsType.CharColourType.NCM:
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

	#endregion

	#region Helpers

	private IndexedImage CreateEmptyMergedLayer()
	{
		// Find the largest layer in case they differ in size.
		var width = 0;
		var height = 0;
		foreach (var layer in Options.Layers)
		{
			if (layer.IndexedImage.Width > width) width = layer.IndexedImage.Width;
			if (layer.IndexedImage.Height > height) height = layer.IndexedImage.Height;
		}

		// Prepare merged layer prefilled with transparent character.
		var result = new IndexedImage();
		result.Prefill(width, height, Options.CharsContainer.TransparentImageIndex);

		return result;
	}

	private void MergeLayersInto(IndexedImage destination)
	{
		Logger.Verbose.Message("Merging layers");

		var isFirstLayer = true;

		// Layers are exported in order bottom to top, so we need to iterate them reversed.
		foreach (var layer in Options.Layers)
		{
			Logger.Verbose.Separator();
			Logger.Verbose.Option($"{Path.GetFileName(layer.SourcePath)}");

			var isChangeLogged = false;
			var formatter = Logger.Verbose.IsEnabled ? new TableFormatter() : null;

			for (var y = 0; y < layer.IndexedImage.Height; y++)
			{
				formatter?.StartNewLine();

				for (var x = 0; x < layer.IndexedImage.Width; x++)
				{
					var layerCharIndex = layer.IndexedImage[x, y];

					if (layerCharIndex != Options.CharsContainer.TransparentImageIndex)
					{
						var original = isFirstLayer ? layerCharIndex : destination[x, y];
						isChangeLogged = true;
						formatter?.AppendData(original, layerCharIndex);
						destination[x, y] = layerCharIndex;
					}
					else
					{
						formatter?.AppendData(layerCharIndex);
					}
				}
			}

			if (isChangeLogged && formatter != null)
			{
				formatter.Log(Logger.Verbose.Option);
			}

			isFirstLayer = false;
		}
	}

	#endregion
}
