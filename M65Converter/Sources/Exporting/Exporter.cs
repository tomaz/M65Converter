using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports data to a single file.
/// 
/// The main responsibility of this class is to unify export folder and file handling and logging.
/// </summary>
public class Exporter
{
	/// <summary>
	/// Description for logging.
	/// </summary>
	public string LogDescription { get; init; } = null!;

	/// <summary>
	/// The path and filename to export to.
	/// </summary>
	public string Filename { get; init; } = null!;

	#region Exporting

	public void Export(Action<BinaryWriter> handler)
	{
		Logger.Verbose.Separator();
		Logger.Debug.Message($"Exporting {LogDescription} to {Path.GetFileName(Filename)}");
		Logger.Verbose.Message($"{Filename}");

		Directory.CreateDirectory(Path.GetDirectoryName(Filename)!);

		using var writer = new BinaryWriter(new FileStream(Filename, FileMode.Create));
		handler(writer);

		Logger.Debug.Message($"{writer.BaseStream.Length} bytes");
	}

	#endregion
}
