using System.CommandLine;

namespace M65Converter.Sources.Runners.Options.Helpers;

public static class CommandExtensions
{
	/// <summary>
	/// Invokes this command using multiple runs, once per each sub-command.
	/// 
	/// This method works around `System.CommandLine` inability to invoke multiple commands during single run.
	/// </summary>
	public static int InvokeAllCommands(this Command command, string[] args)
	{
		// Note: we must use `Console.WriteLine` here since we only setup `Logger` verbosity after parsing command line.

		string[] SubCommands()
		{
			var result = new List<string>();

			var enumerator = command.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (enumerator.Current is Command cmd)
				{
					result.Add(cmd.Name);
				}
			}

			return result.ToArray();
		}

		int NextCommandIndex(int index, string[] commands)
		{
			while (index < args.Length)
			{
				var arg = args[index];

				if (commands.Contains(arg))
				{
					return index;
				}

				index++;
			}

			// If we reached this point, there is no additional command so return the length.
			return args.Length;
		}

		// Extract all possible sub-commands.
		var subcommands = SubCommands();
		Console.WriteLine($"Matching {subcommands.Length} possible commands with total {args.Length} arguments");

		// If user didn't provide any argument, just invoke the command normally to print out help.
		if (args.Length == 0)
		{
			return command.Invoke(args);
		}

		// Invoke the command as many times as needed.
		var index = 0;
		string[]? globalOptions = null;
		Console.WriteLine("Will run:");
		while (index < args.Length)
		{
			// Find next command, then search again to find the end of the command arguments. This will also consume any starting global options with the first command.
			var nextCmdIdx = NextCommandIndex(index, subcommands);
			var endCmdIdx = NextCommandIndex(nextCmdIdx + 1, subcommands);

			// Prepare global options if needed. We have to inject them to every command since our `BaseOptionsBinder` re-assigns them to the data container on each command also. We could alternatively only assign the first time, however global options can also be specified under each command, along side command specific options. The approach used here will correctly take any global options setup, either globals specified before all commands or injected with command options.
			if (globalOptions == null && nextCmdIdx > 0)
			{
				globalOptions = args[0..nextCmdIdx];
			}

			// Prepare arguments for new run and invoke the command. Exit if the result is an error.
			var options = globalOptions != null ? globalOptions : Array.Empty<string>();
			var runArgs = options.Concat(args[nextCmdIdx..endCmdIdx]).ToArray();

			Console.WriteLine();
			Console.WriteLine($"{string.Join(' ', runArgs)}");
			var result = command.Invoke(runArgs);
			if (result != 0) return result;

			// Proceed with next command.
			index = endCmdIdx;
		}

		// If we reached here, all commands completed successfully.
		return 0;
	}
}
