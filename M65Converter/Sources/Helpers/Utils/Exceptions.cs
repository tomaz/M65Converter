namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Thrown when palette validation fails - aka too many colours.
/// </summary>
public class InvalidCompositeImageDataException : Exception
{
	public InvalidCompositeImageDataException(string message, Exception? inner = null) : base(message, inner)
	{
	}
}
