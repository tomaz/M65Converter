using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

using static M65Converter.Sources.Helpers.Images.ImageExtensions;

namespace M65Converter.Sources.Helpers.Converters.Palette;

public class PaletteMerger4Bit : PaletteMerger
{
	private static readonly int MaxBanks = 16;
	private static readonly int MaxColoursPerBank = 16;

	#region Overrides

	protected override void OnMerge(IReadOnlyList<ImageData> images, List<ColourData> palette)
	{
		// Merging 4-bit palette is quite a complex operation composed of several steps. In a nutshell: we need to inteligently parse the colours and try to fit as many images as possible into each 16-colour bank. The first colour in each bank is considered transparent if transparency is enabled. At this point we already have all distinct colours of each image in the `ImageData` provided to us so we can work on that. However we must ensure each image only has up to 16 colours.
		ValidateColoursPerImage(images);

		// Once we are sure each image only uses up to 16 colours, we try to find the best fit into 16-colour banks. Colours are reused as much as possible and multiple objects are inserted into the same bank as long as they can fit. But algorithm is not perfect, it may result in unused gaps or duplicated colours. Or in worse case, for very complex images with lots of objects and colours, it will outright not be able to fit all objects (ideally this would be transformed into recursive algorithm where different paths would be attempted, but for now it works for me). In such case, palette should be exported from image editor and loaded manually, provided the image editor is able to maintain 16-colour banks. If not, then the only way is to simplify the image...
		var banks = MapImagesIntoBanks(images);

		// If we were able to determine banks, combine them into single uniform palette. The main thing here is to ensure "missing" colours at the end of the bank are filled in.
		FillExtraColours(banks);

		// Finally, we prepare final merged palette that contains all colours from all banks.
		// Note: with this indexed images will no longer point to correct colours, however they will have correct bank assigned so we can still get proper colour if needed.
		// Also note: in 4-bit mode, the palette may have duplicated colours. That's expected, for example, first colour in each bank is reserved for transparent pixels. But some colours may also need to be duplicated if used in multiple images which would not all fit into the same bank.
		PrepareMergedPalette(banks, palette);

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{banks.Count} colour banks used");
	}

	#endregion

	#region Helpers

	private void ValidateColoursPerImage(IReadOnlyList<ImageData> images)
	{
		Logger.Verbose.Message($"Validating images are composed of max {MaxColoursPerBank} colours");

		var index = -1;
		foreach (var image in images)
		{
			index++;

			var count = image.Palette.CountWithTransparent(Options.IsUsingTransparency);
			if (count > MaxColoursPerBank)
			{
				ThrowException($"Image {index} requires {count} colours, 4-bit mode only supports up to {MaxColoursPerBank}!");
			}
		}
	}

	private List<ColourBank> MapImagesIntoBanks(IReadOnlyList<ImageData> images)
	{
		Logger.Verbose.Separator();
		Logger.Debug.Message("Mapping images into colour banks");
		List<ColourBank> result = new();

		ColourBank? FindBestBank(ImageData image)
		{
			ColourBank? bestBank = null;
			var bestColoursRequired = int.MaxValue;

			foreach (var existingBank in result)
			{
				var newColoursRequired = existingBank.NewColoursIfAdded(image, Options);
				var isPossible = newColoursRequired != null;
				var isBetterFit = newColoursRequired < bestColoursRequired;
				if (isPossible && isBetterFit)
				{
					bestColoursRequired = newColoursRequired!.Value;
					bestBank = existingBank;
				}
			}

			return bestBank;
		}

		string CharLog(ImageData image)
		{
			if (image.IsFullyTransparent)
			{
				return $"Char uses {image.Palette.Count} colours (fully transparent)";
			}
			else
			{
				return $"Char uses {image.Palette.Count} colours";
			}
		}

		string BankLog(ColourBank bank, int addedColours, bool isNewBank = false)
		{
			var addedColoursInfo = isNewBank
				? $"{addedColours} new colours (1 transparent + {addedColours - 1} from char)"
				: $"{addedColours} new colours";

			return addedColours > 0
				? $"Added {addedColoursInfo} for total of {bank.Colours.Count}"
				: $"Added {addedColoursInfo}, bank has {bank.Colours.Count}";
		}

		string RemapLog(int charIndex, bool isRemapped)
		{
			return isRemapped
				? $"Remapping colour indices for char {charIndex}:"
				: $"No remap needed for char {charIndex}:";
		}

		var index = -1;
		foreach (var image in images)
		{
			index++;

			// Try to find fit the image into existing banks. If we find one, add image to it.
			var existingBank = FindBestBank(image);
			if (existingBank != null)
			{
				Logger.Verbose.Option($"Char {index}: adding to existing bank {existingBank.BankIndex} (of {result.Count} total)");

				var originalColours = existingBank.Colours.Count;
				var addResult = existingBank.AddImage(image, index);

				var newColours = existingBank.Colours.Count;
				var addedColours = newColours - originalColours;

				Logger.Verbose.SubOption($"Bank now has {existingBank.Images.Count} chars");
				Logger.Verbose.SubOption(CharLog(image));
				Logger.Verbose.SubOption(BankLog(existingBank, addedColours));
				if (addResult.Formatter != null)
				{
					Logger.Verbose.SubOption(RemapLog(index, addResult.IsChanged));
					addResult.Formatter.Log(Logger.Verbose.SubOption);
				}
			}
			else
			{
				Logger.Verbose.Option($"Char {index}: adding to new bank (now have {result.Count + 1} banks)");

				// If we didn't find possible bank, we need to create a new one.
				var newBank = new ColourBank
				{
					BankIndex = result.Count
				};

				// Add the image to the bank and bank to the resulting banks array.
				// Note: at this point we don't care of maximum number of banks, we blindly create as many as we need. We will validate after all images are finished. We could just as well bail out early, however this way we can log the actual number of banks needed to fit all images to the user as convenience.
				var addResult = newBank.AddImage(image, index);
				result.Add(newBank);

				var addedColours = newBank.Colours.Count;
				Logger.Verbose.SubOption(CharLog(image));
				Logger.Verbose.SubOption(BankLog(newBank, addedColours, isNewBank: true));
				if (addResult.Formatter != null)
				{
					Logger.Verbose.SubOption(RemapLog(index, addResult.IsChanged));
					addResult.Formatter.Log(Logger.Verbose.SubOption);
				}
			}
		}

		// When all images are handled, validate banks count is valid.
		if (result.Count > MaxBanks)
		{
			throw new InvalidCompositeImageDataException($"Couldn't fit all chars into {MaxBanks} banks ({result.Count} banks are needed)");
		}

		return result;
	}

