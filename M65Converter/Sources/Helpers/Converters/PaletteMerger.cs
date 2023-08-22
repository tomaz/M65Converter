using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Helpers.Converters;

/// <summary>
/// Merges palettes from multiple images into single palette.
/// 
/// Input:
/// - A list of <see cref="ImageData"/> with local palettes and locally indexed image data.
/// 
/// Results:
/// - Global palette with reused colours.
/// - All indexed images of <see cref="ImageData"/> objects point to global palette.
/// </summary>
public class PaletteMerger
{
	/// <summary>
	/// The list of all <see cref="ImageData"/> which palettes should be merged.
	/// </summary>
	public List<ImageData> Images { get; set; } = null!;

	private List<Argb32> palette = new();

	#region Public

	/// <summary>
	/// Merges all colours from assigned images.
	/// </summary>
	public List<Argb32> Merge()
	{
		palette.Clear();

		var index = 0;
		foreach (var image in Images)
		{
			// Prepare the dictionary that defines how local colours should be translated to global palette. This also updates palette with all unique colours of the image.
			var map = MergeColours(image, index);

			// Remap indexes image with global indexes.
			image.IndexedImage.Remap(map, index);

			index++;
		}

		return palette;
	}

	#endregion

	#region Helpers

	private Dictionary<int, int> MergeColours(ImageData item, int itemIndex)
	{
		var isItemChangeLogged = false; // used to only log item if it has new colours

		var result = new Dictionary<int, int>();

		for (var i = 0; i < item.Palette.Count; i++)
		{
			var colour = item.Palette[i];

			var index = palette.IndexOf(colour);
			if (index < 0)
			{
				if (!isItemChangeLogged)
				{
					isItemChangeLogged = true;
					Logger.Verbose.Separator();
					Logger.Verbose.Message($"Merging colours for char {itemIndex}");
				}

				Logger.Verbose.Option($"{palette.Count} -> {colour}");
				index = palette.Count;
				palette.Add(colour);
			}

			result[i] = index;
		}

		return result;
	}

	#endregion
}
