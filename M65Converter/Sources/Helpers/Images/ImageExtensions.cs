using SixLabors.ImageSharp.Drawing.Processing;

using System.Runtime.CompilerServices;

namespace M65Converter.Sources.Helpers.Images;

public static class ImageExtensions
{
	/// <summary>
	/// Converts the given colour to semi-transparent using the given alpha.
	/// </summary>
	public static Argb32 WithAlpha(this Argb32 colour, byte alpha)
	{
		return new Argb32(colour.R, colour.G, colour.B, alpha);
	}

	/// <summary>
	/// Draws a pixel into the given image processing context.
	/// </summary>
	public static void SetPixel(this IImageProcessingContext context, Color colour, int x, int y)
	{
		context.Fill(colour, new RectangleF(x, y, 1, 1));
	}

	/// <summary>
	/// Determines if both images are duplicates or not.
	/// </summary>
	public static bool IsDuplicateOf(this Image<Argb32> image, Image<Argb32> other)
	{
		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				var thisColour = image[x, y];
				var otherColour = other[x, y];

				// If both colours are fully transparent, then it doesn't matter if other components are different. This takes care of cases where different images might use different RGB components for transparent pixels. If both are fully transparent, we should treat them the same regardless of other components.
				if (thisColour.A == 0 && otherColour.A == 0) break;

				// If semi or non-transparent colour is different, the images are not the same.
				if (thisColour != otherColour) return false;
			}
		}

		return true;
	}

}