	private void FillExtraColours(List<ColourBank> banks)
	{
		Logger.Verbose.Separator();
		Logger.Verbose.Message($"Filling all banks to {MaxColoursPerBank} colours");

		var index = -1;
		foreach (var bank in banks)
		{
			index++;

			var originalColoursCount = bank.Colours.Count;
			var colourIndex = -1;

			Logger.Verbose.Option($"Bank {index}: {bank.Images.Count} chars");

			// Log existing colours.
			Logger.Verbose.SubOption($"{originalColoursCount} colours used");
			foreach (var colour in bank.Colours)
			{
				colourIndex++;
				Logger.Verbose.SubSubOption($"{colourIndex}: {colour}");
			}

			if (bank.Colours.Count < MaxColoursPerBank)
			{
				Logger.Verbose.SubOption($"{MaxColoursPerBank - originalColoursCount} extra colours added");
				while (bank.Colours.Count < MaxColoursPerBank)
				{
					colourIndex++;

					var colour = new Argb32(r: 255, g: 0, b: 255, a: 255);  // magenta to let it stick out
					Logger.Verbose.SubSubOption($"{colourIndex}: {colour}");

					bank.Colours.Add(new()
					{
						Colour = colour,
						IsUsed = false
					});
				}
			}
		}
	}

	private void PrepareMergedPalette(List<ColourBank> banks, List<ColourData> palette)
	{
		Logger.Verbose.Separator();
		Logger.Verbose.Message("Preparing merged global palette");
		
		foreach (var bank in banks)
		{
			foreach (var colour in bank.Colours)
			{
				palette.Add(colour);
			}
		}
	}

	#endregion

	#region Declarations

	private class ColourBank
	{
		/// <summary>
		/// Bank index.
		/// </summary>
		public int BankIndex { get; set; } = 0;

		/// <summary>
		/// All the colours of this bank.
		/// </summary>
		public List<ColourData> Colours { get; } = new();

		/// <summary>
		/// All the images that use colours from this bank.
		/// </summary>
		public List<ImageData> Images { get; } = new();

		#region Overrides

		public override string ToString() => $"{Colours.Count} colours, {Images.Count} images";

		#endregion

		#region Public

		/// <summary>
		/// Determines how many new colours would be added to this bank if the given image would be added to this bank.
		/// 
		/// If adding the image would result in overflowing the bank, null is returned. Otherwise the number of new colours that would be added to bank's palette is returned.
		/// </summary>
		public int? NewColoursIfAdded(ImageData image, OptionsType options)
		{
			// Determine all colours that would need to be added into this bank to fit the image.
			var addedColours = new List<Argb32>();
			foreach (var colour in image.Palette)
			{
				if (!Colours.Contains(colour))
				{
					addedColours.Add(colour);
				}
			}

			// We must also consider transparency - if adding first image to the bank and the image doesn't use transparent colour, we must account for the transparent colour that will be added as first colour of the bank - should the image be later actually added.
			// Note: we don't have to check for transparency if adding to non-empty bank. In this case we already added transparent colour so we're fine.
			var addedColoursCount = addedColours.Count;
			if (options.IsUsingTransparency && Colours.Count == 0 && addedColours.TransparentIndex() < 0)
			{
				addedColoursCount++;
			}

			// If we can fit the image into the bank, return the number of new colours.
			if (Colours.Count + addedColoursCount <= MaxColoursPerBank)
			{
				return addedColoursCount;
			}

			// Otherwise return null to indicate we can't fit the image into this bank.
			return null;
		}

		/// <summary>
		/// Adds the image and adjusts colours.
		/// 
		/// This function will fail if bank will overflow - use <see cref="NewColoursIfAdded(ImageData, OptionsType)"/> to check first!
		/// </summary>
		public RemapResult AddImage(ImageData image, int imageIndex)
		{
			// If the bank is empty, we need to insert transparent colour first.
			if (Colours.Count == 0)
			{
				Colours.Add(new()
				{
					Colour = new Argb32(r: 0, g: 0, b: 0, a: 0)
				});
			}

			// Append all unique colours of the image.
			var mapping = Colours.MergeColours(image.Palette);

			// Remap indexed image.
			var remapResult = image.IndexedImage.RemapWithFormatter(mapping);

			// Record bank info to the image. This is mainly used for later correct colours handling.
			image.IndexedImage.Bank = BankIndex;
			image.IndexedImage.ColoursPerBank = MaxColoursPerBank;

			// Add image to this bank.
			Images.Add(image);

			return remapResult;
		}

		#endregion
	}

	#endregion
}
