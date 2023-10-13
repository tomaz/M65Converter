using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;

namespace M65Converter.Sources.Exporting;

public abstract class BaseExporter
{
	/// <summary>
	/// All the options and data.
	/// </summary>
	public DataContainer Data { get; init; } = null!;

	#region Subclass

	/// <summary>
	/// Exports the data with the given binary writer.
	/// 
	/// Should only be called for subclasses that support binary writer exports.
	/// </summary>
	public virtual void Export(BinaryWriter writer)
	{
		throw new NotSupportedException("This class only supports exporting with stream provider");
	}

	/// <summary>
	/// Exports the data using given stream provider.
	/// 
	/// This is lower level export and should only be called for subclasses that support exporting with streams.
	/// </summary>
	public virtual void Export(IStreamProvider streamProvider)
	{
		throw new NotSupportedException("This subclass only supports exporting with binary writer");
	}

	#endregion
}
