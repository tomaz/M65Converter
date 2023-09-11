using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Runners;

namespace UnitTests.Creators;

public class ScreensRunnerCreator
{
	public DataContainer Data { get; init; } = null!;
	public CharColourMode CharType { get; init; } = CharColourMode.FCM;
	public int CharsBaseAddress { get; init; } = 0x20000;
	public bool IsRRBEnabled { get; init; } = false;

	private ScreensRunner? runner;

	public ScreensRunner Get()
	{
		if (runner == null)
		{
			Data.ScreenOptions = new ScreenOptions
			{
				Inputs = new[] { ResourcesCreator.CharsInput() },
				CharsBaseAddress = CharsBaseAddress,
				IsRasterRewriteBufferSupported = IsRRBEnabled,
			};

			runner = new ScreensRunner()
			{
				Data = Data
			};
		}

		return runner;
	}
}
