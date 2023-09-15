using M65Converter.Sources.Data.Intermediate.Helpers;
using UnitTests.Creators;

namespace UnitTests.Tests;

public class PaletteExportTests
{
	// Note: we could easily add 2 more bool parameters for chars/screens in single test, but that would make inline data far more complicated (16 possibilities). And this way test method name is more self-explanatory for each specific use case:
	// - just use base characters
	// - just use characters from screen data
	// - use both, base characters and additional ones from screens

	[Theory]
	[InlineData(CharColourMode.FCM)]
	[InlineData(CharColourMode.NCM)]
	public void Palette_ShouldExport_BaseOnlyCharacters(CharColourMode colour)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RunChars();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetPaletteDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Palette_ShouldExport_ScreenOnlyCharacters(CharColourMode colour, bool rrb)
	{
		// Note: even though we only create chars from screens, we still need to run chars runner otherwise no chars output is created and we can't validate the chars that were actually created (in other words, we need --out-palette option)

		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunChars(withInput: false)
			.RunScreens();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetPaletteDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Palette_ShouldExport_BaseAndScreenCharacters(CharColourMode chars, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(chars)
			.RasterRewriteBuffer(rrb)
			.RunChars()
			.RunScreens();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetPaletteDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Palette_ShouldExport_OnlyExportedIfCommandUsed(bool isCharsInputUsed)
	{
		// setup
		var testData = new TestDataCreator()
			.RunChars(withInput: isCharsInputUsed)
			.RunScreens();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetPaletteDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}
}