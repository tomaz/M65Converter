using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports lookup tables for the given screen using the given binary writer.
/// </summary>
public class ScreenLookupExporter : BaseExporter
{
	/// <summary>
	/// The screen which lookup tables to export.
	/// </summary>
	public ScreenData Screen { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		var bytesPerCharWidth = Data.GlobalOptions.CharInfo.BytesPerWidth;
		var bytesPerChar = Data.GlobalOptions.CharInfo.BytesPerCharData;
		var charWidth = Data.GlobalOptions.CharInfo.Width;
		var charHeight = Data.GlobalOptions.CharInfo.Height;

		var screenWidth = Data.ScreenOptions.ScreenSize.Width;
		var screenHeight = Data.ScreenOptions.ScreenSize.Height;
		var screenSizeChars = screenWidth * screenHeight;
		var screenSizeBytes = screenSizeChars * bytesPerCharWidth;
		var screenCharsWidth = screenWidth * 8 / Data.GlobalOptions.CharInfo.Width;
		var screenRowSize = screenWidth * bytesPerCharWidth;
		var screenStartAddress = Data.ScreenOptions.ScreenBaseAddress;

		var layerWidth = Screen.Screen.Width;
		var layerHeight = Screen.Screen.Height;
		var layerSizeChars = layerWidth * layerHeight;
		var layerSizeBytes = layerSizeChars * bytesPerCharWidth;
		var layerRowSize = layerWidth * bytesPerCharWidth;

		Logger.Verbose.Separator();
		Logger.Verbose.Message("Format (hex values in little endian):");

		// Character info.
		var formatter = Logger.Verbose.IsEnabled ? TableFormatter.CreateFileFormatter() : null;

		writer.Write((byte)bytesPerCharWidth);
		formatter?.AddFileFormat(size: 1, value: bytesPerCharWidth, description: "Character width in bytes");

		writer.Write((byte)bytesPerChar);
		formatter?.AddFileFormat(size: 1, value: bytesPerChar, description: "Character data size in bytes");

		writer.Write((byte)charWidth);
		formatter?.AddFileFormat(size: 1, value: charWidth, description: "Character width in pixels");

		writer.Write((byte)charHeight);
		formatter?.AddFileFormat(size: 1, value: charHeight, description: "Character height in pixels");

		// Screen info.
		formatter?.AddFileSeparator();

		writer.Write((ushort)screenWidth);
		formatter?.AddFileFormat(size: 2, value: screenWidth, description: "Screen width in characters");

		writer.Write((ushort)screenHeight);
		formatter?.AddFileFormat(size: 2, value: screenHeight, description: "Screen height in characters");
		
		writer.Write((ushort)(screenCharsWidth));
		formatter?.AddFileFormat(size: 2, value: screenCharsWidth, description: "Number of character per screen width");

		writer.Write((ushort)screenRowSize);
		formatter?.AddFileFormat(size: 2, value: screenRowSize, description: "Number of bytes per screen width (logical size)");

		writer.Write((uint)screenSizeChars);
		formatter?.AddFileFormat(size: 4, value: screenSizeChars, description: "Screen size in characters (width * height)");

		writer.Write((uint)screenSizeBytes);
		formatter?.AddFileFormat(size: 4, value: screenSizeBytes, description: "Screen size in bytes (width * height * char size)");

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

		void PrintLookupTables(int layer, int column)
		{
			var baseAddress = screenStartAddress + layer * layerSizeBytes + column * bytesPerCharWidth;
			var shiftBits = 0;
			var descriptions = new string[] { "Byte 0 (LSB)", "Byte 1", "Byte 2 (MSB)" };
			var titlePrefix = column switch
			{
				0 => "Start of screen row lookup table: ",
				_ => $"Layer {layer} screen GOTOX lookup table: "
			};

			// Mega 65 only can have screen data anywhere in the first 384KB (0-$60000), so we need 3 bytes for these lookup tables.
			for (var i = 0; i < 3; i++)
			{
				formatter?.AddFileSeparator(isDouble: i == 0);
				formatter?.AddFileDescription($"{titlePrefix}{descriptions[i]}");

				// After first title, we change prefix so it's easier to distinguish between groups that describe the same value.
				titlePrefix = "";

				for (var y = 0; y < Screen.Screen.Rows.Count; y++)
				{
					// Calculate address for this byte.
					var address = baseAddress + y * layerRowSize;

					// Prepare full hex address and "emphasize" current byte in the string.
					var hexAddress = $"{address:X5}";
					var emphasisEnd = hexAddress.Length - shiftBits / 4;
					var emphasisStart = Math.Max(0, emphasisEnd - 2);
					hexAddress = hexAddress.Insert(emphasisEnd, "|");
					hexAddress = hexAddress.Insert(emphasisStart, "|");

					// Prepare the value for current byte only and write it.
					var shifted = (address >> shiftBits) & 0xff;
					writer.Write((byte)shifted);
					formatter?.AddFileFormat(size: 1, value: shifted, description: $"Row {y} (${hexAddress})");
				}

				shiftBits += 8;
			}
		}

		var sampleRow = Screen.Screen.Rows[0];
		var layerIndex = 0;
		for (var x = 0; x < sampleRow.Columns.Count; x++)
		{
			var column = sampleRow.Columns[x];

			switch (column.Type)
			{
				case ScreenData.Column.DataType.FirstData:
				case ScreenData.Column.DataType.Attribute:
					PrintLookupTables(layerIndex, x);
					layerIndex++;
					break;
			}
		}

		formatter?.Log(Logger.Verbose.Option);
	}

	#endregion
}
