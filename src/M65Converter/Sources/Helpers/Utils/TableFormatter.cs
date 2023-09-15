using System.Text;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Formats entries into a table layout. Each cell can either describe a value or a change.
/// </summary>
public class TableFormatter
{
	private readonly static string SeparatorRow = "----------";
	private readonly static string DoubleSeparatorRow = "==========";

	/// <summary>
	/// Optional left header value. If not provided, it remains empty.
	/// </summary>
	public string? LeftHeader { get; init; }

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

	private readonly List<RowData> rows = new();
	private int fileOffset = 0;

	#region Initialization & Disposal

	public static TableFormatter CreateFileFormatter()
	{
		return new()
		{
			LeftHeader = "Offset",
			Headers = new[] { "Size", "Hex", "Value", "Description" },
			Prefix = " ",
			Suffix = " ",
		};
	}

	#endregion

	#region Describing data

	/// <summary>
	/// Generates a string representing the given amount of hex chars.
	/// </summary>
	public static string PlaceholderHexValuesString(int count, int grouping = 1)
	{
		var groupCount = count / grouping;
		var group = string.Join(string.Empty, Enumerable.Repeat("XX", grouping));
		var chars = Enumerable.Repeat(group, groupCount);
		return string.Join(' ', chars);
	}

	/// <summary>
	/// Starts a new row. Must be called before appending data.
	/// </summary>
	/// <param name="description">Optional row description</param>
	public void StartNewRow(string? description = null)
	{
		rows.Add(new RowData
		{
			Description = description ?? FormattedLeftHeader(rows.Count)
		});
	}

	/// <summary>
	/// Adds a new integer data column, or change from original to new value.
	/// </summary>
	/// <param name="original">The value to represent in this column</param>
	/// <param name="modified">Optional modified data if this is describing a change</param>
	public void AppendData(int original, int? modified = null)
	{
		// Note the formatted value at this point doesn't take into account prefix and suffix. That's with purpose - we may need to pad the string so all change arrows are aligned which needs to happen before applying prefix.
		rows.Last().Columns.Add(FormattedData(original, modified));
	}

	/// <summary>
	/// Adds a new column to this row with an arbitrary string data.
	/// </summary>
	public void AppendString(string data)
	{
		rows.Last().Columns.Add(new Data
		{
			Value = data
		});
	}

	#endregion

	#region Describing file format

	/// <summary>
	/// Adds a row that's composed solely of the given text, without any columns.
	/// </summary>
	public void AddFileDescription(string description)
	{
		rows.Add(new RowData
		{
			Description = description,
			IsDescription = true
		});
	}

	/// <summary>
	/// Adds a row that describes a file format.
	/// </summary>
	/// <param name="size">The size of the data.</param>
	/// <param name="value">The integer value.</param>
	/// <param name="description">Data description</param>
	public void AddFileFormat(int size, int value, string description)
	{
		// We format is as hex string with enough digits to fit the given size.
		var hex = Number(hex: true, littleEndian: true, value: value, size: size * 2);
		AddFileFormat(size, $"{hex}", value.ToString(), description);
	}

	/// <summary>
	/// Adds a row that describes a file format.
	/// </summary>
	/// <param name="size">The size of the data.</param>
	/// <param name="hex">The value in the form of a hexadecimal string</param>
	/// <param name="value">The value in the form of a string.</param>
	/// <param name="description">Data description</param>
	public void AddFileFormat(int size, string hex, string value, string description)
	{
		// Left header is start-end offset.
		var leftHeader = size == 1
			? fileOffset.ToString()
			: $"{fileOffset}-{fileOffset + size - 1}";
		StartNewRow(leftHeader);

		// First column is size.
		AppendString($"{size}");

		// Second column is the value. We format is as hex string with enough digits to fit the given size.
		AppendString(hex);

		// Third column is value in decimal form.
		AppendString(value);

		// Last column is description.
		AppendString(description);

		fileOffset += size;
	}

