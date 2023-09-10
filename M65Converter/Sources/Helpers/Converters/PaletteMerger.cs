using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Converters.Palette;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Helpers.Converters;

/// <summary>
/// Merges palettes from multiple images into single global palette.
/// 
/// Input:
/// - A list of <see cref="ImageData"/> with local palettes and locally indexed image data.
/// 
/// Results:
/// - Global palette with unique colours.
/// - All indexed images of <see cref="ImageData"/> objects point to global palette.
/// </summary>
public abstract class PaletteMerger
{
	protected OptionsType Options { get; private set; } = null!;

	#region Initialization & Disposal

	public static PaletteMerger Create(OptionsType options)
	{
		PaletteMerger result = options.Is4Bit
			? new PaletteMerger4Bit()
			: new PaletteMerger8Bit();

		result.Options = options;

		return result;
	}

	protected PaletteMerger()
	{
	}

	#endregion

	#region Subclass

	/// <summary>
	/// Called when the palette needs to be merged from the given list of images.
	/// 
	/// Subclass should merge all image colours into the given palette.
	/// </summary>
	protected abstract void OnMerge(IReadOnlyList<ImageData> images, List<ColourData> palette);

	#endregion

	#region Public

	/// <summary>
	/// Merges all colours into global palette.
	/// </summary>
	public List<ColourData> Merge()
	{
		Logger.Debug.Message("Merging palette");

		var result = new List<ColourData>();

		OnMerge(Options.Images, result);

		if (result.Count > 256)
		{
			ThrowException($"Too many colours ({result.Count} found, 256 allowed). Adjust source files to use less colours");
		}

		Logger.Debug.Message($"{result.Count} palette colours used");

		return result;
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Throws an exception.
	/// 
	/// The main responsibility of this method is to select exception type based on options.
	/// </summary>
	protected void ThrowException(string message, Exception? inner = null)
	{
		if (Options.IsCompositeImage)
		{
			throw new InvalidCompositeImageDataException(message, inner);
		}
		else
		{
			throw new InvalidDataException(message, inner);
		}
	}

	#endregion

	#region Declarations

	public class OptionsType
	{
		/// <summary>
		/// Specifies whether we require 4-bit or 8-bit colours.
		/// </summary>
		public bool Is4Bit { get; init; }

		/// <summary>
		/// Indicates whether transparency should be used or not.
		/// </summary>
		public bool IsUsingTransparency { get; init; }

		/// <summary>
		/// Specifies whether the palette is created from composite image or not.
		/// </summary>
		public bool IsCompositeImage { get; init; }

		/// <summary>
		/// The list of all source images.
		/// </summary>
		public IReadOnlyList<ImageData> Images { get; init; } = null!;
	}

	#endregion
}
