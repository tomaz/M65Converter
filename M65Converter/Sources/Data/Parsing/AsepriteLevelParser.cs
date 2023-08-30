using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Parsing;

/// <summary>
/// Parses <see cref="LevelData"/> from Aseprite file.
/// </summary>
public class AsepriteLevelParser
{
	#region Parsing

	public LevelData Parse(string path)
	{
		Logger.Verbose.Message($"Parsing {Path.GetFileName(path)}");
		var aseprite = AsepriteData.Parse(path);

		// For levels we assume there's only 1 frame. But if more, we always take the first.
		Logger.Verbose.Message("Preparing layers");
		var frame = aseprite.GeneratedFrames.First();
		var layers = frame.LayerImages.Select((x, i) => new LevelData.LayerData
		{
			Path = path,
			Name = frame.LayerNames[i],
			Image = x,
		});

		return new LevelData
		{
			Width = aseprite.AsepriteHeader.Width,
			Height = aseprite.AsepriteHeader.Height,
			LevelName = Path.GetFileNameWithoutExtension(path),
			RootFolder = Path.GetDirectoryName(path)!,
			Layers = layers.ToList()
		};
	}

	#endregion
}
