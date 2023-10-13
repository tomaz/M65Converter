using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting.Utils;
using M65Converter.Sources.Runners.Helpers;

namespace M65Converter.Runners;

/// <summary>
/// Base class for a runner.
/// 
/// A runner is an object that handles a cmd line command. The responsibilities are:
/// 
/// - Declaring the command line command and arguments
/// - Parsing the command line through <see cref="BaseOptionsBinder"/> subclass
/// - Handling the inputs and generating outputs
/// - Exporting generated data
/// 
/// In order to provide a common interface for all runners, this is the order in which the data is handled:
///
/// 1. Command line options are parsed and instances of all runners that will be required are prepared. The order of the commands in the cmd line determines the order in which runners are then run in subsequent steps.
/// 
/// 2. All runners are asked to validate their "position" in the upcoming run and adjust common run options:
///		1. <see cref="OnValidateRunPosition"/>
///		2. <see cref="OnAdjustRunOptions"/>
///		
/// 3. The runners are then asked to handle inputs. Each runner get this sequence of methods called in the following order before proceeding with next runner and so on until all registered runners are done. The responsibility of the runners is to parse all data into <see cref="DataContainer"/>.
///		1. <see cref="OnDescribeStep"/>
///		2. <see cref="OnParseInputs"/>
///		3. <see cref="OnValidateParsedData"/>
///	
/// 4. Now we have all the base data needed for export. However we still need to generate export data. This is a step where multiple runners may append their part of data into the same export. So all runners are asked to prepare the export data into <see cref="DataContainer"/>:
///		1. <see cref="OnPrepareExportData"/>
///		
/// 5. After all runners prepare their export data, they also get a chance to finalize the data. Some types of runners may need this additional step to happen after all instances have completed preparing export data.
///		1. <see cref="OnFinalizeExportData"/>
///	
/// 6. After all export data is prepared, the runners are asked to actually export. First each runner gets asked to validate the final export data is valid, and then they are asked to export. NOTE: if dry run is instructed, data is validated only but not exported!
///		1. <see cref="OnValidateExportData"/>
///		2. <see cref="OnExportData"/>
/// 
/// If an exception is thrown during any of the steps, error is logged and run aborted.
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
	public virtual string? Title() => null;

	/// <summary>
	/// Validates this runners instance position in upcoming run.
	/// 
	/// This is where runners need to make sure any prerequisites are run first, only one instance of specific runner is registered etc.
	/// </summary>
	public virtual void OnValidateRunPosition(RunnersRegister runners)
	{
	}

	/// <summary>
	/// Adjusts various run options.
	/// 
	/// This is useful for subclasses that need to run AFTER another command but they need to influence PRIOR command handling. Typically this comes in the form of changing global options.
	/// </summary>
	public virtual void OnAdjustRunOptions()
	{
	}

	/// <summary>
	/// Describes what this step will perform when invoked.
	/// 
	/// Sometimes it's also viable to describe what step won't do. For example if the runner accepts optional array of input files, all the inputs will be logged during parsing. However if no input is provided, it might be beneficial to inform the user that this step won't result in any data being collected.
	/// </summary>
	public virtual void OnDescribeStep()
	{
	}

	/// <summary>
	/// Subclass should parse all of its inputs.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnParseInputs()
	{
	}

	/// <summary>
	/// Subclass should validate all parsed data it gathered in <see cref="OnParseInputs"/>.
	/// 
	/// If data is not valid subclass should throw an exception which will stop all further parsing.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnValidateParsedData()
	{
	}

	/// <summary>
	/// At this point we have finished parsing and established common data (palette etc), so subclass should prepare all additional data for export.
	/// 
	/// This is the step where the data is copied into <see cref="DataContainer"/>. Some runners may register their data already during parsing, then they don't have to override this method. But if final data can only be prepared after we post-process in, then this is where preparation should occur before registering to the data container.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnPrepareExportData()
	{
	}

	/// <summary>
	/// All runners have now prepared their export data, so subclass that needs to finalize something on top of that can override and implement the needed functionality.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnFinalizeExportData()
	{
	}

	/// <summary>
	/// This is called after all runners prepare their export data <see cref="OnPrepareExportData"/> and before they get the chance to export it.
	/// 
	/// It's the final validation to make sure all the data will be valid on Mega 65.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnValidateExportData()
	{
	}

	/// <summary>
	/// The final step after all data has been parsed and processed; subclass should now export its data.
	/// 
	/// Default implementation doesn't do anything.
	/// </summary>
	public virtual void OnExportData()
	{
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Helper function for simpler, one line <see cref="Exporter"/> creation.
	/// </summary>
	protected Exporter CreateExporter(string description, IStreamProvider provider)
	{
		return new()
		{
			LogDescription = description,
			Stream = provider
		};
	}

	#endregion
}
