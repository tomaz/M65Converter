using M65Converter.Sources.Data.Providers;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Convenience wrapper for unified input files or folders handling.
/// 
/// The main reason for using this class is to automatically log all inputs in the same way.
/// </summary>
public class InputFilesHandler
{
	/// <summary>
	/// Optional title for console output.
	/// </summary>
	public string? Title { get; init; }

	/// <summary>
	/// The array of all sources - folders or files.
	/// </summary>
	public IStreamProvider[] Sources { get; set; } = null!;

	#region Public

	/// <summary>
	/// Runs the handler and calls the given action for each encountered file.
	/// </summary>
	public void Run(Action<int, IStreamProvider> handler)
	{
		var index = 0;

		foreach (var source in Sources)
		{
			Logger.Debug.Separator();

			var timerTitle = Title != null ? $"{Title} " : "";
			var filename = Path.GetFileName(source.GetFilename());

			new TimeRunner
			{
				Title = $"{timerTitle}{filename}"
			}
			.Run(() =>
			{
				Logger.Info.Message($"{source}");

				handler(index, source);

				index++;
			});
		}
	}

	#endregion
}
