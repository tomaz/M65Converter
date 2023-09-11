using M65Converter.Sources.Data.Intermediate;

using UnitTests.Creators;

namespace UnitTests.Chars;

public class PaletteExportTests
{
	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Palette_ShouldExportPalette(CharColourMode chars, bool rrb)
	{
		// setup
		var data = new DataContainerCreator
		{
			CharType = chars,
			IsRRBEnabled = rrb,
		};
		var runner = new ScreensRunnerCreator
		{
			Data = data.Get(),
			CharType = chars,
			IsRRBEnabled = rrb
		};

		// execute
		runner.Get().Run();
		data.Get().ExportData();

		// verify
		var expectedDataCreator = new ResourcesCreator.PaletteCreator
		{
			CharType = chars,
			IsRRBEnabled = rrb
		};
		var expectedData = expectedDataCreator.Get();
		var actualData = data.Get().UsedOutputStreams.PaletteStreram;
		Assert.Equal(expectedData, actualData);
	}
}