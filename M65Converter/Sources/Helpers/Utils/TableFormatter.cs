using System.Text;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Formats entries into a table layout. Each cell can either describe a value or a change.
/// </summary>
public class TableFormatter
{
	/// <summary>
	/// Optional header values. If not provided, column indexes will be used.
	/// </summary>
	public string[]? Headers { get; init; }

	/// <summary>
	/// Specifies whether data values should be formatted as hex numbers or decimals.
	/// </summary>
	public bool IsHex { get; init; } = false;

	/// <summary>
	/// Minimum formatted data value length; if smaller, the value will be prefixed with leading zeroes.
	/// </summary>
	public int MinValueLength { get; init; } = 0;

	/// <summary>
	/// Optional data value prefix string.
	/// </summary>
	public string Prefix { get; init; } = string.Empty;

	/// <summary>
	/// Optional data value suffix string.
	/// </summary>
	public string Suffix { get; init; } = string.Empty;

	private readonly List<List<Data>> lines = new();

	#region Describing data

	public void StartNewLine()
	{
		lines.Add(new List<Data>());
	}

	public void AppendData(int original, int? modified = null)
	{
		// Note the formatted value at this point doesn't take into account prefix and suffix. That's with purpose - we may need to pad the string so all change arrows are aligned which needs to happen before applying prefix.
		lines.Last().Add(FormattedData(original, modified));
	}

	#endregion

	#region Exporting

	public void Log(Action<string> logger)
	{
		List<string> Headers()
		{
			// Prepares the list of all top headers.
			var result = new List<string>();

			// Prepares the array of top headers.
			if (lines.Count == 0) return result;

			// Prepare headers for all data columns.
			var line = lines[0];	// doesn't matter which line, we just need to enumerate all columns
			for (var i = 0; i < line.Count; i++)
			{
				// Headers are always pre-formatted with prefix and suffix.
				var header = FormattedHeader(i);
				var formatted = PrefixedSuffixedValue(header);
				result.Add(formatted);
			}

			// Append left header (to the end so header indices match data). Left header is always composed of decimal numbers so we use the largest value so we'll later be able to calculate this column length.
			// Note: while other values in resulting list are actual headers meant for rendering, this value is just a placeholder!
			result.Add(FormattedLeftHeader(lines.Count - 1));

			return result;
		}

		List<ColumnLength> ColumnLenghts(List<string> headers)
		{
			var result = headers.Select(x => new ColumnLength()).ToList();

			// Calculate max length for data columns.
			for (var y = 0; y < lines.Count; y++)
			{
				var line = lines[y];

				for (var x = 0; x < line.Count; x++)
				{
					// For column lengths we take into account data prefix and suffix.
					var header = headers[x];
					var data = line[x];
					var length = result[x];

					var value = PrefixedSuffixedValue(data.Value);
					var columnLength = Math.Max(value.Length, header.Length);

					// Update maximum column length.
					if (columnLength > length.Column)
					{
						length.Column = columnLength;
					}

					// Update maximum original value length.
					if (data.OriginalLength > length.Original)
					{
						length.Original = data.OriginalLength;
					}

					// Update maximum modified value length.
					if (data.ModifiedLength > length.Modified)
					{
						length.Modified = data.ModifiedLength;
					}
				}
			}

			// Calculate left header size. We only need column length here.
			result[^1].Column = headers[^1].Length;

			return result;
		}

		void AppendColSeparator(StringBuilder builder, char separator = '|')
		{
			builder.Append(separator);
		}

		void FormatRowSeparator(List<ColumnLength> columnLengths)
		{
			var builder = new StringBuilder();

			AppendValue(builder, columnLengths.Last().Column, "", '-');

			for (var x = 0; x < columnLengths.Count - 1; x++)
			{
				var columnLength = columnLengths[x].Column;
				AppendColSeparator(builder, '+');
				AppendValue(builder, columnLength, "", '-');
			}

			logger(builder.ToString());
		}

		void AppendLeftHeader(StringBuilder builder, List<ColumnLength> columnLengths, string value)
		{
			var columnLength = columnLengths.Last();

			// Left header is right aligned.
			var originalLength = builder.Length;
			while (builder.Length - originalLength + value.Length < columnLength.Column)
			{
				builder.Append(' ');
			}

			builder.Append(value);
		}

		void AppendValue(StringBuilder builder, int columnLength, string value, char padding = ' ')
		{
			var originalLength = builder.Length;
			
			builder.Append(value);

			// Column data needs to be right padded to fill whole column length
			while (builder.Length - originalLength < columnLength)
			{
				builder.Append(padding);
			}
		}

		void FormatHeader(List<ColumnLength> columnLengths, List<string> headers)
		{
			var builder = new StringBuilder();

			// Append empty left header.
			AppendLeftHeader(builder, columnLengths, "");

			// Append all data column headers.
			for (var i = 0; i < columnLengths.Count - 1; i++)
			{
				var columnLength = columnLengths[i];
				var header = headers[i];

				AppendColSeparator(builder);
				AppendValue(builder, columnLength.Column, header);
			}

			// Render the header string.
			logger(builder.ToString());
		}

		void FormatData(List<ColumnLength> columnLengths)
		{
			for (var y = 0; y < lines.Count; y++)
			{
				var line = lines[y];
				var builder = new StringBuilder();

				AppendLeftHeader(builder, columnLengths, FormattedLeftHeader(y));

				for (var x = 0; x < line.Count; x++)
				{
					var columnLength = columnLengths[x];
					var value = line[x];
					var data = value.Value;

					// If this is a change, we should left pad the value so that the change indicator is aligned for the column. Note how we use original length to determine the amount of padding (we don't want to pad if modified value is the one that's lengthier).
					if (value.IsChange)
					{
						var requiredLength = columnLength.Original - Prefix.Length - Suffix.Length;
						var actualLength = value.OriginalLength;
						while (actualLength < requiredLength)
						{
							data = " " + data;
							actualLength++;
						}
					}

					AppendColSeparator(builder);
					AppendValue(builder, columnLength.Column, PrefixedSuffixedValue(data));
				}

				// Render the line.
				logger(builder.ToString());
			}
		}

		var headers = Headers();
		var columnLengths = ColumnLenghts(headers);
		FormatHeader(columnLengths, headers);
		FormatRowSeparator(columnLengths);
		FormatData(columnLengths);
	}

