using M65Converter.Sources.Helpers.Images;

namespace M65Converter.Sources.Data.Intermediate;

/// <summary>
/// Contains data from parsed image.
/// </summary>
public class ImageData
{
	/// <summary>
	/// Source image.
	/// </summary>
	public Image<Argb32> Image { get; set; } = null!;

	/// <summary>
	/// All colours <see cref="Image"/> contains.
	/// </summary>
	public List<Argb32> Palette { get; set; } = new();

	/// <summary>
	/// Indexed image where each colour is represented as index into the palette.
	/// 
	/// Note: indexes may either be into assigned <see cref="Palette"/>, or into global palette, depending on state of the program.
	/// </summary>
	public IndexedImage IndexedImage { get; set; } = null!;

	/// <summary>
	/// Indicates whether all pixels of this image are transparent or not. If all pixels are transparent this value is true, if at least one opaque or semi-transparent pixel exists, this is false.
	/// </summary>
	public bool IsFullyTransparent { get; set; }

	#region Overrides

	public override string ToString()
	{
		var warnings = "";

		if (IndexedImage.Width != Image.Width || IndexedImage.Height != Image.Height)
		{
			warnings += $" ({IndexedImage})";
		}

		return $"{GetType().Name} {Image.Width}x{Image.Height} @{Palette.Count}{warnings}";
	}

	#endregion

	#region Public

	public bool IsDuplicateOf(ImageData other)
	{
		return Image.IsDuplicateOf(other.Image);
	}

	#endregion
}
