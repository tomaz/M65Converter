using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners;

using System.CommandLine;

Command CreateRootCommand()
{
	var result = new RootCommand(description: "Converter for various mega 65 related files");

	result.AddGlobalOption(GlobalOptions.VerbosityOption);
	result.AddCommand(new CharsRunner.OptionsBinder().CreateCommand());

	return result;
}

var result = 0;

new TimeRunner("\r\nTotal ").Run(() =>
{
	result = CreateRootCommand().Invoke(args);
});

return result;
