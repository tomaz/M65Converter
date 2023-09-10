namespace M65Converter.Sources.Data.Intermediate;

/// <summary>
/// Container for <see cref="ImageData"/>.
/// </summary>
public class ImagesContainer
{
	/// <summary>
	/// The list of all items.
	/// </summary>
	public IReadOnlyList<ImageData> Images { get => images; }

	/// <summary>
	/// Returns transparent item. If multiple items are transparent, the first one is returned. If no item is transparent, null is returned.
	/// </summary>
	public ImageData? TransparentImage { get; private set; }

	/// <summary>
	/// Returns the index of the transparent item.
	/// </summary>
	public int TransparentImageIndex { get; private set; }

	private List<ImageData> images = new();
	private int restorePointIndex = 0;

	#region Managing data

	/// <summary>
	/// Clears all parsed data.
	/// </summary>
	public void Clear()
	{
		images.Clear();
		TransparentImage = null;
		TransparentImageIndex = -1;
	}

	/// <summary>
	/// Adds the given image to the container if the given options allow it.
	/// </summary>
	/// <param name="image">Image to add.</param>
	/// <param name="transparencyOptions">Transparency options for filtering.</param>
	/// <param name="duplicatesOptions">Duplicates options for filtering.</param>
	/// <returns>Returns the result of the addition.</returns>
	public AddResult AddImage(
		ImageData image,
		TransparencyOptionsType transparencyOptions,
		DuplicatesOptionsType duplicatesOptions
	)
	{
		// If transparency options don't allow adding, return.
		var transparencyResult = FilterAddByTransparency(image, transparencyOptions);
		if (transparencyResult != null) return transparencyResult;

		// If duplicates options don't allow adding, return.
		var duplicatesResult = FilterAddByDuplicates(image, duplicatesOptions);
		if (duplicatesResult != null) return duplicatesResult;

		// Remember transparent item if we don't yet have one.
		if (image.IsFullyTransparent && TransparentImage == null)
		{
			TransparentImageIndex = images.Count;
			TransparentImage = image;
		}

		// Add item to the end of the list.
		images.Add(image);

		// Return the addition result.
		return new()
		{
			WasAdded = true,
			ItemIndex = images.Count - 1,
		};
	}

	/// <summary>
	/// Adds transparent item if none is yet present.
	/// </summary>
	public AddResult AddTransparentImage(int width, int height)
	{
		// If we already have transparent item, we don't have to add a new one.
		if (TransparentImage != null) return new()
		{
			WasAdded = false,
			ItemIndex = TransparentImageIndex,
		};

		// Assign transparent item to properties.
		TransparentImageIndex = images.Count;
		TransparentImage = new ImageData
		{
			Image = new Image<Argb32>(
				width: width,
				height: height,
				backgroundColor: new Argb32(r: 0, g: 0, b: 0, a: 0)
			),
			IndexedImage = new(),
			IsFullyTransparent = true,
		};

		// Prepare palette and indices image.
		TransparentImage.Palette.Add(new Argb32(r: 0, g: 0, b: 0, a: 0));
		TransparentImage.IndexedImage.Prefill(
			width: TransparentImage.Image.Width,
			height: TransparentImage.Image.Height,
			index: 0
		);

		// Add it to the list.
		images.Add(TransparentImage);

		// Return add result.
		return new()
		{
			WasAdded = true,
			ItemIndex = images.Count - 1,
		};
	}

	#endregion

	#region Restore point

	/// <summary>
	/// Sets restore point to current data.
	/// </summary>
	public void SetRestorePoint()
	{
		restorePointIndex = images.Count - 1;
	}

	/// <summary>
	/// Resets data to last set restore point.
	/// 
	/// If restore point was never set, this is the same as calling <see cref="Clear"/>.
	/// </summary>
	public void ResetDataToRestorePoint()
	{
		if (restorePointIndex >= images.Count) return;

		images.RemoveRange(
			index: restorePointIndex, 
			count: images.Count - restorePointIndex
		);
	}

	#endregion

	#region Helpers

	private AddResult? FilterAddByTransparency(ImageData item, TransparencyOptionsType transparencyOptions)
	{
		switch (transparencyOptions)
		{
			case TransparencyOptionsType.OpaqueOnly:
				// Fully transparent items are reused from existing one.
				if (item.IsFullyTransparent) return new()
				{
					WasAdded = false,
					ItemIndex = TransparentImageIndex
				};
				break;

			case TransparencyOptionsType.KeepAll:
				// If we want to keep all, we add items regardless of transparency.
				break;
		}

		// Return null since we need to manage addition outside.
		return null;
	}

	private AddResult? FilterAddByDuplicates(ImageData item, DuplicatesOptionsType duplicatesOptions)
	{
		switch (duplicatesOptions)
		{
			case DuplicatesOptionsType.UniqueOnly:
			{
				// If we already have exactly the same item, we should ignore the new one.
				var index = 0;

				foreach (var existingItem in images)
				{
					if (item.IsDuplicateOf(existingItem))
					{
						return new()
						{
							WasAdded = false,
							ItemIndex = index,
						};
					}

					index++;
				}
				break;
			}

			case DuplicatesOptionsType.KeepAll:
			{
				// We don't care if there are duplicates or not.
				break;
			}
		}

		// Returning null indicates this item is unique.
		return null;
	}

	#endregion

	#region Declarations

	public class AddResult
	{
		public bool WasAdded { get; set; }
		public int ItemIndex { get; set; }
	}

	#endregion
}
