using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Helpers.Converters.Palette;

public class PaletteMerger8Bit : PaletteMerger
{
	#region Overrides

	protected override void OnMerge(IReadOnlyList<ImageData> images, List<Argb32> palette)
	{
		var index = -1;
		foreach (var image in images)
		{
			index++;

			// Prepare the dictionary that defines how local colours should be translated to global palette. This also updates palette with all unique colours of the image.
			var map = MergeColours(palette, image, index);

			// Remap indexed image with global indices.
			var result = image.IndexedImage.RemapWithFormatter(map);

			// Log information about the merge if there was some change.
			if (result.IsChanged && result.Formatter != null)
			{
				Logger.Verbose.Separator();
				Logger.Verbose.Message($"Remapping colour indices for char {index}");
				result.Formatter.Log(Logger.Verbose.Option);
			}
		}
	}

	#endregion

	#region Helpers

	private Dictionary<int, int> MergeColours(List<Argb32> palette, ImageData item, int itemIndex)
	{
		var isItemChangeLogged = false; // used to only log item if it adds any new colours to merged palette

		return palette.MergeColours(
			from: item.Palette,
			callback: (isAdded, original, index, colour) =>
			{
				// We only log colours added to merged palette.
				if (!isAdded) return;

				if (!isItemChangeLogged)
				{
					isItemChangeLogged = true;
					Logger.Verbose.Separator();
					Logger.Verbose.Message($"Merging colours for char {itemIndex}");
				}

				Logger.Verbose.Option($"{index} -> {colour}");
			}
		);
	}

	#endregion
}
