namespace M65Converter.Sources.Helpers.Utils;

public static class Extensions
{
	/// <summary>
	/// Returns swapped high and low nibble of this byte.
	/// </summary>
	public static byte SwapNibble(this byte value)
	{
		var hi = value >> 4;
		var low = (value & 0x0f);
		return (byte)((low << 4) | hi);
	}
}
