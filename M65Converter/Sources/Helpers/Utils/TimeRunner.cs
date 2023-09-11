using System.Diagnostics;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Embeds a task and logs the time needed to complete it.
/// </summary>
public class TimeRunner
{
	public static string[] SingleLineHeader = new[]
	{
		" ______________________________________________________________________________",
		"// {Title}"
	};

	public static string[] SingleLineFooter = new[]
	{
		"\\\\_{Time}_{UnderlinedTitle}{EndlingUnderlines}"
	};

	public static string[] DoubleLineHeader = new[]
	{
		" ==============================================================================",
		"// {Title}"
	};

	public static string[] DoubleLineFooter = new[]
	{
		"",
		"\\\\ {Time} [{Title}]",
		" =============================================================================="
	};

	/// <summary>
	/// The logger function to use for logging.
	/// </summary>
	public Action<string> LoggerFunction { get; init; } = Logger.Debug.Message;

	/// <summary>
	/// Header to use at the start of the timing. It can contain the following placeholders:
	/// - `{Title}` will be replaced by the title (if provided)
	/// </summary>
	public string[] Header { get; init; } = SingleLineHeader;

	/// <summary>
	/// Footer to append at the end of the timing. It can contain the following placeholders:
	/// - {Time} will be replaced by the measured time in ms
	/// - {Title} will be replaced by the title (if provided)
	/// - {UnderlinedTitle} will be replaced by the title where all spaces will be replaced by un underline
	/// - {EndlingUnderlines} will be replaced by underline until header line length is reached.
	/// </summary>
	public string[] Footer { get; init; } = SingleLineFooter;

	/// <summary>
	/// Optional title.
	/// </summary>
	public string? Title { get; init; }

	public void Run(Action action)
	{
		var watch = Stopwatch.StartNew();

		var usedTitle = Title ?? string.Empty;

		var longestHeaderLine = 0;
		foreach (var line in Header)
		{
			var formattedLine = line.Replace("{Title}", usedTitle);
			if (formattedLine.Length > longestHeaderLine) longestHeaderLine = formattedLine.Length;
			LoggerFunction(formattedLine);
		}

		try
		{
			action();
		}
		catch
		{
			// In case of error, re-throw the exception.
			throw;
		}
		finally
		{
			// Successful, or failed, we should log how much time it took.
			watch.Stop();

			foreach (var line in Footer)
			{
				var needsUnderlining = line.Contains("{EndlingUnderlines}");

				var formattedLine = line
					.Replace("{Time}", $"{watch.ElapsedMilliseconds}ms")
					.Replace("{Title}", usedTitle)
					.Replace("{UnderlinedTitle}", usedTitle.Replace(' ', '_'))
					.Replace("{EndlingUnderlines}", "");

				if (needsUnderlining)
				{
					while (formattedLine.Length < longestHeaderLine)
					{
						formattedLine += "_";
					}
				}

				LoggerFunction(formattedLine);
			}
		}
	}
}
