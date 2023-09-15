using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports lookup tables for given sprite.
/// </summary>
public class SpriteLookupDataExporter : BaseExporter
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
		var rowSizeBytes = referenceRow.Count * Data.GlobalOptions.CharInfo.BytesPerCharIndex;
		var frameSizeBytes = referenceFrame.Chars.Count * rowSizeBytes;

		var formatter = Logger.Verbose.IsEnabled ? TableFormatter.CreateFileFormatter() : null;

		Logger.Verbose.Message("Format:");

		formatter?.AddFileFormat(size: 2, value: Sprite.Frames.Count, description: "Number of frames");
		writer.Write((ushort)Sprite.Frames.Count);

		formatter?.AddFileFormat(size: 2, value: frameSizeBytes, description: "Frame size in bytes");
		writer.Write((ushort)frameSizeBytes);

		formatter?.AddFileSeparator();
		formatter?.AddFileDescription("Frame durations in number of frames");

		var index = -1;
		foreach (var frame in Sprite.Frames)
		{
			index++;
			formatter?.AddFileFormat(
				size: 1, 
				value: frame.DurationFrames(), 
				description: $"Duration of frame {index}"
			);
		}

		formatter?.Log(Logger.Verbose.Option);
	}

	#endregion
}
