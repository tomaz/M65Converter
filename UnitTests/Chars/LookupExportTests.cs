using M65Converter.Sources.Data.Intermediate;

using UnitTests.Creators;

namespace UnitTests.Chars;

public class LookupExportTests
{
	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Lookup_ShouldExportTables(CharColourMode chars, bool rrb)
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
		};

		// execute
		runner.Get().Run();
		data.Get().ExportData();

		// verify
		var expectedDataCreator = new ResourcesCreator.LookupCreator
		{
			CharType = chars,
			IsRRBEnabled = rrb
		};
		var expectedData = expectedDataCreator.Get();
		var actualData = data.Get().UsedOutputStreams.LookupDataStreams[0];
		Assert.Equal(expectedData, actualData);
	}
}