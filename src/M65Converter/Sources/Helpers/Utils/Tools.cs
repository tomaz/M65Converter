namespace M65Converter.Sources.Helpers.Utils;

public class Tools
{
	/// <summary>
	/// Prepares a string from 1 or more lines of text.
	/// </summary>
	public static string MultilineString(params string[] lines)
	{
		return string.Join(Environment.NewLine, lines);
	}
}
