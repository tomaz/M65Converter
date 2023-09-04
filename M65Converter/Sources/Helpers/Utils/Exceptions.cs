namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Thrown when 4-bit palette validation fails.
/// </summary>
public class Invalid4BitPaletteException : Exception
{
	public Invalid4BitPaletteException(string message, Exception? inner = null) : base(message, inner)
	{
	}
}
