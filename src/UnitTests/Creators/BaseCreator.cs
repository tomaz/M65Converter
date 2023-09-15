using M65Converter.Sources.Data.Intermediate;

using System.Reflection;

namespace UnitTests.Creators;

public abstract class BaseCreator<T>
{
	public TestDataCreator TestData { get; init; } = null!;

	private T? instance;

	#region Subclass

	protected abstract T OnCreateInstance();

	#endregion

	#region Getting object

	public T Get()
	{
		instance ??= OnCreateInstance();

		return instance;
	}

	#endregion
}
