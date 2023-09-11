using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
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
		var charSize = Data.GlobalOptions.CharInfo.PixelDataSize;

		var layerWidth = Screen.Screen.Width;
		var layerHeight = Screen.Screen.Height;
		var layerSizeChars = layerWidth * layerHeight;
		var layerSizeBytes = layerSizeChars * charSize;
		var layerRowSize = layerWidth * charSize;

		var screenColumns = new[] { 40, 80 };
		var screenCharColumns = new[]
		{
			Data.GlobalOptions.CharInfo.CharsPerScreenWidth40Columns,
			Data.GlobalOptions.CharInfo.CharsPerScreenWidth80Columns,
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
			var height = Data.GlobalOptions.CharInfo.CharsPerScreenHeight;  // height is always the same

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
	}

	#endregion
}
