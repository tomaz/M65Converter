using M65Converter.Sources.Data.Intermediate;

namespace M65Converter.Sources.Helpers.Images;

/// <summary>
/// Splits image into smaller chunks.
/// </summary>
public class ImageSplitter
{
	/// <summary>
	/// The width of each part.
	/// </summary>
	public int ItemWidth { get; set; }

	/// <summary>
	/// The height of each part.
	/// </summary>
	public int ItemHeight { get; set; }

	/// <summary>
	/// Options for handling transparency.
	/// </summary>
	public TransparencyOptionsType TransparencyOptions { get; set; } = TransparencyOptionsType.OpaqueOnly;

	/// <summary>
	/// Options for handling duplicates.
	/// </summary>
	public DuplicatesOptionsType DuplicatesOptions { get; set; } = DuplicatesOptionsType.UniqueOnly;

	#region Public

	/// <summary>
	/// Splits the given image into the given container using current property settings.
	/// </summary>
	/// <param name="source">Source image to split.</param>
	/// <param name="container">Container to add all items to</param>
	/// <returns>Returns other results from this split (in addition to items added to container).</returns>
	public SplitResult Split(Image<Argb32> source, ImagesContainer container)
	{
		var result = new SplitResult();

		source.ProcessPixelRows(accessor =>
		{
			// Parse all item rows.
			for (var y = 0; y < accessor.Height; y += ItemHeight)
			{
				// If we don't have enough pixels for whole row, we're done.
				if (y + ItemHeight > accessor.Height) break;

				// Add a new row to resulting indexed image.
				result.IndexedImage.AddRow();

				// Parse all item columns.
				for (var x = 0; x < accessor.Width; x += ItemWidth)
				{
					// If we don't have enough pixels for whole column, continue with next row.
					if (x + ItemWidth > accessor.Width) break;

					var item = new ImageData
					{
						Image = new Image<Argb32>(width: ItemWidth, height: ItemHeight),
						IndexedImage = new(),
						IsFullyTransparent = true,
					};

					// Parse all pixels of this item.
					for (var yOffset = 0; yOffset < ItemHeight; yOffset++)
					{
						var sourceRowSpan = accessor.GetRowSpan(y + yOffset);
						
						item.IndexedImage.AddRow();

						for (var xOffset = 0; xOffset < ItemWidth; xOffset++)
						{
							var colour = sourceRowSpan[x + xOffset];

							// If we have at least one non transparent colour, the item is not fully transparent.
							if (colour.A != 0) item.IsFullyTransparent = false;

							// Set the colour for the destination image.
							item.Image.Mutate(op => op.SetPixel(colour, xOffset, yOffset));

							// Prepare palette index. Note: at this point it's "local" image palette index only, we'll later update it to global palette once all images are ready.
							var colourIndex = item.Palette.IndexOf(colour);
							if (colourIndex < 0)
							{
								colourIndex = item.Palette.Count;
								item.Palette.Add(colour);
							}

							// Add palette index entry to indexed image.
							item.IndexedImage.AddColumn(colourIndex);
						}
					}

					// Add the item to container (this may or may not add the item based on given options).
					var addResult = container.AddImage(
						image: item,
						transparencyOptions: TransparencyOptions,
						duplicatesOptions: DuplicatesOptions
					);

					// Add data to indexed image.
					if (addResult.ItemIndex >= 0)
					{
						result.IndexedImage.AddColumn(addResult.ItemIndex);
					}

					// Increment results.
					if (addResult.WasAdded) result.AddedCount++;
					result.ParsedCount++;
				}
			}
		});

		return result;
	}

	#endregion

	#region Declarations

	public class SplitResult
	{
		/// <summary>
		/// Source image represented as character indices.
		/// </summary>
		public IndexedImage IndexedImage { get; set; } = new();

		/// <summary>
		/// Number of characters detected.
		/// </summary>x
		public int ParsedCount { get; set; }

		/// <summary>
		/// Number of new characters added to container.
		/// </summary>
		public int AddedCount { get; set; }
	}

	#endregion
}
