using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Providers;

using UnitTests.Models;

namespace UnitTests.Creators.Outputs;

public abstract class BaseOutputDataCreator
{
	public TestDataCreator TestData { get; init; } = null!;

	// These properties are simple "pass through" to `TestData`, but they greatly improve readability of expected streams creation code.
	protected CharColourMode ColourMode { get => TestData.ColourMode; }
	
	protected bool IsRRBEnabled { get => TestData.IsRRBEnabled; }
	
	protected bool IsCharsRunnerEnabled { get => TestData.IsCharsRunnerEnabled; }
	protected bool IsCharsInputUsed { get => TestData.IsCharsInputUsed; }
	
	protected bool IsScreensRunnerEnabled { get => TestData.IsScreensRunnerEnabled; }
	protected bool IsScreensInputUsed { get => TestData.IsScreensInputUsed; }

	protected bool IsRRBSpritesRunnerEnabled { get => TestData.IsRRBSpritesRunnerEnabled; }
	protected bool IsRRBSpritesInputUsed { get => TestData.IsRRBSpritesInputUsed; }

	private MemoryStreamProvider? expectedStream;
	private MemoryStreamProvider? actualStream;

	#region Subclass

	protected abstract IStreamProvider? OnGetActualStream(DataContainer.OutputStreams outputs);
	protected abstract IStreamProvider? OnGetExpectedStream();

	#endregion

	#region Data

	public MemoryStreamProvider? GetActualData()
	{
		actualStream ??= (MemoryStreamProvider?)OnGetActualStream(TestData.GetDataContainerCreator().Get().UsedOutputStreams);

		return actualStream;
	}

	public MemoryStreamProvider? GetExpectedData()
	{
		expectedStream ??= (MemoryStreamProvider?)OnGetExpectedStream();

		return expectedStream;
	}

	#endregion
}
