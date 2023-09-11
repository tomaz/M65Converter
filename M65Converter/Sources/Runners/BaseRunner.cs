using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Runners;

/// <summary>
/// Base class for a runner.
/// 
/// A runner is an object that handles a cmd line command. The responsibilities are:
/// 
/// - Declaring the command line command and arguments
/// - Parsing the command line through <see cref="BaseOptionsBinder"/> subclass
/// - Handling the inputs and generating outputs
/// </summary>
public abstract class BaseRunner
{
	/// <summary>
	/// The data container where all parsed data is saved into.
	/// 
	/// The data instance is shared between all runners, so each subsequent one can append data as needed.
	/// </summary>
	public DataContainer Data { get; set; } = null!;

	#region Subclass

	/// <summary>
	/// Optional title, used as timer header if provided.
	/// </summary>
	protected virtual string? Title() => null;

	/// <summary>
	/// Called before any other override. Subclass should validate it has all required input parameters.
	/// </summary>
	protected virtual void OnValidate()
	{
	}

	/// <summary>
	/// Called after <see cref="OnValidate"/>. Subclass that needs to setup additional data for run can do so here.
	/// </summary>
	protected virtual void OnSetup()
	{
	}

	/// <summary>
	/// Called after <see cref="OnSetup"/>. This is where the subclass should implement its handling.
	/// </summary>
	protected abstract void OnRun();

	#endregion

	#region Public

	public void Run()
	{
		new TimeRunner
		{
			Title = Title(),
			Header = " ==============================================================================\r\n// ",
			Footer = "\r\n\\\\ {Time} [{Title}]\r\n =============================================================================="
		}
		.Run(() =>
		{
			OnValidate();
			OnSetup();
			OnRun();
		});
	}

	#endregion
}
