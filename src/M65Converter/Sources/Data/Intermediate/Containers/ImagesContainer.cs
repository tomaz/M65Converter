using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Intermediate.Images;

namespace M65Converter.Sources.Data.Intermediate.Containers;

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
	/// The list of all groups of images that need to be placed in the same palette bank.
	/// </summary>
	public IReadOnlyList<ImageGroupData> SamePaletteBankImages { get => samePaletteBankImages; }

	/// <summary>
	/// Returns transparent item. If multiple items are transparent, the first one is returned. If no item is transparent, null is returned.
	/// </summary>
	public ImageData? TransparentImage { get; private set; }

	/// <summary>
	/// Returns the index of the transparent item.
	/// </summary>
	public int TransparentImageIndex { get; private set; }

	private List<ImageData> images = new();
	private List<ImageGroupData> samePaletteBankImages = new();
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
		TransparencyOptions transparencyOptions,
		DuplicatesOptions duplicatesOptions
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
		image.ImageIndex = images.Count;
		images.Add(image);

		// Return the addition result.
		return new()
		{
			WasAdded = true,
			ItemIndex = images.Count - 1,
		};
	}

	/// <summary>
	/// Adds transparent image.
	/// 
	/// By default it only adds if none is present yet, but optionally it can be force added. In latter case existing <see cref="TransparentImage"/> and <see cref="TransparentImageIndex"/> remain unchanged if transparent image already exists.
	/// </summary>
	public AddResult AddTransparentImage(
		int width,
		int height,
		TransparentImageRule reuse = TransparentImageRule.ReuseFirst
	)
	{
		// Check if we can reuse existing transparent image.
		switch (reuse)
		{
			case TransparentImageRule.ReuseFirst:
				if (TransparentImage != null)
				{
					return new()
					{
						WasAdded = false,
						ItemIndex = TransparentImageIndex
					};
				}
				break;

			case TransparentImageRule.ReusePrevious:
				if (images.Count > 0 && images[^1].IsFullyTransparent)
				{
					return new()
					{
						WasAdded = false,
						ItemIndex = images.Count - 1,
					};
				}
				break;
		}

		// Prepare transparent image data.
		var index = images.Count;
		var image = new ImageData
		{
			Image = new Image<Argb32>(
				width: width,
				height: height,
				backgroundColor: new Argb32(r: 0, g: 0, b: 0, a: 0)
			),
			ImageIndex = index,
			IndexedImage = new(),
			IsFullyTransparent = true,
		};

		// Assign as the default transparent image if needed.
		if (TransparentImage == null)
		{
			TransparentImage = image;
			TransparentImageIndex = index;
		}

		// Prepare palette and indices image.
		image.Palette.Add(new Argb32(r: 0, g: 0, b: 0, a: 0));
		image.IndexedImage.Prefill(
			width: image.Image.Width,
			height: image.Image.Height,
			index: 0
		);

		// Add it to the list.
		images.Add(image);

		// Return add result.
		return new()
		{
			WasAdded = true,
			ItemIndex = index,
		};
	}

	/// <summary>
	/// Registers the given list of images to be added to the same palette bank.
	/// 
	/// Note: this is only used if the colour mode requires palette banks.
	/// </summary>
	public void RequireSamePaletteBank(ImageGroupData group)
	{
		samePaletteBankImages.Add(group);
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

	private AddResult? FilterAddByTransparency(ImageData item, TransparencyOptions transparencyOptions)
	{
		switch (transparencyOptions)
		{
			case TransparencyOptions.OpaqueOnly:
				// Fully transparent items are reused from existing one.
				if (item.IsFullyTransparent) return new()
				{
					WasAdded = false,
					ItemIndex = TransparentImageIndex
				};
				break;

			case TransparencyOptions.KeepAll:
				// If we want to keep all, we add items regardless of transparency.
				break;
		}

		// Return null since we need to manage addition outside.
		return null;
	}

	private AddResult? FilterAddByDuplicates(ImageData item, DuplicatesOptions duplicatesOptions)
	{
		switch (duplicatesOptions)
		{
			case DuplicatesOptions.UniqueOnly:
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

			case DuplicatesOptions.KeepAll:
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
