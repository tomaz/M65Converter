using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Converters.Layer;

namespace M65Converter.Sources.Helpers.Converters;

/// <summary>
/// Merges layers into data suitable for exporting.
/// </summary>
public abstract class LayerMerger
{
	protected OptionsType Options { get; private set; } = null!;

	#region Initialization & Disposal

	public static LayerMerger Create(OptionsType options)
	{
		LayerMerger result = options.IsRasterRewriteBufferSupported
			? new LayerMergerRRB()
			: new LayerMergerSingle();

		result.Options = options;

		return result;
	}

	#endregion

	#region Subclass

	/// <summary>
	/// Merges data from the given source into a new data.
	/// </summary>
	public abstract LevelData Merge(LevelData source);

	#endregion

	#region Declarations

	public class OptionsType
	{
		public bool IsRasterRewriteBufferSupported { get; init; }
	}

	#endregion
}
