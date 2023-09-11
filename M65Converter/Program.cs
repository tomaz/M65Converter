using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

using System.CommandLine;

static Command CreateRootCommand(DataContainer data)
{
	var result = new RootCommand(description: "Converter for various mega 65 related files");

	result.AddGlobalOption(GlobalOptions.VerbosityOption);
	result.AddCommand(new ScreensOptionsBinder().CreateCommand(data));

	return result;
}

var result = 0;
var data = new DataContainer();

new TimeRunner
{
	LoggerFunction = Logger.Info.Message,
	Header = "",
	Footer = "\r\nTotal: {Time}"
}
.Run(() =>
{
	result = CreateRootCommand(data).Invoke(args);

	data.ExportGeneratedData();
});

return result;
