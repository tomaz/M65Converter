using UnitTests.Models;

namespace UnitTests.Creators.Inputs;

public class InputRRBSpritesDataCreator : BaseCreator<MemoryStreamProvider[]>
{
	protected override MemoryStreamProvider[] OnCreateInstance()
	{
		if (!TestData.IsRRBSpritesRunnerEnabled) return Array.Empty<MemoryStreamProvider>();

		return new[]
		{
			new MemoryStreamProvider
			{
				Data = Resources.input_sprite1,
				Filename = "input-sprites1.aseprite"
			},

			new MemoryStreamProvider
			{
				Data = Resources.input_sprite2,
				Filename = "input-sprites2.png"
			}
		};
	}
}
