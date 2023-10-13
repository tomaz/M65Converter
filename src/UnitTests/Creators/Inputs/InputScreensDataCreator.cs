using UnitTests.Models;

namespace UnitTests.Creators.Inputs;

public class InputScreensDataCreator : BaseCreator<MemoryStreamProvider[]>
{
	protected override MemoryStreamProvider[] OnCreateInstance()
	{
		if (!TestData.IsScreensRunnerEnabled) return Array.Empty<MemoryStreamProvider>();

		return new[] {
			new MemoryStreamProvider
			{
				Data = Resources.input_level,
				Filename = "input-level.aseprite"
			}
		};
	}
}
