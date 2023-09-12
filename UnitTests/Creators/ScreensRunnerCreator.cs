using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Runners;

namespace UnitTests.Creators;

public class ScreensRunnerCreator
{
	public DataContainer Data { get; init; } = null!;

	private ScreensRunner? runner;

	public ScreensRunner Get()
	{
		runner ??= new ScreensRunner()
		{
			Data = Data
		};

		return runner;
	}
}
