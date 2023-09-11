using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting.Images;
using M65Converter.Sources.Exporting.Utils;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Intermediate;

/// <summary>
/// Container for all parsed data.
/// 
/// Besides being a container, this class also validates and exports common data.
/// </summary>
public class DataContainer
{
	/// <summary>
	/// All options for generating screens and colours data.
	/// </summary>
	public ScreenOptionsType ScreenOptions { get; set; } = null!;

	/// <summary>
	/// All the parsed characters.
	/// </summary>
	public ImagesContainer CharsContainer { get; } = new();

	/// <summary>
	/// All parsed layers containing source data for screen and colours data.
	/// </summary>
	public LevelData MergedLayers { get; set; } = new();

	/// <summary>
	/// Screen and colour data in a form suitable for exporting.
	/// </summary>
	public LayersData ExportData { get; set; } = new();

	// TODO: we need to have generated data for each input separately. Probably don't need merged layers - that's just intermediate data for screens and colours, but we do need export data to be an array of layers

	#region Validation

	/// <summary>
	/// Validates input data to make sure we can export it.
	/// 
	/// Note: this doesn't take care of all possible issues. It only checks for common issues.
	/// </summary>
	public void ValidateParsedData()
	{
		if (CharsContainer.Images.Count > 8192)
		{
			throw new ArgumentException("Too many characters to fit 2 bytes, adjust source files");
		}

		if (ExportData.Palette.Count > 256)
		{
			throw new ArgumentException("Too many colours in palette, adjust source files");
		}
	}

	#endregion

	#region Exporting

	/// <summary>
	/// Exports all generated data.
	/// </summary>
	public void ExportGeneratedData()
	{
		// Note: the order of exports is not important from generated data perspective, but the given order results in nicely grouped log data, especially when verbose logging is enabled. This way it's simpler to compare related data as it's printed close together.
		ExportColours();
		ExportScreens();
		ExportChars();
		ExportPalette();
		ExportLayerInfos();
		ExportInfoImages();
	}

	private void ExportColours()
	{
		foreach (var io in ScreenOptions.InputsOutputs)
		{
			CreateExporter("colour ram", io.OutputColourStream).Export(writer =>
			{
				Logger.Verbose.Message("Format:");
				Logger.Verbose.Option($"Each colour is {ScreenOptions.CharData.PixelDataSize} bytes");
				Logger.Verbose.Option("Top-to-down, left-to-right order");

				var formatter = Logger.Verbose.IsEnabled
					? new TableFormatter
					{
						IsHex = true,
						MinValueLength = 4,
					}
					: null;

				for (var y = 0; y < ExportData.Colour.Rows.Count; y++)
				{
					var row = ExportData.Colour.Rows[y];

					formatter?.StartNewRow();

					for (var x = 0; x < row.Columns.Count; x++)
					{
						var column = row.Columns[x];

						formatter?.AppendData(column.LittleEndianData);

						foreach (var data in column.Values)
						{
							writer.Write(data);
						}
					}
				}

				Logger.Verbose.Separator();
				Logger.Verbose.Message($"Exported colours (little endian hex values):");
				formatter?.Log(Logger.Verbose.Option);
			});
		}
	}

