using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Options;
using M65Converter.Sources.Runners.Options.Helpers;
using System.CommandLine;

static Command CreateRootCommand(DataContainer data)
{
	var result = new GlobalOptionsBinder().CreateCommand(data);

	result.AddCommand(new CharsOptionsBinder().CreateCommand(data));
	result.AddCommand(new ScreensOptionsBinder().CreateCommand(data));
	result.AddCommand(new RRBSpritesOptionsBinder().CreateCommand(data));

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
		// Create instances for all commands.
		var result = CreateRootCommand(data).InvokeAllCommands(args);
		if (result != 0) return;

		// Run all commands.
		data.Run();
	}
	catch (Exception e)
	{
		Logger.Info.Box(e, "OH NO, SOMETHING WENT WRONG...");
	}
});

return result;
