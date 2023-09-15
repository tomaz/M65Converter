using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Helpers.Utils;

using System.Text;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports given sprite frames data using the given binary writer.
/// </summary>
public class SpriteDataExporter : BaseExporter
{
	/// <summary>
	/// The sprite to export.
	/// </summary>
	public SpriteExportData Sprite { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		var referenceFrame = Sprite.Frames.First();
		var referenceRow = referenceFrame.Chars.First();
		var rowsCount = referenceFrame.Chars.Count;
		var rowSizeBytes = referenceRow.Count * Data.GlobalOptions.CharInfo.BytesPerCharIndex;

		var charOffset = 0;

		string FrameRowString()
		{
			var result = new StringBuilder();

			for (var i = 0; i < referenceRow.Count; i++)
			{
				if (result.Length > 0) result.Append(' ');
				result.Append($"{charOffset:0000}");
				charOffset++;
			}

			return result.ToString();
		}

		var frameFormatter = Logger.Verbose.IsEnabled ? TableFormatter.CreateFileFormatter() : null;
		frameFormatter?.AddFileFormat(size: rowSizeBytes, hex: FrameRowString(), value: "/", description: "Top transparent char indices");
		frameFormatter?.AddFileSeparator();
		for (var i = 0; i < rowsCount; i++)
		{
			frameFormatter?.AddFileFormat(size: rowSizeBytes, hex: FrameRowString(), value: "/", description: $"Data row {i}");
		}
		frameFormatter?.AddFileSeparator();
		frameFormatter?.AddFileFormat(size: rowSizeBytes, hex: FrameRowString(), value: "/", description: "Bottom transparent char indices");

		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"Chars expected starting on memory address ${Data.GlobalOptions.CharsBaseAddress:X}");
		Logger.Verbose.Option($"Frame row has {referenceRow.Count} characters");
		Logger.Verbose.Option($"Row logical size {rowSizeBytes} bytes");
		Logger.Verbose.Option($"Each char uses {Data.GlobalOptions.CharInfo.BytesPerCharIndex} bytes");
		Logger.Verbose.Option("Each frame format:");
		frameFormatter?.Log(Logger.Verbose.SubOption);
		Logger.Verbose.SubOption("All values as char addresses (table above uses offsets into individual frame instead)");
		Logger.Verbose.SubOption("Values order in file is top-to-bottom, left-to-right");
		Logger.Verbose.SubOption("Next frame data immediately after previous one");

		void WriteRow(List<SpriteExportData.CharData> row)
		{
			foreach (var ch in row)
			{
				foreach (var value in ch.Values)
				{
					writer.Write(value);
				}
			}
		}

		foreach (var frame in Sprite.Frames)
		{
			WriteRow(frame.StartingTransparentChars);

			foreach (var row in frame.Chars)
			{
				WriteRow(row);
			}

			WriteRow(frame.EndingTransparentChars);
		}
	}

	#endregion
}
