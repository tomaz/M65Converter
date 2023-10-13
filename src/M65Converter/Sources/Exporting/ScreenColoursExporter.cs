using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports the given screen colour RAM data using the given binary writer.
/// </summary>
public class ScreenColoursExporter : BaseExporter
{
	/// <summary>
	/// The screen which colours to export.
	/// </summary>
	public ScreenExportData Screen { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"Expected to be copied to colour ram memory (default is at $ff80000)");
		Logger.Verbose.Option($"Row character size {Screen.Screen.Rows.First().Columns.Count} characters");
		Logger.Verbose.Option($"Row logical size {Screen.Screen.Rows.First().Columns.Count * Data.GlobalOptions.CharInfo.BytesPerCharIndex} bytes");
		Logger.Verbose.Option($"Each value uses {Data.GlobalOptions.CharInfo.BytesPerCharIndex} bytes");
		Logger.Verbose.Option("Top-to-down, left-to-right order");

		var formatter = Logger.Verbose.IsEnabled
			? new TableFormatter
			{
				IsHex = true,
				MinValueLength = 4,
			}
			: null;

		for (var y = 0; y < Screen.Colour.Rows.Count; y++)
		{
			var row = Screen.Colour.Rows[y];

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
	}

	#endregion
}
