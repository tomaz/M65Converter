using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports the given characters to format suitable for Mega 65 hardware.
/// </summary>
public class CharsExporter : BaseExporter
{
	/// <summary>
	/// The characters to export.
	/// </summary>
	public ImagesContainer Chars { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"{Chars.Images.Count} characters");
		switch (Data.GlobalOptions.ColourMode)
		{
			case CharColourMode.NCM:
				Logger.Verbose.Option("Each character is 16x8 pixels");
				Logger.Verbose.Option("Each pixel is 4 bits, 2 successive pixels form 1 byte");
				break;
			case CharColourMode.FCM:
				Logger.Verbose.Option("Each character is 8x8 pixels");
				Logger.Verbose.Option("Each pixel is 8 bits / 1 byte");
				break;
		}
		Logger.Verbose.Option("All pixels as palette indices");
		Logger.Verbose.Option("Top-to-down, left-to-right order");
		Logger.Verbose.Option($"Character size is {Data.GlobalOptions.CharInfo.CharDataSize} bytes");

		var charData = Logger.Verbose.IsEnabled ? new List<byte>() : null;
		var formatter = Logger.Verbose.IsEnabled
			? new TableFormatter
			{
				IsHex = true,
				Headers = new[] { "Address", "Index", $"Data ({Data.GlobalOptions.CharInfo.CharDataSize} bytes)" },
				Prefix = " $",
				Suffix = " "
			}
			: null;

		var charIndex = -1;
		foreach (var character in Chars.Images)
		{
			charIndex++;

			var startingFilePosition = (int)writer.BaseStream.Position;

			charData?.Clear();
			formatter?.StartNewRow();

			for (var y = 0; y < character.IndexedImage.Height; y++)
			{
				switch (Data.GlobalOptions.ColourMode)
				{
					case CharColourMode.NCM:
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

					case CharColourMode.FCM:
						for (var x = 0; x < character.IndexedImage.Width; x++)
						{
							var colour = (byte)(character.IndexedImage[x, y] & 0xff);
							charData?.Add(colour);
							writer.Write(colour);
						}
						break;
				}
			}

			formatter?.AppendData(Data.ScreenOptions.CharsBaseAddress + startingFilePosition);
			formatter?.AppendData(Data.CharIndexInRam(charIndex));
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
	}

	#endregion
}
