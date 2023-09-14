using M65Converter.Sources.Data.Intermediate;

namespace UnitTests.Creators;

public abstract class BaseRunnerCreator<T>
{
	public DataContainer Data { get; init; } = null!;

	private T? instance;

	#region Subclass

	protected abstract T CreateInstance();

	#endregion

	#region Public

	public T Get()
	{
		instance ??= CreateInstance();

		return instance;
	}

	#endregion
}
