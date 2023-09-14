using M65Converter.Sources.Data.Intermediate;

using UnitTests.Creators;

namespace UnitTests.Tests;

public class LookupExportTests
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
	public void Lookup_ShouldExport_BaseCharactersOnly(CharColourMode colour, bool rrb)
	{
		// setup
		var data = new DataContainerCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunChars()
			.Get();

		// execute
		new Tests(data).Chars().Run();

		// verify
		var testDataCreator = new ResourcesCreator.ExpectedLookupCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunChars();
		var expectedData = testDataCreator.Get();
		var actualData = testDataCreator.GetActualData(data);
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Lookup_ShouldExport_ScreenCharactersOnly(CharColourMode colour, bool rrb)
	{
		// setup
		var data = new DataContainerCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunScreens()
			.Get();

		// execute
		new Tests(data).Screens().Run();

		// verify
		var testDataCreator = new ResourcesCreator.ExpectedLookupCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunScreens();
		var expectedData = testDataCreator.Get();
		var actualData = testDataCreator.GetActualData(data);
		Assert.Equal(expectedData, actualData);
	}

	[Theory]
	[InlineData(CharColourMode.FCM, false)]
	[InlineData(CharColourMode.FCM, true)]
	[InlineData(CharColourMode.NCM, false)]
	[InlineData(CharColourMode.NCM, true)]
	public void Lookup_ShouldExport_BaseAndScreenCharacters(CharColourMode colour, bool rrb)
	{
		// setup
		var data = new DataContainerCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunChars()
			.RunScreens()
			.Get();

		// execute
		new Tests(data).Chars().Screens().Run();

		// verify
		var testDataCreator = new ResourcesCreator.ExpectedLookupCreator()
			.Colour(colour)
			.RRB(rrb)
			.RunChars()
			.RunScreens();
		var expectedData = testDataCreator.Get();
		var actualData = testDataCreator.GetActualData(data);
		Assert.Equal(expectedData, actualData);
	}
}