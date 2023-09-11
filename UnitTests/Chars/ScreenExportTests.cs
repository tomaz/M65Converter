using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;

using UnitTests.Creators;

namespace UnitTests.Chars;

public class ScreenExportTests
{
	[Theory]
	[InlineData(ScreenOptionsType.CharColourType.FCM, false)]
	[InlineData(ScreenOptionsType.CharColourType.FCM, true)]
	[InlineData(ScreenOptionsType.CharColourType.NCM, false)]
	[InlineData(ScreenOptionsType.CharColourType.NCM, true)]
	public void Screen_ShouldExportScreen(ScreenOptionsType.CharColourType output, bool rrb)
	{
		// setup
		var data = new DataContainer();
		var runner = new ScreensRunnerCreator
		{
			Data = data,
			CharType = output,
			IsRRBEnabled = rrb,
		};

		// execute
		runner.Get().Run();
		data.ExportGeneratedData();

		// verify
		var expectedDataCreator = new ResourcesCreator.ScreenCreator
		{
			CharType = output,
			IsRRBEnabled = rrb,
		};
		var expectedData = expectedDataCreator.Get();
		var actualData = data.ScreenOptions.InputsOutputs[0].OutputScreenStream;
		Assert.Equal(expectedData, actualData);
	}
}