namespace M65Converter.Sources.Data.Intermediate.Helpers;

/// <summary>
/// Provides information about characters.
/// </summary>
public class CharInfo
{
	/// <summary>
	/// Width of the character in pixels.
	/// </summary>
	public int Width { get; init; }

	/// <summary>
	/// Height of character in pixels.
	/// </summary>
	public int Height { get; init; }

	/// <summary>
	/// Char size in pixels.
	/// </summary>
	public Size Size { get => new(Width, Height); }

	/// <summary>
	/// Number of bytes each character requires in screen/colour data.
	/// </summary>
	public int BytesPerCharIndex { get; init; }

	/// <summary>
	/// Number of bytes each character (aka all pixels) require.
	/// </summary>
	public int BytesPerCharData { get; init; }

	/// <summary>
	/// Number of colours each character can have.
	/// </summary>
	public int ColoursPerChar { get; init; }

	/// <summary>
	/// Screen height in characters.
	/// </summary>
	public int CharsPerScreenHeight { get => 25; }

	#region Overrides

	public override string ToString() => $"{Width}x{Height}, {ColoursPerChar} colours per char";

	#endregion

	#region Helpers

	/// <summary>
	/// Creates an instance based on the given colour mode.
	/// </summary>
	public static CharInfo FromColourMode(CharColourMode mode)
	{
		return mode switch
		{
			CharColourMode.FCM => new CharInfo
			{
				Width = 8,
				Height = 8,
				BytesPerCharIndex = 2,
				BytesPerCharData = 64,
				ColoursPerChar = 256
			},

			CharColourMode.NCM => new CharInfo
			{
				Width = 16,
				Height = 8,
				BytesPerCharIndex = 2,
				BytesPerCharData = 64,
				ColoursPerChar = 16
			},

			_ => throw new ArgumentException($"Unsupported colour mode {mode}")
		};
	}

	/// <summary>
	/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
	/// </summary>
	public int CharIndexInRam(int baseAddress, int relativeIndex)
	{
		return (baseAddress + relativeIndex * BytesPerCharData) / BytesPerCharData;
	}

	#endregion
}