	private void ExportScreens()
	{
		foreach (var io in ScreenOptions.InputsOutputs)
		{
			CreateExporter("screen data", io.OutputScreenStream).Export(writer =>
			{
				Logger.Verbose.Message("Format:");
				Logger.Verbose.Option($"Expected to be copied to memory address ${ScreenOptions.CharsBaseAddress:X}");
				Logger.Verbose.Option($"Char start index {ScreenOptions.CharIndexInRam(0)} (${ScreenOptions.CharIndexInRam(0):X})");
				Logger.Verbose.Option("All pixels as char indices");
				Logger.Verbose.Option($"Each pixel is {ScreenOptions.CharData.PixelDataSize} bytes");
				Logger.Verbose.Option("Top-to-down, left-to-right order");

				var formatter = Logger.Verbose.IsEnabled
					? new TableFormatter
					{
						IsHex = true,
					}
					: null;

				for (var y = 0; y < ExportData.Screen.Rows.Count; y++)
				{
					var row = ExportData.Screen.Rows[y];

					formatter?.StartNewRow();

					for (var x = 0; x < row.Columns.Count; x++)
					{
						var column = row.Columns[x];

						// We log as big endian to potentially preserve 1-2 chars in the output. See comment in `TableFormatter.FormattedData()` method for more details.
						formatter?.AppendData(column.BigEndianData);

						foreach (var data in column.Values)
						{
							writer.Write(data);
						}
					}
				}

				Logger.Verbose.Separator();
				Logger.Verbose.Message($"Exported layer (big endian hex char indices adjusted to base address ${ScreenOptions.CharsBaseAddress:X}):");
				formatter?.Log(Logger.Verbose.Option);
			});
		}
	}

