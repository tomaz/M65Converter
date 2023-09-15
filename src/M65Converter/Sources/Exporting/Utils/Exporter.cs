using M65Converter.Sources.Data.Providers;
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
	/// Output stream provider.
	/// </summary>
	public IStreamProvider Stream { get; init; } = null!;

	private string Filename { get => Stream.GetFilename(); }

	#region Exporting

	/// <summary>
	/// Prepares everything for export and calls the given action with the stream provider to write to.
	/// </summary>
	public void Prepare(Action<IStreamProvider> handler)
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = $"Exporting {LogDescription} to {Path.GetFileName(Filename)}"
		}
		.Run(() =>
		{
			Logger.Verbose.Message($"{Filename}");

			handler(Stream);

			Logger.Debug.Message($"{Stream.GetLength():#,0} bytes");
		});
	}

	/// <summary>
	/// Prepares everything for export and calls the given action with the binary writer into which data can be written.
	/// </summary>
	public void Export(Action<BinaryWriter> handler)
	{
		Prepare(stream =>
		{
			using var writer = new BinaryWriter(stream.GetStream(FileMode.CreateNew));

			handler(writer);
		});
	}

	#endregion
}
