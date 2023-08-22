using System.Diagnostics;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Embeds a task and logs the time needed to complete it.
/// </summary>
public class TimeRunner
{
	private string ReportPrefix { get; init; }

	public TimeRunner(string reportPrefix = "---> ")
	{
		ReportPrefix = reportPrefix;
	}

	public void Run(Action action)
	{
		var watch = Stopwatch.StartNew();

		action();

		watch.Stop();

		Logger.Info.Message($"{ReportPrefix}{watch.ElapsedMilliseconds}ms");
	}
}
