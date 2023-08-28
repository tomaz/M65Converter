namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Convenience wrapper for unified input files or folders handling.
/// 
/// The main reason for using this class is to automatically log all inputs in the same way.
/// </summary>
public class InputFilesHandler
{
	/// <summary>
	/// The array of all sources - folders or files.
	/// </summary>
	public FileInfo[] Sources { get; set; } = null!;

	#region Public

	/// <summary>
	/// Runs the handler and calls the given action for each encountered file.
	/// </summary>
	public void Run(Action<FileInfo> handler)
	{
		foreach (var source in Sources)
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {source}");

			handler(source);
		}
	}

	#endregion
}
