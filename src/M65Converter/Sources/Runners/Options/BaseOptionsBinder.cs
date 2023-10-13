using System.CommandLine;
using System.CommandLine.Binding;
using System.Globalization;
using System.Reflection;
using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Runners.Options;

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
	private DataContainer data = null!;
	private readonly bool isGlobal = false;

	#region Initialization & Disposal

	protected BaseOptionsBinder(bool global = false)
	{
		isGlobal = global;
	}

	#endregion

	#region Overrides

	protected sealed override T GetBoundValue(BindingContext bindingContext)
	{
		// We don't allow overriding default binding method, instead we redirect to out own where we can pass data container to.
		return OnCreateOptions(bindingContext, data);
	}

	#endregion

	#region Subclass

	protected abstract Command OnCreateCommand();

	protected abstract T OnCreateOptions(BindingContext bindingContext, DataContainer data);

	protected virtual void OnAssignOptions(T options, DataContainer data)
	{
	}

	protected abstract BaseRunner OnCreateRunner(T options, DataContainer data);

	#endregion

	#region Commands

	public Command CreateCommand(DataContainer data)
	{
		// Assign data so we can pass it to various subclass methods not called directly from here.
		this.data = data;

		var result = OnCreateCommand();

		// Add all options from subclass non-private fields.
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
		result.SetHandler((context) =>
		{
			try
			{
				// Get all command line options and arguments for this command.
				var options = GetBoundValue(context.BindingContext);

				// We must also parse global options for each and every command. I didn't find a way of doing this just once with `System.CommandLine` - it only invokes `GetBoundValue` on `GlobalOptionsBinder` but couldn't get any other subclass method to get called. Maybe because global options are just options, no actual command?? Anyway, the solution is to inject global options before each command (happens in `InvokeAllCommands` extension method) and then re-parse and re-assign them to data container.
				data.GlobalOptions = GlobalOptionsBinder.Instance.GetBoundValue(context.BindingContext);

				// Notify subclass about its options.
				OnAssignOptions(options, data);

				// Ask subclass to create a runner and store the instance to data container. We'll execute all commands in several passes once we have all of them registered.
				data.Runners.Register(OnCreateRunner(options, data));
			}
			catch (Exception e)
			{
				Logger.Info.Box(e, "ERROR");

				// Finish all subsequent commands handling.
				context.ExitCode = -1;
			}
		});

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
		var width = components[0].Trim().ParseAsInt();
		var height = components.Length >= 2 ? components[1].Trim().ParseAsInt() : defaultHeight;
		return new Size(width, height);
	}

	public static Point ParseAsPoint(this string value)
	{
		var components = value.Split(',');
		var x = components[0].Trim().ParseAsInt();
		var y = components.Length >= 2 ? components[1].Trim().ParseAsInt() : 0;
		return new Point(x, y);
	}
}
