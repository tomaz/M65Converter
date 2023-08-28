using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Helpers.Converters.Layer;

/// <summary>
/// Merges multiple layers into single layer.
/// </summary>
public class LayerMergerSingle : LayerMerger
{
	#region Overrides

	public override LevelData Merge(LevelData source)
	{
		// Otherwise we need to merge all layer images into single one to preserve char transparency and then use this single layer to generate characters from.
		Logger.Verbose.Separator();
		Logger.Debug.Message("Merging layers");

		var mergedImage = new Image<Argb32>(width: source.Width, height: source.Height);

		foreach (var layer in source.Layers)
		{
			Logger.Verbose.Option($"{Path.GetFileName(layer.Path)}");

			var mergedPixels = 0;

			mergedImage.Mutate(mutator =>
			{
				layer.Image.ProcessPixelRows(accessor =>
				{
					for (var y = 0; y < Math.Min(mergedImage.Height, source.Height); y++)
					{
						var sourceRowSpan = accessor.GetRowSpan(y);

						for (var x = 0; x < Math.Min(mergedImage.Width, source.Width); x++)
						{
							// Only copy non-transparent colours.
							var colour = sourceRowSpan[x];
							if (colour.A == 0) continue;

							mergedPixels++;
							mutator.SetPixel(colour, x, y);
						}
					}
				});
			});

			var totalPixels = mergedImage.Width * mergedImage.Height;
			var mergePercentage = mergedPixels * 100.0 / totalPixels;
			Logger.Verbose.SubOption($"{mergedPixels} of {totalPixels} ({mergePercentage:0.00}%) pixels overwriten");
		}

		// Prepare the new layer.
		var mergedLayer = new LevelData.LayerData
		{
			Path = Path.Combine(Path.GetDirectoryName(source.Layers.First().Path)!, "MergedLayer"),
			Image = mergedImage
		};

		// Return our single merged layer as the result.
		return new LevelData
		{
			Width = source.Width,
			Height = source.Height,
			LevelName = source.LevelName,
			RootFolder = source.RootFolder,
			Layers = new() { mergedLayer }
		};
	}

	#endregion
}
