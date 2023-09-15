using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Intermediate.Containers;

/// <summary>
/// Contains all post-processed data that needs to be exported for a single sprite.
/// </summary>
public class SpriteExportData
{
	/// <summary>
	/// Name of this sprite.
	/// </summary>
	public string SpriteName { get; set; } = null!;

	/// <summary>
	/// Width of the individual frame in number of characters.
	/// </summary>
	public int CharactersWidth { get; init; }

	/// <summary>
	/// Height of the individual frame in number of characters.
	/// </summary>
	public int CharactersHeight { get; init; }

	/// <summary>
	/// Gets the number of all characters in all frames.
	/// </summary>
	public int CharactersCount { get => CharactersWidth * CharactersHeight * Frames.Count; }

	/// <summary>
	/// The list of all frames data.
	/// </summary>
	public List<FrameData> Frames { get; } = new();

	/// <summary>
	/// Gets the character using absolute index accross all frames.
	/// 
	/// Frames are expected to be layed out horizontally.
	/// </summary>
	public CharData this[int x, int y]
	{
		get
		{
			var frameIndex = x / CharactersWidth;
			var charIndex = x % CharactersWidth;
			var frame = Frames[frameIndex];
			return frame.AbsoluteChar(charIndex, y);
		}
	}

	/// <summary>
	/// Determines if the given absolute character index represents a character in the first row of any of the frames.
	/// </summary>
	public bool IsFirstColumnOfFrame(int x)
	{
		return x % CharactersWidth == 0;
	}

	#region Declarations

	public class FrameData
	{
		/// <summary>
		/// Duration in milliseconds.
		/// </summary>
		public int Duration { get; init; }

		/// <summary>
		/// The top row of transparent chars in the form of char indices, 1 per each column.
		/// </summary>
		public List<CharData> StartingTransparentChars { get; } = new();

		/// <summary>
		/// The bottom row of transparent chars in the form of char indices, 1 per each column.
		/// </summary>
		public List<CharData> EndingTransparentChars { get; } = new();

		/// <summary>
		/// All of the frame characters in the form of list of rows where each row is a list of char indices, 1 per each column.
		/// </summary>
		public List<List<CharData>> Chars { get; } = new();

		/// <summary>
		/// Returns the character at the given index, including top starting and ending transparent charaneters.
		/// </summary>
		public CharData AbsoluteChar(int x, int y)
		{
			if (y == 0)
			{
				return StartingTransparentChars[x];
			}
			else if (y >= Chars.Count + 1)
			{
				return EndingTransparentChars[x];
			}
			else
			{
				return Chars[y - 1][x];
			}
		}

		/// <summary>
		/// Calculates duration of this frame in number of frames assuming the given frames per second rate.
		/// </summary>
		public int DurationFrames(int fps = 50)
		{
			return Duration * fps / 1000;
		}
	}

	public class CharData
	{
		/// <summary>
		/// Char zero based index.
		/// </summary>
		public int CharIndex { get; init; }

		/// <summary>
		/// The address of the char in char memory.
		/// </summary>
		public int CharAddress { get; init; }

		/// <summary>
		/// All bytes of the data.
		/// </summary>
		public List<byte> Values { get; init; } = new();

		/// <summary>
		/// Returns all values as single little endian value (only supports up to 4 bytes!)
		/// </summary>
		public int LittleEndianData { get => (int)Values.ToArray().AsLittleEndianData(); }

		/// <summary>
		/// Returns all values as single big endian value (only supports up to 4 digits!)
		/// </summary>
		public int BigEndianData { get => (int)Values.ToArray().AsBigEndianData(); }
	}

	#endregion
}
