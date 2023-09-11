using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;

using UnitTests.Creators;

namespace UnitTests.Chars;

public class ColourExportTests
{
	[Theory]
	[InlineData(ScreenOptionsType.CharColourType.FCM, false)]
	[InlineData(ScreenOptionsType.CharColourType.FCM, true)]
	[InlineData(ScreenOptionsType.CharColourType.NCM, false)]
	[InlineData(ScreenOptionsType.CharColourType.NCM, true)]
	public void Colour_ShouldExportColour(ScreenOptionsType.CharColourType chars, bool rrb)
	{
		// setup
		var data = new DataContainer();
		var runner = new ScreensRunnerCreator
		{
			Data = data,
			CharType = chars,
			IsRRBEnabled = rrb
		};

		// execute
		runner.Get().Run();
		data.ExportGeneratedData();

		// verify
		var expectedDataCreator = new ResourcesCreator.ColourCreator
		{
			CharType = chars,
			IsRRBEnabled = rrb
		};
		var expectedData = expectedDataCreator.Get();
		var actualData = data.ScreenOptions.InputsOutputs[0].OutputColourStream;
		Assert.Equal(expectedData, actualData);
	}
}