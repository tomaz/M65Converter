using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Parsing;

/// <summary>
/// Parses <see cref="Sprite"/> from a bitmap image.
/// </summary>
public class ImageSpriteParser
{
	#region Public

	public Sprite Parse(IStreamProvider source, Size? frameSize)
	{
		var path = source.GetFilename();
		Logger.Verbose.Message($"Parsing {Path.GetFileName(path)}");

		var image = Image.Load<Argb32>(source.GetStream(FileMode.Open));
		var size = frameSize ?? image.Size;
		var frames = new List<Sprite.FrameData>();

		if (frameSize == null)
		{
			ParseWholeImage(frames, image, size);
		}
		else
		{
			ParseSplits(frames, image, size);
		}

		return new Sprite
		{
			Width = size.Width,
			Height = size.Height,
			SpriteName = Path.GetFileNameWithoutExtension(path),
			SourceFilename = path,
			Frames = frames,
		};
	}

	#endregion

	#region Helpers

	private void ParseWholeImage(List<Sprite.FrameData> destination, Image<Argb32> source, Size frameSize)
	{
		// We just create 1 frame from the whole image.
		destination.Add(new Sprite.FrameData
		{
			Duration = 0,
			Image = source
		});
	}

	private void ParseSplits(List<Sprite.FrameData> destination, Image<Argb32> source, Size frameSize)
	{
		var splitter = new ImageSplitter
		{
			ItemWidth = frameSize.Width,
			ItemHeight = frameSize.Height,
			TransparencyOptions = Intermediate.Helpers.TransparencyOptions.KeepAll,
			TransparentImageInsertion = Intermediate.Helpers.TransparentImageInsertion.None,
			TransparentImageInsertionRule = Intermediate.Helpers.TransparentImageRule.AlwaysAdd,
			DuplicatesOptions = Intermediate.Helpers.DuplicatesOptions.KeepAll,
			ParsingOrder = Intermediate.Helpers.ParsingOrder.RowByRow
		};

		var container = new ImagesContainer();
		splitter.Split(source, container);

		destination.AddRange(
			container.Images.Select(x => new Sprite.FrameData
			{
				Duration = 0,
				Image = x.Image
			})
		);
	}

	#endregion
}
