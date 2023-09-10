namespace M65Converter.Sources.Data.Providers;

/// <summary>
/// Provides a stream to consumers.
/// 
/// This way we can easily swap an actual file stream with a memory one and can thus form the basis for unit testing.
/// </summary>
public interface IStreamProvider
{
	/// <summary>
	/// Gets the stream that provides data for the consumer.
	/// </summary>
	Stream GetStream(FileMode mode);

	/// <summary>
	/// Returns the filename. This is mainly used to identify the type of the data from the stream.
	/// </summary>
	string GetFilename();

	/// <summary>
	/// Returns underlying stream length.
	/// </summary>
	/// <returns></returns>
	long GetLength();

	/// <summary>
	/// Specifies whether this stream provider represents a folder (true) or file (false). In case of folder, <see cref="GetStream"/> will throw an exception.
	/// </summary>
	bool IsFolder();
}
