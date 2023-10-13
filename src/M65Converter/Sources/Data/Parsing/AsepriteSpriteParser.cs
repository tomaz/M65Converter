using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Parsing;

/// <summary>
/// Parses <see cref="Sprite"/> from Aseprite file.
/// </summary>
public class AsepriteSpriteParser
{
	#region Public

	public Sprite Parse(IStreamProvider source)
	{
		var path = source.GetFilename();
		Logger.Verbose.Message($"Parsing {Path.GetFileName(path)}");

		var aseprite = Aseprite.Parse(source);

		// Prepare all frames; we always merge layers (if present) into a single composite image for sprites.
		var frames = aseprite.GeneratedFrames.Select((frame, i) => new Sprite.FrameData
		{
			Duration = aseprite.AsepriteFrames[i].FrameDuration,
			Image = frame.CompositeImage
		});

		// Prepare sprite data.
		return new Sprite
		{
			Width = aseprite.AsepriteHeader.Width,
			Height = aseprite.AsepriteHeader.Height,
			SpriteName = Path.GetFileNameWithoutExtension(path),
			SourceFilename = path,
			Frames = frames.ToList(),
		};
	}

	#endregion
}
