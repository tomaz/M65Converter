using UnitTests.Creators;

namespace UnitTests.Chars;

public class ColourExportTests
{
	[Theory]
	[InlineData(CharsType.FCM, false)]
	[InlineData(CharsType.FCM, true)]
	[InlineData(CharsType.NCM, false)]
	[InlineData(CharsType.NCM, true)]
	public void Colour_ShouldExportColour(CharsType output, bool rrb)
	{
		// setup
		var runner = new CharsCreator()
			.OutputType(output)
			.RRB(rrb)
			.Get();

		// execute
		runner.Run();

		// verify
		var expectedCharsData = ResourcesCreator.ColourOutput()
			.Chars(output)
			.RRB(rrb)
			.Get();
		var actualCharsData = runner.Options.InputsOutputs[0].OutputColourStream;
		Assert.Equal(expectedCharsData, actualCharsData);
	}
}