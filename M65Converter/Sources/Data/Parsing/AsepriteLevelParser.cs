using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Parsing;

/// <summary>
/// Parses <see cref="LevelData"/> from Aseprite file.
/// </summary>
public class AsepriteLevelParser
{
	#region Parsing

	public LevelData Parse(IStreamProvider source)
	{
		var path = source.GetFilename();
		Logger.Verbose.Message($"Parsing {Path.GetFileName(path)}");
		var aseprite = AsepriteData.Parse(source);

		// For levels we assume there's only 1 frame. But if more, we always take the first.
		Logger.Verbose.Message("Preparing layers");
		var frame = aseprite.GeneratedFrames.First();
		var layers = frame.LayerImages.Select((x, i) => new LevelData.LayerData
		{
			Path = path,
			Name = frame.LayerNames[i],
			Image = x,
		});

		// Prepare composite layer, it's more accurate representation if merged layers are needed as it takes care of layer transparency etc.
		var composite = new LevelData.LayerData
		{
			Path = path,
			Name = frame.LayerNames.First(),
			Image = frame.CompositeImage
		};

		return new LevelData
		{
			Width = aseprite.AsepriteHeader.Width,
			Height = aseprite.AsepriteHeader.Height,
			LevelName = Path.GetFileNameWithoutExtension(path),
			RootFolder = Path.GetDirectoryName(path)!,
			CompositeLayer = composite,
			Layers = layers.ToList()
		};
	}

	#endregion
}
