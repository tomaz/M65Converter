
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
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
	public ScreenData Screen { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"Each value uses {Data.GlobalOptions.CharInfo.PixelDataSize} bytes");
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
