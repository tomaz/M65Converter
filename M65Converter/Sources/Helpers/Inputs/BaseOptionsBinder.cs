using System.CommandLine;
using System.CommandLine.Binding;
using System.Globalization;
using System.Reflection;
using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

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
	#region Subclass

	protected abstract Command OnCreateCommand();
	protected abstract void OnAssignOptions(T options, DataContainer data);
	protected abstract BaseRunner OnCreateRunner(T options, DataContainer data);

	#endregion

	#region Helpers

	public Command CreateCommand(DataContainer data)
	{
		var result = OnCreateCommand();

		// Add all options.
		foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			var value = field.GetValue(this);

			if (value is Option option)
			{
				result.Add(option);
			}

			if (value is Argument argument)
			{
				result.AddArgument(argument);
			}
		}

		// Setup handler. Note how we embed runner inside try/catch to log any runtime errors.
		result.SetHandler((verbosity, options) =>
		{
			try
			{
				Logger.Verbosity = verbosity;

				OnAssignOptions(options, data);
				OnCreateRunner(options, data).Run();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

		},
		GlobalOptions.VerbosityOption,
		this);

		return result;
	}

	#endregion
}

public static class GlobalOptions
{
	public static Option<Logger.VerbosityType> VerbosityOption = new(
		aliases: new[] { "-v", "--verbosity" },
		description: "Verbosity level",
		getDefaultValue: () => Logger.Verbosity
	);
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
}
