namespace M65Converter.Sources.Helpers.Utils;

public static class Extensions
{
	/// <summary>
	/// Returns swapped high and low nibble of this byte.
	/// </summary>
	/// <remarks>
	/// No sure why some values need to swap nibbles. Discovered this by accident with palette colours (read: misconfigured the byte and it was working). Then tried the same approach in other areas where the default values didn't produce expected results. Since I didn't find this mentioned in Mega 65 documentation, decided to prepare the value according to docs and then use this method to swap nibbles afterwards. This way it's very easy to find all the places where this "hack" is needed ¯\_(ツ)_/¯

	/// </remarks>
	public static byte SwapNibble(this byte value)
	{
		var hi = value >> 4;
		var low = (value & 0x0f);
		return (byte)((low << 4) | hi);
	}
}
