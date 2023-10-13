using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Parsing;
using M65Converter.Sources.Data.Providers;

namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Describes a source sprite in a generic, source type independent way.
/// </summary>
public class Sprite
{
	/// <summary>
	/// Width of the sprite in pixels.
	/// </summary>
	public int Width { get; set; }

	/// <summary>
	/// Height of the sprite in pixels.
	/// </summary>
	public int Height { get; set; }

	/// <summary>
	/// Number of characters this sprite requires to fit the whole width. Assigned during parsing.
	/// </summary>
	public int CharactersWidth { get; set; }
		
	/// <summary>
	/// Number of characters this sprite requires to fit the whole height. Assigned during parsing.
	/// </summary>
	public int CharactersHeight { get; set; }

	/// <summary>
	/// Name of the sprite - usually derived from the name of the source file.
	/// </summary>
	public string SpriteName { get; set; } = null!;

	/// <summary>
	/// Filename and path of the source where the sprite was parsed from.
	/// </summary>
	public string SourceFilename { get; set; } = null!;

	/// <summary>
	/// The list of all frames.
	/// </summary>
	public List<FrameData> Frames { get; set; } = new();

	#region Initialization & Disposal

	/// <summary>
	/// Parses input. Input can etiher be:
	/// 
	/// - Aseprite file
	/// - BMP, PNG, JPG file
	/// 
	/// Either way, the method creates new <see cref="Sprite"/> instance describing parsed data.
	/// </summary>
	public static Sprite Parse(IStreamProvider input, Size? frameSize = null)
	{
		var extension = Path.GetExtension(input.GetFilename()).ToLower();

		return extension switch
		{
			".ase" or ".aseprite" => new AsepriteSpriteParser().Parse(input),
			".bmp" or ".png" or ".jpg" or ".jpeg" => new ImageSpriteParser().Parse(input, frameSize),
			_ => throw new InvalidDataException($"Unknown input type {input.GetFilename()}"),
		};
	}

	#endregion

	#region Declarations

	/// <summary>
	/// Describes individual frame.
	/// </summary>
	public class FrameData
	{
		/// <summary>
		/// Duration of this frame in milliseconds.
		/// </summary>
		public int Duration { get; set; }

		/// <summary>
		/// Frame bitmap image.
		/// </summary>
		public Image<Argb32> Image { get; set; } = null!;

		/// <summary>
		/// Indexed image, representing indices to characters in chars container. Assigned from outside class, only available after parsing completes.
		/// </summary>
		public IndexedImage IndexedImage { get; set; } = null!;
	}

	#endregion
}
