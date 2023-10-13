namespace M65Converter.Sources.Helpers.Utils;

public static class Logger
{
	public static VerbosityType Verbosity
	{
		get => _verbosity;
		set
		{
			_verbosity = value;

			Info.IsEnabled = value >= VerbosityType.Info;
			Debug.IsEnabled = value >= VerbosityType.Debug;
			Verbose.IsEnabled = value >= VerbosityType.Verbose;
		}
	}
	private static VerbosityType _verbosity = VerbosityType.Info;

	public static Log Info { get; } = new()
	{
		IsEnabled = Verbosity >= VerbosityType.Info
	};

	public static Log Debug { get; } = new()
	{
		IsEnabled = Verbosity >= VerbosityType.Debug
	};

	public static Log Verbose { get; } = new()
	{
		IsEnabled = Verbosity >= VerbosityType.Verbose
	};

	#region Declarations

	public class Log
	{
		public bool IsEnabled { get; set; }

		public void Separator()
		{
			if (IsEnabled) Console.WriteLine();
		}

		public void Exclamation(string message)
		{
			if (IsEnabled) Console.WriteLine($">>>> {message}");
		}

		public void Box(params string[] lines)
		{
			Separator();
			Message(" ==============================================================================");
			foreach (var line in lines)
			{
				// Lines may also contain new lines, we split it and display each line separately.
				var sublines = line.Split(Environment.NewLine);
				foreach (var subline in sublines)
				{
					Message($"|| {subline}");
				}
			}
			Message(" ==============================================================================");
			Separator();
		}

		public void Box(Exception e, string title, params string[] conclusions)
		{
			if (conclusions.Length > 0)
			{
				Box(
					title,
					string.Empty,
					e.Message,
					string.Empty,
					string.Join(Environment.NewLine, conclusions),
					string.Empty,
					e.GetType().Name,
					e.StackTrace ?? string.Empty
				);
			}
			else
			{
				Box(
					title,
					string.Empty,
					e.Message,
					string.Empty,
					e.GetType().Name,
					e.StackTrace ?? string.Empty
				);
			}
		}

		public void Message(string message)
		{
			if (IsEnabled) Console.WriteLine(message);
		}

		public void Option(string option)
		{
			if (IsEnabled) Console.WriteLine($"  {option}");
		}

		public void SubOption(string option)
		{
			if (IsEnabled) Console.WriteLine($"    {option}");
		}

		public void SubSubOption(string option)
		{
			if (IsEnabled) Console.WriteLine($"      {option}");
		}
	}

	public enum VerbosityType
	{
		Info,
		Debug,
		Verbose,
	}

	#endregion
}