	#endregion

	#region Helpers

	private Data FormattedData(int original, int? modified = null)
	{
		var value = new StringBuilder();
		var originalValue = string.Empty;
		var modifiedValue = string.Empty;
		var isChange = false;

		if (modified == null || original == modified)
		{
			// We only have a single value.
			value.Append(FormattedValue(original));
		}
		else
		{
			// We have a change.
			isChange = true;
			originalValue = FormattedValue(original);
			modifiedValue = FormattedValue(modified.Value);
			value.Append(originalValue);
			value.Append('→');
			value.Append(modifiedValue);
		}

		return new Data
		{
			Value = value.ToString(),
			OriginalLength = originalValue.Length,
			ModifiedLength = modifiedValue.Length,
			IsChange = isChange
		};
	}

	private string FormattedValue(int value)
	{
		// Note: hex formatting will use big-endian. The reason is it can save 1 char in logs per value. However 1 char per column is multiplied by number of columns, so the overall "save" can be significant, mainly as it can be the difference for console line wrapping or not (no wrapping = much more readable at glance). The downside is the value will look different than the actual one (which is in fact saved as little-endian). For example: $801 only uses 3 letters ("$" is not logged) in big endian ("801"), but it would require 4 in little endian ("0108"). Can see this being argued, but so far I think the pros outweight the cons.
		// BUT: it's possible to circumvent and have little-endian format simply by reversing the bytes in the value!
		var result = IsHex
			? value.ToString("X")
			: value.ToString();

		while (result.Length < MinValueLength)
		{
			result = "0" + result;
		}

		return result;
	}

	private string FormattedHeader(int index)
	{
		// If header strings are provided, use that.
		if (Headers != null && index < Headers.Length)
		{
			return Headers[index];
		}

		// If number is used, it's always decimal, `IsHex` is only for cell values.
		return index.ToString();
	}

	private string FormattedLeftHeader(int value)
	{
		return value.ToString();
	}

	private string PrefixedSuffixedValue(string value)
	{
		return Prefix + value + Suffix;
	}

	#endregion

	#region Declarations

	private class Data
	{
		/// <summary>
		/// Specifies whether this data represents a change (true) or a single value (false).
		/// </summary>
		public bool IsChange { get; init; } = false;

		/// <summary>
		/// Original value length. Only if this is a change.
		/// </summary>
		public int OriginalLength { get; init; }

		/// <summary>
		/// Modified value length. Only if this is a change.
		/// </summary>
		public int ModifiedLength { get; init; }

		/// <summary>
		/// The actual formatted value for display.
		/// </summary>
		public string Value { get; init; } = null!;

		public override string ToString() => $"{Value}";
	}

	private class ColumnLength
	{
		public int Column { get; set; }
		public int Original { get; set; }
		public int Modified { get; set; }
	}

	#endregion
}
