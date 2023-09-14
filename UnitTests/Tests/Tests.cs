using M65Converter.Sources.Data.Intermediate;

using UnitTests.Creators;

namespace UnitTests.Tests;

/// <summary>
/// Ensures all required methods are invoked depending on setup.
/// 
/// This way we only need to change <see cref="Run"/> method below if additional methods need to be called...
/// </summary>
public class Tests
{
	private DataContainer data;
	private bool isCharsEnabled = false;
	private bool isScreensEnabled = false;

	public Tests(DataContainer data)
	{
		this.data = data;
	}

	public Tests Chars(bool run = true)
	{
		isCharsEnabled = run;
		return this;
	}

	public Tests Screens(bool run = true)
	{
		isScreensEnabled = run;
		return this;
	}

	public void Run()
	{
		if (isCharsEnabled)
		{
			new CharsRunnerCreator { Data = data }.Get().Run();
		}

		if (isScreensEnabled)
		{
			new ScreensRunnerCreator { Data = data }.Get().Run();
		}

		data.GenerateData();
		data.ExportData();
	}
}
