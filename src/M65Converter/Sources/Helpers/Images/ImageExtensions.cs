using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;

using SixLabors.ImageSharp.Drawing.Processing;

namespace M65Converter.Sources.Helpers.Images;

public static class ImageExtensions
{
	#region Images

	/// <summary>
	/// Draws the given image at the given destination rectangle.
	/// </summary>
	public static void DrawImageAt(this IImageProcessingContext context, Image<Argb32> image, Rectangle destination)
	{
		var imageToDraw = image;

		// If needed we need to scale the image to fit destination.
		if (image.Width != destination.Width || image.Height != destination.Height)
		{
			imageToDraw = new Image<Argb32>(image.Width, image.Height);

			// Draw original image over the new one.
			imageToDraw.Mutate(mutator => mutator.DrawImage(image, 1f));

			// Resize image to fit desired size.
			imageToDraw.Mutate(mutator => mutator.Resize
			(
				width: destination.Width,
				height: destination.Height,
				sampler: KnownResamplers.NearestNeighbor
			));
		}

		// Draw the image into the given destination.
		context.DrawImage(
			image: imageToDraw,
			location: new Point(destination.X, destination.Y),
			opacity: 1f
		);
	}

	/// <summary>
	/// Saves the image using the given stream provider.
	/// </summary>
	public static void Save(this Image<Argb32> image, IStreamProvider output)
	{
		Stream GetStream() => output.GetStream(FileMode.CreateNew);

		switch (Path.GetExtension(output.GetFilename()).ToLower())
		{
			case ".bmp": image.SaveAsBmp(GetStream()); break;
			case ".png": image.SaveAsPng(GetStream()); break;
			case ".jpg": image.SaveAsJpeg(GetStream()); break;
			case ".jpeg": image.SaveAsJpeg(GetStream()); break;
			case ".gif": image.SaveAsGif(GetStream()); break;
			default: throw new InvalidDataException($"Unsupported image file type {output.GetFilename()}");
		}
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
				if (thisColour.A == 0 && otherColour.A == 0) continue;

				// If semi or non-transparent colour is different, the images are not the same.
				if (thisColour != otherColour) return false;
			}
		}

		return true;
	}
	
	#endregion

	#region Indexed images

	/// <summary>
	/// Remaps the given <see cref="IndexedImage"/> with logging all changes to a formatter.
	/// 
	/// Formatting only occurs if logging is set to verbose.
	/// 
	/// Since we almost always use formatting, this results in a much more compact function call. Yet still, we don't add formatting handling into the model classes, so they are independent if formatting is not desired.
	/// </summary>
	public static RemapResult RemapWithFormatter(this IndexedImage image, Dictionary<int, int> map)
	{
		var isColourRemapped = false; // used to only log item if it has new colours
		var formatter = Logger.Verbose.IsEnabled ? new TableFormatter() : null;

		image.Remap(
			map: map,
			rowCallback: (y) =>
			{
				formatter?.StartNewRow();
			},
			colourCallback: (x, y, original, merged) =>
			{
				if (merged != original)
				{
					isColourRemapped = true;
					formatter?.AppendData(original, merged);
				}
				else
				{
					formatter?.AppendData(original);
				}
			}
		);

		return new RemapResult
		{
			IsChanged = isColourRemapped,
			Formatter = formatter
		};
	}

	#endregion

	#region Colours list

	/// <summary>
	/// Returns the count taking into account transparent colour if needed.
	/// 
	/// If <see cref="requiresTransparent"/> is true and this colours list doesn't contain transparent colour, then count + 1 is returned. If transparent colour is not required or is present, then count is returned.
	/// </summary>
	public static int CountWithTransparent(this IReadOnlyList<Argb32> colours, bool requiresTransparent)
	{
		if (requiresTransparent && colours.TransparentIndex() < 0)
		{
			return colours.Count + 1;
		}

		return colours.Count;
	}

	/// <summary>
	/// Returns the index of the transparent colour in the given list of colours or -1 if it doesn't contain transparent colour.
	/// </summary>
	public static int TransparentIndex(this IReadOnlyList<Argb32> colours)
	{
		var index = 0;

		foreach (var colour in colours)
		{
			if (colour.IsTransparent())
			{
				return index;
			}

			index++;
		}

		return -1;
	}

	/// <summary>
	/// Determines if the given ARGB is part of this colours list.
	/// </summary>
	public static bool Contains(this List<ColourData> colours, Argb32 find)
	{
		return colours.IndexOf(find) > 0;
	}

	/// <summary>
	/// Finds an index of the given ARGB colour in this list.
	/// </summary>
	public static int IndexOf(this List<ColourData> colours, Argb32 find)
	{
		return colours.FindIndex(c => c.Colour == find);
	}

	/// <summary>
	/// Merges the given list of source colours into this palette (both are represented as list of colours).
	/// 
	/// This function takes care of unifying transparent colour (when alpha is 0, it doesn't matter what RGB components are, it will always be treated as fully transparent colour). When completed, this palette will contain all previous colours plus all unique colours from the given source list.
	/// 
	/// Additionally, it prepares a mapping dictionary where the key is original source index and value is mapped index into the merged palette. This is useful for adjusting indexed images.
	/// 
	/// Optionally a callback action can be assigned. It will be called for every colour from source telling with parameters:
	/// - bool: true if colour was added to palette, false if it was already present before
	/// - int: original colour index (from source)
	/// - int: new colour index (in destination palette)
	/// - Argb32: the colour that was handled
	/// </summary>
	public static Dictionary<int, int> MergeColours(
		this List<ColourData> palette, 
		IReadOnlyList<Argb32> from,
		Action<bool, int, int, Argb32>? callback = null)
	{
		var result = new Dictionary<int, int>();

		for (var i = 0; i < from.Count; i++)
		{
			var originalColour = from[i];

			// If this is transparent colour, convert it so RGB will always match. This way we won't end up with 2 transparent colours which have different RGB components.
			var colour = originalColour;
			if (colour.A == 0)
			{
				colour = new Argb32(r: 0, b: 0, g: 0, a: 0);
			}

			// Find the colour in this palette. If not found, we need to add it. As we're adding to the end, the index is current count, so adjust it.
			var index = palette.IndexOf(colour);
			if (index < 0)
			{
				index = palette.Count;
				palette.Add(new() { Colour = colour });
				callback?.Invoke(true, i, index, originalColour);
			}
			else
			{
				callback?.Invoke(false, i, index, originalColour);
			}

			result[i] = index;
		}

		return result;
	}

	#endregion

	#region Colours

	/// <summary>
	/// Creates a new colour using the same RGB components but given alpha.
	/// </summary>
	public static Argb32 WithAlpha(this Argb32 colour, int alpha)
	{
		return new Argb32(r: colour.R, g: colour.G, b: colour.B, a: (byte)alpha);
	}

	/// <summary>
	/// Determines if this colour is fully transparent.
	/// </summary>
	public static bool IsTransparent(this Argb32 colour)
	{
		return colour.A == 0;
	}

	/// <summary>
	/// Determines if the colour is dark (result is true) or light (result is false).
	/// </summary>
	public static bool IsDark(this Argb32 color)
	{
		var r = (double)color.R;
		var g = (double)color.G;
		var b = (double)color.B;

		var brightness = Math.Sqrt(0.299 * r * r + 0.587 * g * g + 0.114 * b * b);

		return brightness < 127.5;
	}

	#endregion

	#region Declarations

	public class RemapResult
	{
		public bool IsChanged { get; set; }
		public TableFormatter? Formatter { get; set; }
	}

	#endregion
}
