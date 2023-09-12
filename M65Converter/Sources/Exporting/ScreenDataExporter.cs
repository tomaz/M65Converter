using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports given screen to screen RAM data using the given binary writer.
/// </summary>
public class ScreenDataExporter : BaseExporter
{
	/// <summary>
	/// The screen to export.
	/// </summary>
	public ScreenData Screen { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"Expected to be copied to memory address ${Data.ScreenOptions.CharsBaseAddress:X}");
		Logger.Verbose.Option($"Char start index {Data.CharIndexInRam(0)} (${Data.CharIndexInRam(0):X})");
		Logger.Verbose.Option("All values as char indices");
		Logger.Verbose.Option($"Each char uses {Data.GlobalOptions.CharInfo.BytesPerWidth} bytes");
		Logger.Verbose.Option("Top-to-down, left-to-right order");

		var formatter = Logger.Verbose.IsEnabled
			? new TableFormatter
			{
				IsHex = true,
			}
			: null;

		for (var y = 0; y < Screen.Screen.Rows.Count; y++)
		{
			var row = Screen.Screen.Rows[y];

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
		Logger.Verbose.Message($"Exported layer (big endian hex char indices adjusted to base address ${Data.ScreenOptions.CharsBaseAddress:X}):");
		formatter?.Log(Logger.Verbose.Option);
	}

	#endregion
}