	/// <summary>
	/// Adds the separator line, single or double lines.
	/// </summary>
	public void AddFileSeparator(bool isDouble = false)
	{
		StartNewRow(isDouble ? DoubleSeparatorRow : SeparatorRow);
	}

	#endregion

	#region Exporting

	/// <summary>
	/// Outputs the table using the given logger function.
	/// </summary>
	public void Log(Action<string> logger)
	{
		List<string> Headers()
		{
			// Prepares the list of all top headers.
			var result = new List<string>();

			// Prepares the array of top headers.
			if (rows.Count == 0) return result;

			// Prepare headers for all data columns.
			var line = rows[0];	// doesn't matter which line, we just need to enumerate all columns
			for (var i = 0; i < line.Columns.Count; i++)
			{
				var header = FormattedHeader(i);
				result.Add(header);
			}

			// Append left header (to the end so header indices match data).
			// Note: while other values in resulting list are actual headers meant for rendering, this value is just a placeholder!
			var longestRowDesc = LeftHeader ?? string.Empty;
			foreach (var row in rows)
			{
				var description = row.IsSeparator || row.IsDescription ? string.Empty : row.Description;
				if (description.Length > longestRowDesc.Length)
				{
					longestRowDesc = description;
				}
			}
			result.Add(longestRowDesc);

			return result;
		}

		List<ColumnLength> ColumnLenghts(List<string> headers)
		{
			var result = headers.Select(x => new ColumnLength()).ToList();

			// Calculate max length for data columns.
			for (var y = 0; y < rows.Count; y++)
			{
				var row = rows[y];

				for (var x = 0; x < row.Columns.Count; x++)
				{
					// For column lengths we take into account data prefix and suffix.
					var header = headers[x];
					var data = row.Columns[x];
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

		void FormatRowSeparator(List<ColumnLength> columnLengths, char separator = '-')
		{
			var builder = new StringBuilder();

			AppendValue(builder, columnLengths.Last().Column, "", separator);

			for (var x = 0; x < columnLengths.Count - 1; x++)
			{
				var columnLength = columnLengths[x].Column;
				AppendColSeparator(builder, '+');
				AppendValue(builder, columnLength, "", separator);
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
			AppendLeftHeader(builder, columnLengths, LeftHeader ?? string.Empty);

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
			for (var y = 0; y < rows.Count; y++)
			{
				var row = rows[y];

				if (row.IsSeparator)
				{
					FormatRowSeparator(columnLengths, row.IsSingleSeparator ? '-' : '=');
					continue;
				}

				if (row.IsDescription)
				{
					logger(row.Description);
					continue;
				}

				var builder = new StringBuilder();

				AppendLeftHeader(builder, columnLengths, row.Description);

				for (var x = 0; x < row.Columns.Count; x++)
				{
					var columnLength = columnLengths[x];
					var value = row.Columns[x];
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
		return Number(hex: IsHex, littleEndian: false, value: value, size: MinValueLength);
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

	private static string Number(bool hex, bool littleEndian, int value, int size)
	{
		var result = hex
			? value.ToString("X")
			: value.ToString();

		while (result.Length < size)
		{
			result = "0" + result;
		}

		if (littleEndian && size >= 4 && size % 2 == 0)
		{
			// We only convert endianess when we have sizes of 4, 6, 8 etc
			var temp = string.Empty;

			for (var i = 0; i < result.Length; i += 2)
			{
				var next = result.Substring(i, 2);
				temp = next + temp;
			}

			result = temp;
		}

		return result;
	}

	#endregion

	#region Declarations

	private class RowData
	{
		public bool IsSeparator { get => IsSingleSeparator || IsDoubleSeparator; }
		public bool IsSingleSeparator { get => Description == SeparatorRow; }
		public bool IsDoubleSeparator { get => Description == DoubleSeparatorRow; }
		public bool IsDescription { get; set; } = false;
		public string Description { get; set; } = null!;
		public List<Data> Columns { get; } = new();
	}

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
