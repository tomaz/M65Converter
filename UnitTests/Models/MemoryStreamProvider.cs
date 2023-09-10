using M65Converter.Sources.Data.Providers;

using Xunit.Sdk;

namespace UnitTests.Models;

/// <summary>
/// Provides stream onto data in memory.
/// 
/// Note: memory streams can only represent files.
/// </summary>
public class MemoryStreamProvider : IStreamProvider
{
	/// <summary>
	/// The underlying data (only used for input data).
	/// </summary>
	public byte[]? Data { get; init; }

	/// <summary>
	/// The name of the file this data represents.
	/// </summary>
	public string Filename { get; init; } = null!;

	private MemoryStream? stream = null;

	#region IStreamProvider

	public Stream GetStream(FileMode mode)
	{
		if (stream == null)
		{
			stream = new MemoryStream();

			// Data is null for outputs.
			if (Data != null)
			{
				stream.Write(Data, 0, Data.Length);
				stream.Position = 0;
			}
		}

		return stream!;
	}

	public string GetFilename()
	{
		return Filename; 
	}

	public long GetLength()
	{
		return stream != null ? stream.ToArray().Length : 0;
	}

	public bool IsFolder()
	{
		return false;
	}

	#endregion

	#region Overrides

	public override int GetHashCode()
	{
		return base.GetHashCode(); 
	}

	public override bool Equals(object? obj)
	{
		if (obj is MemoryStreamProvider other)
		{
			var ourData = GetData();
			var otherData = other.GetData();

			if (ourData == null && otherData != null) return false;
			if (ourData != null && otherData == null) return false;

			if (ourData == null && otherData == null) return true;

			// We do manual comparison so that we can output more meaningful errors.
			if (ourData!.Length != otherData!.Length)
			{
				throw new AssertActualExpectedException(
					expected: ourData.Length,
					actual: otherData.Length,
					userMessage: $"Expected data length {ourData.Length}, actual {otherData.Length}"
				);
				throw new AssertCollectionCountException(ourData.Length, otherData.Length);
			}

			for (var i = 0; i < ourData.Length; i++)
			{
				if (ourData[i] != otherData[i])
				{
					throw new AssertActualExpectedException(
						expected: ourData[i],
						actual: otherData[i],
						userMessage: $"Data on byte {i} is different: expected {ourData[i]}, actual {otherData[i]}"
					);
				}
			}

			return true;
		}

		return false;
	}

	public override string ToString()
	{
		var data = GetData();
		var dataString = data != null ? string.Join(',', data[..30]) : string.Empty;
		return $"MemoryStreamProvider {{ {Filename}, {data?.Length ?? 0}: [{dataString}...] }}";
	}

	#endregion

	#region Helpers

	private byte[]? GetData()
	{
		// If we have data pre-assigned, return that.
		if (Data != null) return Data;

		// If we have stream, get the data from it.
		if (stream != null) return stream.ToArray();

		// Otherwise we don't have data and return null.
		return null;
	}

	#endregion
}
