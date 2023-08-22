namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Convenience input files handler for unified handling code and log output.
/// </summary>
public class InputFilesHandler
{
	public FileInfo[] InputFolders { get; set; } = null!;

	#region Public

	/// <summary>
	/// Runs the handler and calls the given action for each encountered file.
	/// </summary>
	public void Run(Action<FileInfo> handler)
	{
		foreach (var folder in InputFolders)
		{
			Logger.Debug.Separator();
			Logger.Info.Message($"---> {folder}");

			handler(folder);
		}
	}

	#endregion
}
