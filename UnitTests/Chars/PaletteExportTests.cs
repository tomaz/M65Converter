using UnitTests.Creators;

namespace UnitTests.Chars;

public class PaletteExportTests
{
	[Theory]
	[InlineData(CharsType.FCM, false)]
	[InlineData(CharsType.FCM, true)]
	[InlineData(CharsType.NCM, false)]
	[InlineData(CharsType.NCM, true)]
	public void Palette_ShouldExportPalette(CharsType output, bool rrb)
	{
		// setup
		var runner = new CharsCreator()
			.OutputType(output)
			.RRB(rrb)
			.Get();

		// execute
		runner.Run();

		// verify
		var expectedData = ResourcesCreator.PaletteOutput()
			.Chars(output)
			.RRB(rrb)
			.Get();
		var actualData = runner.Options.InputsOutputs[0].OutputPaletteStream;
		Assert.Equal(expectedData, actualData);
	}
}