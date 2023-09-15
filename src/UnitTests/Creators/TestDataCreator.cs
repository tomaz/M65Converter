using M65Converter.Sources.Data.Intermediate.Helpers;
using UnitTests.Creators.Inputs;
using UnitTests.Creators.Outputs;

namespace UnitTests.Creators;

public class TestDataCreator
{
	public CharColourMode ColourMode { get; private set; }
	
	public bool IsRRBEnabled { get; private set; }
	
	public bool IsCharsRunnerEnabled { get; private set; }
	public bool IsCharsInputUsed { get; private set; }

	public bool IsScreensRunnerEnabled { get; private set; }
	public bool IsScreensInputUsed { get; private set; }

	private DataContainerCreator? dataContainerCreator;

	#region Builder

	public TestDataCreator Colour(CharColourMode mode)
	{
		ColourMode = mode;
		return this;
	}

	public TestDataCreator RasterRewriteBuffer(bool use = true)
	{
		IsRRBEnabled = use;
		return this;
	}

	public TestDataCreator RunChars(bool run = true, bool withInput = true)
	{
		IsCharsRunnerEnabled = run;
		IsCharsInputUsed = withInput;
		return this;
	}

	public TestDataCreator RunScreens(bool run = true, bool withInput = true)
	{
		IsScreensRunnerEnabled = run;
		IsScreensInputUsed = withInput;
		return this;
	}

	#endregion

	#region Getting data

	public DataContainerCreator GetDataContainerCreator()
	{
		dataContainerCreator ??= new DataContainerCreator
		{
			TestData = this
		};

		return dataContainerCreator;
	}

	public InputBaseCharsDataCreator GetInputBaseCharsDataCreator()
	{
		return new InputBaseCharsDataCreator
		{
			TestData = this
		};
	}

	public InputScreensDataCreator GetInputScreensDataCreator()
	{
		return new InputScreensDataCreator
		{
			TestData = this
		};
	}

	public BaseOutputDataCreator GetCharsDataCreator()
	{
		return new OutputCharsDataCreator
		{
			TestData = this
		};
	}

	public BaseOutputDataCreator GetPaletteDataCreator()
	{
		return new OutputPaletteDataCreator
		{
			TestData = this
		};
	}

	public BaseOutputDataCreator GetScreenDataCreator()
	{
		return new OutputScreenDataCreator
		{
			TestData = this
		};
	}

	public BaseOutputDataCreator GetColourDataCreator()
	{
		return new OutputColourDataCreator
		{
			TestData = this
		};
	}

	public BaseOutputDataCreator GetLookupDataCreator()
	{
		return new OutputLookupDataCreator
		{
			TestData = this
		};
	}

	#endregion
}
