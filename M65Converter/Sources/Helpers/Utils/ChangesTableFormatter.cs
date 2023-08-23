using System.Text;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Formats entries where a change from one value to another occurs into a tabular format.
/// </summary>
public class ChangesTableFormatter
{
	public bool IsHex { get; set; } = false;
	public int MinValueSize { get; set; } = 0;

	private List<List<Change>> lines = new();

	#region Describing data

	public void StartNewLine()
	{
		lines.Add(new List<Change>());
	}

	public void AppendNoChange(int original)
	{
		lines.Last().Add(new Change
		{
			MinValueSize = MinValueSize,
			IsHex = IsHex,
			Original = original,
			Modified = original,
		});
	}

	public void AppendChange(int from, int to)
	{
		lines.Last().Add(new Change
		{
			MinValueSize = MinValueSize,
			IsHex = IsHex,
			Original = from,
			Modified = to,
		});
	}

	#endregion

	#region Exporting

	public void ExportLines(Action<string> handler)
	{
		List<Change> MaxColumnLenghts()
		{
			// Calculates maximum string size for original and modified text for each column and left header.
			var result = new List<Change>();

			// Calculate sizes for all columns.
			for (var y = 0; y < lines.Count; y++)
			{
				var line = lines[y];

				for (var x = 0; x < line.Count; x++)
				{
					if (x >= result.Count)
					{
						result.Add(new()
						{
							IsData = false,
						});
					}

					var maxSize = result[x];
					var change = line[x];

					// If original text is larger than current max size, replace.
					if (change.OriginalText.Length > maxSize.Original)
					{
						maxSize.Original = change.OriginalText.Length;
					}

					// If modified text is larger than current max size, replace.
					if (change.ModifiedText.Length > maxSize.Modified)
					{
						maxSize.Modified = change.ModifiedText.Length;
					}
				}
			}

			// Sometimes top header may be larger than the content; we should adjust to accomodate the larger of the two values.
			for (var x = 0; x < result.Count; x++)
			{
				var maxSize = result[x];
				var topHeader = x.ToString();

				// If top header value is larger than current max size, replace.
				if (topHeader.Length > maxSize.DataSize)
				{
					maxSize.DataSize = topHeader.Length;
				}
			}

			// Calculate sizes for left header. This is simply the number of lines that we set as modified.
			// Note: we add this as the last entry so that column indices will match with data. Just something to keep in mind, it's not that important as we'll treat this separately when formatting anyway.
			result.Add(new Change
			{
				IsData = false,
				Original = 0,
				Modified = lines.Count.ToString().Length,
			});

			return result;
		}

		void AppendColSeparator(StringBuilder builder, char separator = '|')
		{
			builder.Append(separator);
		}

		void AppendChars(StringBuilder builder, string data, int size, char delimiter = ' ')
		{
			var startingLength = builder.Length;
			
			builder.Append(data);
			while (builder.Length - startingLength < size)
			{
				builder.Append(delimiter);
			}
		}

		void AppendDelimitedString(StringBuilder builder, string value, Change maxSize)
		{
			// We need left separator first. For first value this is between left header and value, otherwise between previous value and this one.
			AppendColSeparator(builder);

			// Append the value with padding.
			AppendChars(builder, value, maxSize.DataSize);
		}

		void AppendLeftHeader(List<Change> maxColumnLengths, StringBuilder builder, string? value = null)
		{
			var length = maxColumnLengths.Last().DataSize;
			var header = value != null ? value : "";
			AppendChars(builder, header, length);
		}

		void FormatHeader(List<Change> maxColumnLengths)
		{
			var builder = new StringBuilder();

			// First fill in left header. We don't add row number here, so we don't provide the value.
			AppendLeftHeader(maxColumnLengths, builder);

			// Then fill in all header values.
			for (var x = 0; x < maxColumnLengths.Count - 1; x ++)
			{
				var maxSize = maxColumnLengths[x];
				var header = x.ToString();

				// Render header value.
				AppendDelimitedString(builder, header, maxSize);

				// If header is larger than max size, adjust it.
				if (header.Length > maxSize.DataSize)
				{
					int i = 0;
				}
			}

			// Notify caller to render the header.
			handler(builder.ToString());
		}

		void FormatLineSeparator(List<Change> maxColumnLenghts)
		{
			var builder = new StringBuilder();

			AppendChars(builder, "", maxColumnLenghts.Last().DataSize, '-');

			for (var x = 0; x < maxColumnLenghts.Count - 1; x++)
			{
				var maxLen = maxColumnLenghts[x];
				AppendColSeparator(builder, '+');
				AppendChars(builder, "", maxLen.DataSize, '-');
			}

			handler(builder.ToString());
		}

		void FormatData(List<Change> maxColumnLengths)
		{
			for (var y = 0; y < lines.Count; y++)
			{
				var line = lines[y];
				var builder = new StringBuilder();

				AppendLeftHeader(maxColumnLengths, builder, y.ToString());

				for (var x = 0; x < line.Count; x++)
				{
					var change = line[x];
					var maxSize = maxColumnLengths[x];

					var valueBuilder = new StringBuilder();

					if (change.Original == change.Modified)
					{
						// There's no change.
						valueBuilder.Append(change.OriginalText);
					}
					else if (change.OriginalText.Length == 0)
					{
						// There's no real change, just a description of state.
						valueBuilder.Append(change.ModifiedText);
					}
					else
					{
						// Left pad original value so that all arrows will be vertically aligned.
						while (valueBuilder.Length < maxSize.OriginalText.Length - 1)
						{
							valueBuilder.Append(' ');
						}

						// Add original value.
						valueBuilder.Append(change.OriginalText);

						// Add arrow.
						valueBuilder.Append('→');

						// Add modified value.
						valueBuilder.Append(change.ModifiedText);
					}

					AppendDelimitedString(builder, valueBuilder.ToString(), maxSize);
				}

				// Notify caller to render the line.
				handler(builder.ToString());
			}
		}

		var maxColumnLengths = MaxColumnLenghts();
		FormatHeader(maxColumnLengths);
		FormatLineSeparator(maxColumnLengths);
		FormatData(maxColumnLengths);
	}