	private void ExportChars()
	{
		// TODO: we should move characters outside the array of I/Os since they are shared by all
		if (ScreenOptions.InputsOutputs.Length == 0) return;

		CreateExporter("chars", ScreenOptions.InputsOutputs[0].OutputCharsStream).Export(writer =>
		{
			Logger.Verbose.Message("Format:");
			Logger.Verbose.Option($"{CharsContainer.Images.Count} characters");
			switch (ScreenOptions.CharColour)
			{
				case ScreenOptionsType.CharColourType.NCM:
					Logger.Verbose.Option("Each character is 16x8 pixels");
					Logger.Verbose.Option("Each pixel is 4 bits, 2 successive pixels form 1 byte");
					break;
				case ScreenOptionsType.CharColourType.FCM:
					Logger.Verbose.Option("Each character is 8x8 pixels");
					Logger.Verbose.Option("Each pixel is 8 bits / 1 byte");
					break;
			}
			Logger.Verbose.Option("All pixels as palette indices");
			Logger.Verbose.Option("Top-to-down, left-to-right order");
			Logger.Verbose.Option($"Character size is {ScreenOptions.CharData.CharDataSize} bytes");

			var charData = Logger.Verbose.IsEnabled ? new List<byte>() : null;
			var formatter = Logger.Verbose.IsEnabled
				? new TableFormatter
				{
					IsHex = true,
					Headers = new[] { "Address", "Index", $"Data ({ScreenOptions.CharData.CharDataSize} bytes)" },
					Prefix = " $",
					Suffix = " "
				}
				: null;

			var charIndex = -1;
			foreach (var character in CharsContainer.Images)
			{
				charIndex++;

				var startingFilePosition = (int)writer.BaseStream.Position;

				charData?.Clear();
				formatter?.StartNewRow();

				for (var y = 0; y < character.IndexedImage.Height; y++)
				{
					switch (ScreenOptions.CharColour)
					{
						case ScreenOptionsType.CharColourType.NCM:
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

						case ScreenOptionsType.CharColourType.FCM:
							for (var x = 0; x < character.IndexedImage.Width; x++)
							{
								var colour = (byte)(character.IndexedImage[x, y] & 0xff);
								charData?.Add(colour);
								writer.Write(colour);
							}
							break;
					}
				}

				formatter?.AppendData(ScreenOptions.CharsBaseAddress + startingFilePosition);
				formatter?.AppendData(ScreenOptions.CharIndexInRam(charIndex));
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

	private void ExportPalette()
	{
		// TODO: we should move palette stream out of I/Os since it's shared between all
		if (ScreenOptions.InputsOutputs.Length == 0) return;

		CreateExporter("palette", ScreenOptions.InputsOutputs[0].OutputPaletteStream).Export(writer =>
		{
			new PaletteExporter().Export(
				palette: ExportData.Palette.Select(x => x.Colour).ToList(),
				writer: writer
			);
		});
	}

	private void ExportLayerInfos()
	{
		foreach (var io in ScreenOptions.InputsOutputs)
		{
			CreateExporter("layer info", io.OutputInfoDataStream).Export(writer =>
			{
				var charSize = ScreenOptions.CharData.PixelDataSize;

				var layerWidth = ExportData.Screen.Width;
				var layerHeight = ExportData.Screen.Height;
				var layerSizeChars = layerWidth * layerHeight;
				var layerSizeBytes = layerSizeChars * charSize;
				var layerRowSize = layerWidth * charSize;

				var screenColumns = new[] { 40, 80 };
				var screenCharColumns = new[]
				{
				ScreenOptions.CharData.CharsPerScreenWidth40Columns,
				ScreenOptions.CharData.CharsPerScreenWidth80Columns,
				};

				Logger.Verbose.Separator();
				Logger.Verbose.Message("Format (hex values in little endian):");

				// Character info.
				var formatter = Logger.Verbose.IsEnabled ? TableFormatter.CreateFileFormatter() : null;

				writer.Write((byte)charSize);
				formatter?.AddFileFormat(size: 1, value: charSize, description: "Character size in bytes");

				writer.Write((byte)0xff);
				formatter?.AddFileFormat(size: 1, value: 0xff, description: "Unused");

				// Layer info.
				formatter?.AddFileSeparator();

				writer.Write((ushort)layerWidth);
				formatter?.AddFileFormat(size: 2, value: layerWidth, description: "Layer width in characters");

				writer.Write((ushort)layerHeight);
				formatter?.AddFileFormat(size: 2, value: layerHeight, description: "Layer height in characters");

				writer.Write((ushort)layerRowSize);
				formatter?.AddFileFormat(size: 2, value: layerRowSize, description: "Layer row size in bytes (logical row size)");

				writer.Write((uint)layerSizeChars);
				formatter?.AddFileFormat(size: 4, value: layerSizeChars, description: "Layer size in characters (width * height)");

				writer.Write((uint)layerSizeBytes);
				formatter?.AddFileFormat(size: 4, value: layerSizeBytes, description: "Layer size in bytes (width * height * char size)");

				// Screen info.
				for (var i = 0; i < screenColumns.Length; i++)
				{
					var columns = screenColumns[i];
					var width = screenCharColumns[i];
					var height = ScreenOptions.CharData.CharsPerScreenHeight;  // height is always the same

					formatter?.AddFileSeparator();

					writer.Write((byte)width);
					formatter?.AddFileFormat(size: 1, value: width, description: $"Characters per {columns} column screen width");

					writer.Write((byte)height);
					formatter?.AddFileFormat(size: 1, value: height, description: "Characters per screen height");

					writer.Write((ushort)(width * charSize));
					formatter?.AddFileFormat(size: 2, value: width * charSize, description: "Screen row size in bytes");

					writer.Write((ushort)(width * height));
					formatter?.AddFileFormat(size: 2, value: width * height, description: "Screen size in characters");

					writer.Write((ushort)(width * height * charSize));
					formatter?.AddFileFormat(size: 2, value: width * height * charSize, description: "Screen size in bytes");
				}

				formatter?.Log(Logger.Verbose.Option);
			});
		}
	}

	private void ExportInfoImages()
	{
		if (ScreenOptions.InfoRenderingScale <= 0) return;

		foreach (var io in ScreenOptions.InputsOutputs)
		{
			CreateExporter("info image", io.OutputInfoImageStream).Prepare(stream =>
			{
				new CharsImageExporter
				{
					Scale = ScreenOptions.InfoRenderingScale,
					LayersData = ExportData,
					CharsContainer = CharsContainer,
					CharInfo = ScreenOptions.CharData,
					CharsBaseAddress = ScreenOptions.CharsBaseAddress
				}
				.Draw(stream);
			});
		}
	}

	private Exporter CreateExporter(string description, IStreamProvider provider)
	{
		return new()
		{
			LogDescription = description,
			Stream = provider
		};
	}

	#endregion
}
