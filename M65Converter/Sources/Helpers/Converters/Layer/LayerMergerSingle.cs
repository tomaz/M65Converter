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
		Logger.Verbose.Separator();
		Logger.Debug.Message("Merging layers");

		Image<Argb32> mergedImage;

		if (source.CompositeLayer != null && Options.IsCompositeImageAllowed)
		{
			// If we have composite layer image and caller allows using it, prefer that. Composite image takes into consideration layer transparency and blending mode, so is more accurate, but is more prone to colours overflow since every blended colour needs to be added to palette.
			Logger.Verbose.Option("Taking composite image");
			mergedImage = source.CompositeLayer.Image;
		}
		else {
			// Otherwise we merge layers manually.
			mergedImage = MergeManually(source);
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

	#region Helpers

	private Image<Argb32> MergeManually(LevelData source)
	{
		var result = new Image<Argb32>(width: source.Width, height: source.Height);

		foreach (var layer in source.Layers)
		{
			Logger.Verbose.Option($"{Path.GetFileName(layer.Path)}");

			var mergedPixels = 0;

			result.Mutate(mutator =>
			{
				layer.Image.ProcessPixelRows(accessor =>
				{
					for (var y = 0; y < Math.Min(result.Height, source.Height); y++)
					{
						var sourceRowSpan = accessor.GetRowSpan(y);

						for (var x = 0; x < Math.Min(result.Width, source.Width); x++)
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

			var totalPixels = result.Width * result.Height;
			var mergePercentage = mergedPixels * 100.0 / totalPixels;
			Logger.Verbose.SubOption($"{mergedPixels} of {totalPixels} ({mergePercentage:0.00}%) pixels overwriten");
		}

		return result;
	}

	#endregion
}
