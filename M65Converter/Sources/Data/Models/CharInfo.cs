namespace M65Converter.Sources.Data.Models;

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
	/// Number of bytes each pixel requires.
	/// </summary>
	public int PixelDataSize { get; init; }

	/// <summary>
	/// Number of bytes each character (aka all pixels) require.
	/// </summary>
	public int CharDataSize { get; init; }

	/// <summary>
	/// Number of colours each character can have.
	/// </summary>
	public int ColoursPerChar { get; init; }

	/// <summary>
	/// Number of characters that can be rendered in each line when 80 column mode is used.
	/// </summary>
	public int CharsPerScreenWidth80Columns { get; init; }

	/// <summary>
	/// Number of characters that can be rendered in each line when 40 column mode is used.
	/// </summary>
	public int CharsPerScreenWidth40Columns { get => CharsPerScreenWidth80Columns / 2; }

	/// <summary>
	/// Screen height in characters.
	/// </summary>
	public int CharsPerScreenHeight { get => 25; }

	#region Helpers

	/// <summary>
	/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
	/// </summary>
	public int CharIndexInRam(int baseAddress, int relativeIndex)
	{
		return (baseAddress + relativeIndex * CharDataSize) / CharDataSize;
	}

	#endregion
}
