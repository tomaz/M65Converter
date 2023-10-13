using M65Converter.Sources.Data.Intermediate.Helpers;
using UnitTests.Creators;

namespace UnitTests.Tests;

public class ScreenExportTests
{
	// Note: we could easily add 2 more bool parameters for chars/screens in single test, but that would make inline data far more complicated (16 possibilities). And this way test method name is more self-explanatory for each specific use case:
	// - just use base characters
	// - just use characters from screen data
	// - use both, base characters and additional ones from screens

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Screen__ShouldExport__Base(CharColourMode colour, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunChars();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetScreenDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Screen__ShouldExport__Screen(CharColourMode colour, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunScreens();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetScreenDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Screen__ShouldExport__Screen_Sprites(CharColourMode colour, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunScreens()
			.RunRRBSprites();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetScreenDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Screen__ShouldExport__Base_Screen(CharColourMode colour, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunChars()
			.RunScreens();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetScreenDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Screen__ShouldExport__Base_Screen_Sprites(CharColourMode colour, bool rrb)
	{
		// setup
		var testData = new TestDataCreator()
			.Colour(colour)
			.RasterRewriteBuffer(rrb)
			.RunChars()
			.RunScreens()
			.RunRRBSprites();

		// execute
		testData.GetDataContainerCreator().Get().Run();

		// verify
		var testDataCreator = testData.GetScreenDataCreator();
		var expectedData = testDataCreator.GetExpectedData();
		var actualData = testDataCreator.GetActualData();
		Assert.Equal(expectedData, actualData);
	}
}