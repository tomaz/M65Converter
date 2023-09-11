using System.CommandLine;
using System.CommandLine.Binding;
using System.Globalization;
using System.Reflection;
using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;

namespace M65Converter.Sources.Helpers.Inputs;

/// <summary>
/// Generic options binder.
/// 
/// To use:
/// 
/// 1. Create subclass
/// 2. Declare all options as private variables.
/// 3. Override OnCreateCommand() and return the command instance
/// 4. Override OnCreateRunner() and return the runner
/// 5. Override GetBoundValue() and return the object with all option values (using `bindingContext.ParseResult.GetValueForOption(inputOption)`)
/// 
/// So there's still some manual work, but at least options assignment to command is automated...
/// </summary>
public abstract class BaseOptionsBinder<T> : BinderBase<T>
{
	private bool isGlobal = false;

	#region Initialization & Disposal

	protected BaseOptionsBinder(bool global = false)
	{
		isGlobal = global;
	}

	#endregion

	#region Subclass

	protected abstract Command OnCreateCommand();
	protected abstract void OnAssignOptions(T options, DataContainer data);
	protected abstract BaseRunner OnCreateRunner(T options, DataContainer data);

	#endregion

	#region Commands

	public Command CreateCommand(DataContainer data)
	{
		var result = OnCreateCommand();

		// Add all options.
		foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			var value = field.GetValue(this);

			if (value is Option option)
			{
				if (isGlobal)
				{
					result.AddGlobalOption(option);
				}
				else
				{
					result.Add(option);
				}
			}

			if (value is Argument argument)
			{
				result.AddArgument(argument);
			}
		}

		// Setup handler.
		// Note how we embed runner inside try/catch to log any runtime errors.
		// Note how we need to manage global options manually.
		result.SetHandler((globals, options) =>
		{
			try
			{
				// Assign global options.
				data.GlobalOptions = globals;

				// Notify subclass about its options.
				OnAssignOptions(options, data);

				// Ask subclass to create a runner and invoke it.
				OnCreateRunner(options, data).Run();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

		},
		GlobalOptionsBinder.Instance,
		this);

		return result;
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Converts the given file info to stream provider.
	/// </summary>
	protected IStreamProvider? Provider(FileInfo? info)
	{
		return info != null
			? new FileStreamProvider { FileInfo = info }
			: null;
	}

	/// <summary>
	/// Converts the given array of file infos to array of stream providers.
	/// </summary>
	protected IStreamProvider[]? Providers(FileInfo[]? infos)
	{
		return (infos?.Length ?? 0) > 0
			? infos!.Select(x => Provider(x)!).ToArray()
			: null;
	}

	#endregion
}

public static class OptionsExtensions
{
	public static int ParseAsInt(this string value)
	{
		static int ParseHex(string value) => int.Parse(value, NumberStyles.HexNumber);

		if (value.StartsWith("$")) return ParseHex(value[1..]);
		if (value.StartsWith("0x")) return ParseHex(value[2..]);

		return int.Parse(value);
	}

	public static Size ParseAsSize(this string value, int defaultHeight = 0)
	{
		var components = value.Split('x');
		var width = ParseAsInt(components[0].Trim());
		var height = components.Length >= 2 ? ParseAsInt(components[1].Trim()) : defaultHeight;
		return new Size(width, height);
	}
}
