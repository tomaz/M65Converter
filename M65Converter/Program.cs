using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Helpers.Utils;

using System.CommandLine;

static Command CreateRootCommand(DataContainer data)
{
	var result = new GlobalOptionsBinder().CreateCommand(data);

	result.AddCommand(new CharsOptionsBinder().CreateCommand(data));
	result.AddCommand(new ScreensOptionsBinder().CreateCommand(data));

	return result;
}

var result = 0;
var data = new DataContainer();

new TimeRunner
{
	LoggerFunction = Logger.Info.Message,
	Header = Array.Empty<string>(),
	Footer = new[] { "", "Total: {Time}" }
}
.Run(() =>
{
	try
	{
		var result = CreateRootCommand(data).InvokeAllCommands(args);

		data.ExportData();
	}
	catch (Exception e)
	{
		Logger.Info.Separator();
		Logger.Info.Message($"ERROR OCCURRED {e}");
	}
});

return result;
