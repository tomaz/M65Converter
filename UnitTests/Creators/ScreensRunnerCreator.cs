using M65Converter.Sources.Runners;

namespace UnitTests.Creators;

public class ScreensRunnerCreator : BaseRunnerCreator<ScreensRunner>
{
	protected override ScreensRunner CreateInstance()
	{
		return new ScreensRunner()
		{
			Data = Data
		};
	}
}