	#endregion

	#region Declarations

	private class Change
	{
		public bool IsHex { get; set; } = false;
		public bool IsData { get; set; } = true;
		public int MinValueSize { get; set; } = 0;
		public int Original { get; set; } = -1;
		public int Modified { get; set; }

		public string OriginalText
		{
			get
			{
				return Original >= 0 ? FormattedValue(Original) : "";
			}
		}

		public string ModifiedText
		{
			get
			{
				return IsData
					? Original != Modified ? FormattedValue(Modified) : ""
					: FormattedValue(Modified);
			}
		}

		public int DataSize
		{
			set
			{
				dataSize = value;
			}
			get
			{
				if (dataSize != null)
				{
					return dataSize.Value;
				}

				if (IsData)
				{
					var hasBothValues = OriginalText.Length > 0 && ModifiedText.Length > 0;
					return OriginalText.Length + ModifiedText.Length + (hasBothValues ? 1 : 0);
				}
				else
				{
					var hasBothValues = Original > 0 && Modified > 0;
					return Original + Modified + (hasBothValues ? 1 : 0);
				}
			}
		}
		private int? dataSize = null;

		public override string ToString() => $"{DataSize} ({Original}->{Modified})";

		private string FormattedValue(int value)
		{
			// Note: hex formatting will use big-endian. The reason is it can save 1 char in logs per value. However 1 char per column is multiplied by number of columns, so the overall "save" can be significant, mainly as it can be the difference for console line wrapping or not (no wrapping = much more readable at glance). The downside is the value will look different than the actual one (which is in fact saved as little-endian). For example: $801 only uses 3 letters ("$" is not logged) in big endian ("801"), but it would require 4 in little endian ("0108"). Can see this being argued, but so far I think the pros outweight the cons.
			var result = IsHex
				? value.ToString("X")
				: value.ToString();

			while (result.Length < MinValueSize)
			{
				result = "0" + result;
			}

			return result;
		}
	}

	#endregion
}
