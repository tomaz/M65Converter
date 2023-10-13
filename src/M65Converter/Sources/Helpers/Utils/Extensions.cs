using System.Text;

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

	/// <summary>
	/// Converts this value to string that can be used to measure maximum width.
	/// </summary>
	public static string ToMeasureString(this int value, char digit = '8')
	{
		var result = new StringBuilder();

		var text = value.ToString();
		for (var i = 0; i < text.Length; i++)
		{
			result.Append(digit);
		}

		return result.ToString();
	}

	/// <summary>
	/// Converts the given integer into little endian ordered list (LSB first, MSB last).
	/// </summary>
	public static List<byte> ToLittleEndianList(this int value, int maxLength = 2)
	{
		var result = new List<byte>();

		for (var i = 0; i < maxLength; i++)
		{
			result.Add((byte)(value & 0xff));
			value >>= 8;
		}

		return result;
	}

	/// <summary>
	/// Returns up to 4 bytes as little endian data. The array is expected to have little endian order - LSB first, MSB last.
	/// </summary>
	public static uint AsLittleEndianData(this byte[] data)
	{
		uint result = 0;

		// Values are already in little endian order.
		foreach (var value in data)
		{
			result <<= 8;
			result |= value;
		}

		return result;
	}

	/// <summary>
	/// Retruns up to 4 bytes as big endian data. The array is expected to have little endian order - LSB first, MSB last.
	/// </summary>
	public static uint AsBigEndianData(this byte[] data)
	{
		uint result = 0;

		// Values are little endian order, so we need to reverse the array.
		for (var i = data.Length - 1; i >= 0; i--)
		{
			result <<= 8;
			result |= data[i];
		}

		return result;
	}
}
