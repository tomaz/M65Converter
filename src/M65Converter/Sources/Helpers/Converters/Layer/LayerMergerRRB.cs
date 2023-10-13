using M65Converter.Sources.Data.Models;

namespace M65Converter.Sources.Helpers.Converters.Layer;

/// <summary>
/// Layers merger when raster-rewrite-buffer is used.
/// </summary>
public class LayerMergerRRB : LayerMerger
{
	#region Overrides

	public override Level Merge(Level source)
	{
		// In this case there's no merging needed, so simply return the source data.
		return source;
	}

	#endregion
}
