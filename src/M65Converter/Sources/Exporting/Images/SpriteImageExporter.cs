using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Exporting.Images.Helpers;

namespace M65Converter.Sources.Exporting.Images;

/// <summary>
/// Exports info image for a given set of sprites.
/// </summary>
public class SpriteImageExporter : BaseImageExporter
{
	/// <summary>
	/// The list of sprites to render.
	/// </summary>
	public IReadOnlyList<SpriteExportData> Sprites { get; init; } = null!;

	private List<ImageBoxHandler<SpriteExportData.CharData>> Handlers { get; } = new();

	#region Overrides

	protected override void OnResetCalculations()
	{
		Handlers.Clear();
	}

	protected override Size OnCalculateDrawingSize(Measures measures)
	{
		MeasureSprites(measures);

		return measures.MeasuredSize;
	}

	protected override void OnDraw(DrawInfo drawings)
	{
		DrawSprites(drawings);
	}

	#endregion

	#region Measuring

	private void MeasureSprites(Measures measures)
	{
		var offsetX = Measures.Position.ImagePadding;
		var offsetY = Measures.Position.ImagePadding;

		foreach (var sprite in Sprites)
		{
			measures.MeasureBoxedData(offsetX, offsetY, builder =>
			{
				var handler = new ImageBoxHandler<SpriteExportData.CharData>();

				handler.Measure(new ImageBoxHandler<SpriteExportData.CharData>.MeasureData
				{
					GlobalOptions = Data.GlobalOptions,
					Measures = measures,
					BoxMeasurer = builder,

					Width = sprite.CharactersWidth * sprite.Frames.Count,
					Height = sprite.CharactersHeight,
					RowSize = sprite.CharactersWidth * Data.GlobalOptions.CharInfo.BytesPerCharIndex,	// each frame is its own "row", see more detailed description in `DrawSprites` below

					IsUsingElevatedTitle = true,
					IsUsingImages = true,

					ItemAt = (x, y) => sprite[x, y],
					SectionName = (x, y, item) => sprite.IsFirstColumnOfFrame(x) ? (x / sprite.CharactersWidth).ToString() : null,
					SeparatorBefore = (x, y, item) => sprite.IsFirstColumnOfFrame(x),
				});

				Handlers.Add(handler);
			});

			// Each next sprite is rendered below previous one.
			offsetY = Measures.Position.LastEnd;
		}
	}

	#endregion

	#region Drawing

	private void DrawSprites(DrawInfo info)
	{
		for (var i = 0; i < Handlers.Count; i++)
		{
			var handler = Handlers[i];
			var sprite = Sprites[i];

			ImageData? currentItemImage = null;

			handler.Draw(new ImageBoxHandler<SpriteExportData.CharData>.DrawData
			{
				GlobalOptions = Data.GlobalOptions,
				DrawInfo = info,
				Title = sprite.SpriteName,

				WillStartHandlingItem = (item) => currentItemImage = Data.CharsContainer.Images[item.CharIndex],
				IsImageFullyTransparent = (item) => currentItemImage?.IsFullyTransparent ?? false,
				ItemImage = (item) => currentItemImage?.Image,
				ItemText = (item) => item.LittleEndianData.ToString("X4"),

				// Our data doesn't comply with the typical 2D layout where data is written to file row by row. In those cases X address can simply be accumulated when displayed in top header (0, 2, 4, 6 etc) and when the value for each column is added to left header offset, the result is the actual offset of each element from the start of the data. With sprites though, the data in the file is rendered frame by frame, then each frame row by row, but in the image we want to display each frame in a meaningful way (frame by frame horizontally). This means that accumulated offsets will be off. There's no way of drawing sprite frames nicely AND have headers that would yield exact offsets, so instead we show offset relative to each frame separately by alternating 0, 2, 0, 2 in top header and show offset into frame rows with left header (which is accomplished with `MeasureData.RowSize` when measuring - see above).
 				AddressXOffset = (item, x, p) => sprite.IsFirstColumnOfFrame(x) ? 0 : p,
			});
		}
	}

	#endregion
}
