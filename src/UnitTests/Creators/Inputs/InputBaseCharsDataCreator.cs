using UnitTests.Models;

namespace UnitTests.Creators.Inputs;

public class InputBaseCharsDataCreator : BaseCreator<MemoryStreamProvider[]?>
{
	#region Overrides

	protected override MemoryStreamProvider[]? OnCreateInstance()
	{
		if (!TestData.IsCharsInputUsed) return null;

		return new[] {
			new MemoryStreamProvider
			{
				Data = Resources.input_base_chars,
				Filename = "input-base-chars.png"
			}
		};
	}

	#endregion
}
