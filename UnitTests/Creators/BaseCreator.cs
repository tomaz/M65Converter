using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;

using System.Reflection;

using UnitTests.Models;

namespace UnitTests.Creators;

public interface IBaseCreator
{
}

public abstract class BaseCreator<T>: IBaseCreator
{
	protected CharColourMode ColourMode { get; set; }
	protected bool IsRRBEnabled { get; set; }
	protected bool IsCharsRunnerEnabled { get; set; }
	protected bool IsScreensRunnerEnabled { get; set; }

	private T? instance;
	private MemoryStreamProvider? actualStream;

	#region Initialization & Disposal

	public BaseCreator()
	{
		// We need parameter-less constructor so subclasses are not forced to use copy constructor.
	}

	public BaseCreator(IBaseCreator source)
	{
		// We only support our subclasses, but we do need an interface since C# can't accept "any" for template parameter (at least I'm not aware of this possibility). We should be fine though, we are always using BaseCreator subclasses. The code below scans all properties of our class and then checks if source also has them - the value of each property also found on source is then copied to out class.
		var flags = BindingFlags.Instance | BindingFlags.NonPublic;
		foreach (var property in GetType().GetProperties(flags))
		{
			var sourceField = source.GetType().GetProperty(property.Name, flags);
			if (sourceField != null)
			{
				var value = sourceField.GetValue(source);
				property.SetValue(this, value);
			}
		}
	}

	#endregion

	#region Subclass

	protected abstract T OnCreateObject();
	protected abstract IStreamProvider? OnGetActualStream(DataContainer data);

	#endregion

	#region Builder

	public BaseCreator<T> Colour(CharColourMode mode)
	{
		ColourMode = mode;
		return this;
	}

	public BaseCreator<T> RunChars(bool run = true)
	{
		IsCharsRunnerEnabled = run;
		return this;
	}

	public BaseCreator<T> RunScreens(bool run = true)
	{
		IsScreensRunnerEnabled = run;
		return this;
	}

	public BaseCreator<T> RRB(bool use = true)
	{
		IsRRBEnabled = use;
		return this;
	}

	#endregion

	#region Results

	/// <summary>
	/// Creates and returns the instance this creator handles.
	/// 
	/// Instance is only created once, then reused for subsequent calls (if you want to change parameters, you have to create a new subclas instance as well).
	/// </summary>
	public T Get()
	{
		instance ??= OnCreateObject();

		return instance;
	}

	/// <summary>
	/// Extracts actual data stream that corresponds to this creator.
	/// 
	/// This also extracts the stream only the first time, then caches is and returns the same instance on each subsequent method call.
	/// </summary>
	public MemoryStreamProvider? GetActualData(DataContainer data)
	{
		// We always expect to receive memory stream provider! But subclass method returns the interface - that's pure convenience, so that we don't have to box/unbox in each and every subclass.
		actualStream ??= (MemoryStreamProvider?)OnGetActualStream(data);

		return actualStream;
	}

	#endregion
}
