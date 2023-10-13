namespace M65Converter.Sources.Data.Providers;

/// <summary>
/// Provides stream for an actual file or folder.
/// 
/// Note: if the object represents a folder, then <see cref="GetStream"/> returns an empty memory stream!
/// </summary>
public class FileStreamProvider : IStreamProvider
{
	/// <summary>
	/// The underlying file or folder.
	/// </summary>
	public FileInfo FileInfo { get; init; } = null!;

	private Stream? stream = null;

	#region IStreamProvider

	public Stream GetStream(FileMode mode)
	{
		if (IsFolder()) throw new InvalidDataException($"{FileInfo} represents a folder, not valid to get a stream");

		// Prepare the stream if this is the first time we are asked for it.
		if (stream == null)
		{
			switch (mode)
			{
				case FileMode.Create:
				case FileMode.CreateNew:
				case FileMode.OpenOrCreate:
				{
					// For write modes we should create the folder.
					var folder = IsFolder() ? FileInfo.FullName : Path.GetDirectoryName(FileInfo.FullName);
					Directory.CreateDirectory(folder!);

					// We should also delete existing file otherwise creating new will fail.
					if (!IsFolder() && File.Exists(FileInfo.FullName))
					{
						File.Delete(FileInfo.FullName);
					}
					break;
				}
			}

			// Open the stream. We also assign it to variable so we always use the same stream if asked multiple times.
			stream = File.Open(FileInfo.FullName, mode);
		}

		return stream;
	}

	public string GetFilename()
	{
		return FileInfo.FullName;
	}

	public long GetLength()
	{
		return FileInfo.Length;
	}

	public bool IsFolder()
	{
		// If path doesn't exist, attempting to get attributes will throw exception. In such case we assume this is output file, so we return false.
		try
		{
			var path = FileInfo.FullName;
			var attributes = File.GetAttributes(path);
			return ((attributes & FileAttributes.Directory) != 0);
		}
		catch
		{
			return false;
		}
	}

	#endregion
}
