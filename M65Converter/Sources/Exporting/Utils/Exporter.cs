using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting.Utils;

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

	/// <summary>
	/// Prepares everything for export and calls the given action with the full path to the expected output file.
	/// </summary>
	public void Prepare(Action<string> handler)
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = $"Exporting {LogDescription} to {Path.GetFileName(Filename)}"
		}
		.Run(() =>
		{
			Logger.Verbose.Message($"{Filename}");

			Directory.CreateDirectory(Path.GetDirectoryName(Filename)!);

			handler(Filename);

			Logger.Debug.Message($"{new FileInfo(Filename).Length:#,0} bytes");
		});
	}

	/// <summary>
	/// Prepares everything for export and calls the given action with the binary writer into which data can be written.
	/// </summary>
	public void Export(Action<BinaryWriter> handler)
	{
		Prepare(path =>
		{
			using var writer = new BinaryWriter(new FileStream(Filename, FileMode.Create));
			handler(writer);
		});
	}

	#endregion
}
