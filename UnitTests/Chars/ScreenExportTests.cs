using UnitTests.Creators;

namespace UnitTests.Chars;

public class ScreenExportTests
{
	[Theory]
	[InlineData(CharsType.FCM, false)]
	[InlineData(CharsType.FCM, true)]
	[InlineData(CharsType.NCM, false)]
	[InlineData(CharsType.NCM, true)]
	public void Screen_ShouldExportScreen(CharsType output, bool rrb)
	{
		// setup
		var runner = new CharsCreator()
			.OutputType(output)
			.RRB(rrb)
			.Get();

		// execute
		runner.Run();

		// verify
		var expectedData = ResourcesCreator.ScreenOutput()
			.Chars(output)
			.RRB(rrb)
			.Get();
		var actualData = runner.Options.InputsOutputs[0].OutputScreenStream;
		Assert.Equal(expectedData, actualData);
	}
}