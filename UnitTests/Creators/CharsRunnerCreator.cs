using M65Converter.Sources.Runners;

namespace UnitTests.Creators;

public class CharsRunnerCreator : BaseRunnerCreator<CharsRunner>
{
	protected override CharsRunner CreateInstance()
	{
		return new CharsRunner()
		{
			Data = Data
		};
	}
}
