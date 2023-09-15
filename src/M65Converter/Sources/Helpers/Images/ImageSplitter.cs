using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Intermediate.Images;

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
	public TransparencyOptions TransparencyOptions { get; set; } = TransparencyOptions.OpaqueOnly;

	/// <summary>
	/// Options for handling duplicates.
	/// </summary>
	public DuplicatesOptions DuplicatesOptions { get; set; } = DuplicatesOptions.UniqueOnly;

	/// <summary>
	/// Specifies the order for parsing.
	/// </summary>
	public ParsingOrder ParsingOrder { get; set; } = ParsingOrder.RowByRow;

	/// <summary>
	/// Specifies how/if transparent images are inserted on column (vertically) or row basis (horizontally).
	/// 
	/// Whether the insertion is column or row based depends on <see cref="ParsingOrder"/>:
	/// - <see cref="ParsingOrder.RowByRow"/> inserts transparents on row basis
	/// - <see cref="ParsingOrder.ColumnByColumn"/> inserts on column basis
	/// </summary>
	public TransparentImageInsertion TransparentImageInsertion { get; set; } = TransparentImageInsertion.None;

	/// <summary>
	/// The rule to use when adding additional transparent images.
	/// 
	/// This is only used when adding new transparent images due to <see cref="TransparentImageInsertion"/> setting.
	/// </summary>
	public TransparentImageRule TransparentImageInsertionRule { get; set; } = TransparentImageRule.ReuseFirst;

	#region Public

	/// <summary>
	/// Splits the given image into the given container using current property settings.
	/// </summary>
	/// <param name="source">Source image to split.</param>
	/// <param name="container">Container to add all items to</param>
	/// <returns>Returns other results from this split (in addition to items added to container).</returns>
	public SplitResult Split(Image<Argb32> source, ImagesContainer container)
	{
		var generatedImages = new List<ImageData>();
		var startingTransparentImages = new List<int>();
		var endingTransparentImages = new List<int>();

		var result = new SplitResult
		{
			Items = generatedImages
		};

		// Resulting indexed image represents image/character indices for each sub-image.
		result.IndexedImage.Prefill(
			width: source.Width / ItemWidth,
			height: source.Height / ItemHeight,
			index: 0
		);

		#region Helpers

		List<SplitGroup> PrepareSplits(ref PixelAccessor<Argb32> accessor)
		{
			// Note: unfortunately we can't pass `PixelAccessor` to closures or methods due to being `ref struct`. I didn't find another way so processing methods return a list of all splits in correct order which we then parse below.
			return ParsingOrder switch
			{
				ParsingOrder.RowByRow => ProcessRowByRow(ref accessor),
				ParsingOrder.ColumnByColumn => ProcessColumnByColumn(ref accessor),
				_ => throw new InvalidDataException($"Unsupported parsing order {ParsingOrder}")
			};
		}

		ImagesContainer.AddResult AddTransparentImage()
		{
			// We must always add transparent image
			var addResult = container.AddTransparentImage(
				width: ItemWidth,
				height: ItemHeight,
				reuse: TransparentImageInsertionRule
			);

			if (addResult.WasAdded)
			{
				// Note: so far we are assuming that palette bank is the same even if we reuse previous transparent image. If this proves problematic, we will have to change `TransparentImageRule` above to always create new image.
				var image = container.Images[addResult.ItemIndex];
				generatedImages.Add(image);
			}

			return addResult;
		}

		void InsertStartingTransparentImage(SplitGroup group)
		{
			switch (TransparentImageInsertion)
			{
				case TransparentImageInsertion.Before:
				case TransparentImageInsertion.BeforeAndAfter:
				{
					// When adding starting transparent image, we should use the same palette bank as for the next "data" image.
					var addResult = AddTransparentImage();
					startingTransparentImages.Add(addResult.ItemIndex);
					break;
				}
			}
		}

		void InsertEndingTransparentImage(SplitGroup group)
		{
			switch (TransparentImageInsertion)
			{
				case TransparentImageInsertion.After:
				case TransparentImageInsertion.BeforeAndAfter:
				{
					// When adding ending transparent image, we should use the same palette bank as previous "data" image.
					var addResult = AddTransparentImage();
					endingTransparentImages.Add(addResult.ItemIndex);
					break;
				}
			}
		}

		ImageData CreateImage()
		{
			var image = new ImageData
			{
				Image = new Image<Argb32>(width: ItemWidth, height: ItemHeight),
				IndexedImage = new(),
				IsFullyTransparent = true,
			};

			// Item's indexed image represents colour indices for each pixel.
			image.IndexedImage.Prefill(
				width: ItemWidth,
				height: ItemHeight,
				index: 0
			);

			return image;
		}

		void CopyPixels(ref PixelAccessor<Argb32> source, ImageData destination, SplitData split)
		{

			// Parse all pixels of this split.
			for (var yOffset = 0; yOffset < ItemHeight; yOffset++)
			{
				var sourceRowSpan = source.GetRowSpan(split.Image.Y + yOffset);

				for (var xOffset = 0; xOffset < ItemWidth; xOffset++)
				{
					var colour = sourceRowSpan[split.Image.X + xOffset];

					// If we have at least one non transparent colour, the item is not fully transparent.
					if (colour.A != 0) destination.IsFullyTransparent = false;

					// Set the colour for the destination image.
					destination.Image.Mutate(op => op.SetPixel(colour, xOffset, yOffset));

					// Prepare palette index. Note: at this point it's "local" image palette index only, we'll later update it to global palette once all images are ready.
					var colourIndex = destination.Palette.IndexOf(colour);
					if (colourIndex < 0)
					{
						colourIndex = destination.Palette.Count;
						destination.Palette.Add(colour);
					}

					// Add palette index entry to indexed image.
					destination.IndexedImage[xOffset, yOffset] = colourIndex;
				}
			}

		}

		#endregion

		// Split the source image into smaller items.
		source.ProcessPixelRows(accessor =>
		{
			var splits = PrepareSplits(ref accessor);

			// Split the image. Note: "group" represents either a row or a column, depending on parsing order.
			foreach (var group in splits)
			{
				// If needed insert transparent image (unless already present).
				InsertStartingTransparentImage(group);

				// Handle all splits in this row or column.
				foreach (var split in group.Splits)
				{
					// If we don't have enough pixels for whole row or column, proceed with next split.
					if (split.Image.X + ItemWidth > accessor.Width) continue;
					if (split.Image.Y + ItemHeight > accessor.Height) continue;

					// Create new image item and copy pixels.
					var item = CreateImage();
					CopyPixels(ref accessor, item, split);

					// Add the item to container (this may or may not add the item based on given options).
					var addResult = container.AddImage(
						image: item,
						transparencyOptions: TransparencyOptions,
						duplicatesOptions: DuplicatesOptions
					);

					// Add item index to our images indexed image. Here indexed image represent character indices.
					result.IndexedImage[split.Index.X, split.Index.Y] = addResult.ItemIndex;

					// Increment results.
					if (addResult.WasAdded)
					{
						generatedImages.Add(container.Images[addResult.ItemIndex]);
						result.AddedCount++;
					}
					result.ParsedCount++;
				}

				// Insert ending transparent image if needed.
				InsertEndingTransparentImage(group);
			}
		});

		// Adjust resulting indexed image (again, indices represent items/characters) to contain all transparent edges.
		switch (ParsingOrder)
		{
			case ParsingOrder.RowByRow:
			{
				if (startingTransparentImages.Count > 0)
				{
					result.IndexedImage.InsertColumn(0, startingTransparentImages);
				}

				if (endingTransparentImages.Count > 0)
				{
					result.IndexedImage.AddColumn(endingTransparentImages);
				}
				break;
			}

			case ParsingOrder.ColumnByColumn:
			{
				if (startingTransparentImages.Count > 0)
				{
					result.IndexedImage.InsertRow(0, startingTransparentImages);
				}

				if (endingTransparentImages.Count > 0)
				{
					result.IndexedImage.AddRow(endingTransparentImages);
				}
				break;
			}
		}

		return result;
	}

	#endregion

	#region Helpers

	private List<SplitGroup> ProcessRowByRow(ref PixelAccessor<Argb32> accessor)
	{
		var result = new List<SplitGroup>();

		// Parse all pixel rows.
		var yIndex = -1;
		for (var y = 0; y < accessor.Height; y += ItemHeight)
		{
			yIndex++;

			var column = new SplitGroup
			{
				Index = new Point(0, yIndex)
			};

			// Parse all pixel columns of the row.
			var xIndex = -1;
			for (var x = 0; x < accessor.Width; x += ItemWidth)
			{
				xIndex++;

				column.Splits.Add(new()
				{
					Image = new Point(x, y),
					Index = new Point(xIndex, yIndex)
				});
			}

			result.Add(column);
		}

		return result;
	}

	private List<SplitGroup> ProcessColumnByColumn(ref PixelAccessor<Argb32> accessor)
	{
		var result = new List<SplitGroup>();

		// Parse all pixel columns.
		var xIndex = -1;
		for (var x = 0; x < accessor.Width; x += ItemWidth)
		{
			xIndex++;

			var row = new SplitGroup
			{
				Index = new Point(xIndex, 0)
			};

			// Parse all pixel rows of the column.
			var yIndex = -1;
			for (var y = 0; y < accessor.Height; y += ItemHeight)
			{
				yIndex++;

				row.Splits.Add(new()
				{
					Image = new Point(x, y),
					Index = new Point(xIndex, yIndex)
				});
			}

			result.Add(row);
		}

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
		/// The list of all split items.
		/// </summary>
		public IReadOnlyList<ImageData> Items { get; set; } = null!;

		/// <summary>
		/// Number of characters detected.
		/// </summary>x
		public int ParsedCount { get; set; }

		/// <summary>
		/// Number of new characters added to container.
		/// </summary>
		public int AddedCount { get; set; }
	}

	private class SplitGroup
	{
		public Point Index { get; set; }
		public List<SplitData> Splits { get; } = new();
	}

	private class SplitData
	{
		public Point Image { get; set; }
		public Point Index { get; set; }
	}

	#endregion
}
