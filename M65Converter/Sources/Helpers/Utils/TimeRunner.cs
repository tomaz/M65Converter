using System.Diagnostics;
using System.Text;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Embeds a task and logs the time needed to complete it.
/// </summary>
public class TimeRunner
{
	public Action<string> LoggerFunction { get; init; } = Logger.Debug.Message;

	public string Header { get; init; } = " ______________________________________________________________________________\r\n// ";
	public string? Title { get; init; }
	public string? Footer { get; init; }

	public void Run(Action action)
	{
		var watch = Stopwatch.StartNew();

		LoggerFunction(Title != null ? Header + Title : Header);

		action();

		watch.Stop();

		if (Footer != null)
		{
			var footer = Footer
				.Replace("{Time}", $"{watch.ElapsedMilliseconds}ms")
				.Replace("{Title}", Title != null ? Title : "");

			LoggerFunction(footer);
		}
		else
		{
			var timeText = new StringBuilder($"\\\\_{watch.ElapsedMilliseconds}ms");

			if (Title != null) timeText.Append($"_[{Title.Replace(" ", "_")}]");
			
			while (timeText.Length < 79) timeText.Append("_");
			
			LoggerFunction(timeText.ToString());
		}
	}
}
