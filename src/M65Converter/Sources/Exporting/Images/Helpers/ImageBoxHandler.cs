using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Runners.Options;

using SixLabors.ImageSharp.Drawing.Processing;

namespace M65Converter.Sources.Exporting.Images.Helpers;

/// <summary>
/// Unifies measuring and rendering of a box of images such as screen or colour data.
/// 
/// This class is a wrapper over <see cref="BaseImageExporter.Measures.BoxMeasurer"/>. It greatly simplifies cases where we need to measure and render 2D box of (char) images and corresponding values. It also knows how to manage offsets and titles. All of this is customizable through a set of measuring and drawing callbacks to allow each caller to tweak the outcome.
/// 
/// The main reasoning for this abstraction is the bulk of the code is exactly the same, so wrapping it in a reusable class allows us to share implementation and not introduce the complexity in mulitple places in the code. But also, this code is somewhat hacky and that's definitely something I prefer to contain and not spread throughout the codebase. For example the underlying measuring data was copied from the solution used for screen layer measuring/rendering and then generalized to support other data. So the underlying data structures that hold data between measuring and drawing are not the best suit for a general 2D table handling (for example section titles are stored at item level and we have to ensure we only ask/draw them for the top most line). The class and data would definitely benefit from future refactoring, at which point we'd end up with more predictable results in all places where this is used.
/// </summary>
public class ImageBoxHandler<T> where T : class
{
	private BoxesData? MeasuredBoxes { get; set; }

	/// <summary>
	/// Measures the box.
	/// </summary>
	public void Measure(MeasureData data)
	{
		var result = new BoxesData
		{
			MaxLeftHeaderSize = data.MaxLeftHeaderSize,
			IsUsingImages = data.IsUsingImages,
			IsFirstSectionTitleVisible = data.IsUsingElevatedTitle,
		};

		var scaledCharSize = data.Measures.Scaled(data.GlobalOptions.CharInfo.Size);
		var dataSize = data.Measures.SmallFontRenderer.Measure("8888");
		var leftHeaderSize = data.Measures.SmallFontRenderer.Measure(string.Concat(Enumerable.Repeat("8", data.MaxLeftHeaderSize)));
		var topHeaderSize = data.Measures.SmallFontRenderer.Measure("8");    // only height is important
		var topHeaderMargin = 3;

		// For FCM mode we need some more horizontal separation to have readable hex values. Either way we also want some separation between layers.
		var horizontalMargin = data.GlobalOptions.CharInfo.Width == 8 ? 3 : 1;
		var verticalMargin = data.Measures.FontRenderer.IsEnabled ? 3 : 1;

		var charWidth = Math.Max(scaledCharSize.Width, dataSize.Width);
		var charHeight =
			(data.IsUsingImages ? scaledCharSize.Height : 0) +   // character itself
			dataSize.Height;    // values

		var layerHorizontaMargin = 5;

		// "Normalize" section chess pattern. We only want values -1, 0 or 1.
		var sectionChessPattern = data.SectionTitlesChessPatternStart >= 0 ? data.SectionTitlesChessPatternStart % 2 : -1;
		var sectionIndex = 0;

		var builder = data.BoxMeasurer;
		var measuredItems = new List<BoxData>();

		builder.UseTitle(isElevated: data.IsUsingElevatedTitle, sectionRows: sectionChessPattern >= 0 ? 2 : 1);
		builder.SetupLeftHeader(leftHeaderSize.Width);
		builder.SetupTopHeader(topHeaderSize.Height);
		builder.SetupElementWidth(charWidth, margin: horizontalMargin);
		builder.SetupElementHeight(charHeight, margin: verticalMargin);
		builder.SetupBox(
			count: data.Width * data.Height,
			width: data.Width
		);

		builder.Measure((index, position) =>
		{
			var dataIndex = builder.CurrentIndex;
			var item = data.ItemAt(dataIndex.X, dataIndex.Y);

			// Get section name (only for top row - this is a hacky way to prevent multiple redraws of the section titles ¯\_(ツ)_/¯).
			var sectionName = dataIndex.Y == 0 ? data.SectionName(dataIndex.X, dataIndex.Y, item) : null;
			if (sectionName != null) sectionIndex++;

			// Add some offset after attributes. But also before - for this we have to update the position we got. Note how we need to adjust margin after attribute twice - we need to take into account the offset we're applying to current position.
			var adjustedPosition = position;
			if (data.SeparatorBefore(dataIndex.X, dataIndex.Y, item))
			{
				adjustedPosition.X += layerHorizontaMargin;
				builder.OffsetBox(layerHorizontaMargin, 0);
			}

			// Rectangle for item image.
			var itemImageRect = new Rectangle(
				x: adjustedPosition.X,
				y: adjustedPosition.Y,
				width: scaledCharSize.Width,
				height: scaledCharSize.Height
			);

			// Rectangle for transparent item image.
			var itemTransparentRect = new Rectangle(
				x: itemImageRect.X,
				y: itemImageRect.Y,
				width: itemImageRect.Width - 1,     // to achieve 1px gap between successive transparent boxes
				height: itemImageRect.Height - 1    // 1px gap between box and text
			);

			// Position for section name.
			var sectionNamePos = new Point(
				x: adjustedPosition.X,
				y: builder.SectionTitleLeftTop?.Y ?? 0     // we should have section left-top, but let's be proactive...
			);

			// Adjust section name position to account for chess pattern. Note how we alternate based on section index; we can't use data indices here since each section may be aribitrarily wide.
			if (sectionChessPattern >= 0 && sectionIndex % 2 == sectionChessPattern)
			{
				sectionNamePos.Y += builder.TitleHeight;
			}

			// Position for left header.
			var leftHeaderPos = new Point(
				x: builder.LeftHeader.X - 3,
				y: itemImageRect.Y
			);

			// Position for top header.
			var topHeaderPos = new Point(
				x: adjustedPosition.X,
				y: builder.TopHeader.Y - topHeaderMargin * 2
			);

			// Position for value.
			var valuesPos = new Point(
				x: itemImageRect.X,
				y: data.IsUsingImages ? itemImageRect.Bottom : itemImageRect.Top
			);

			result.Boxes.Add(new()
			{
				Item = item,
				RowSize = data.RowSize,
				Coordinate = dataIndex,
				SectionName = sectionName,
				SectionNamePos = sectionNamePos,
				TopHeaderPos = topHeaderPos,
				LeftHeaderPos = leftHeaderPos,
				CharImageBox = itemImageRect,
				CharTransparentBox = itemTransparentRect,
				DataPos = valuesPos
			});
		});

		result.TitlePos = builder.TitleLeftTop;

		MeasuredBoxes = result;
	}

	/// <summary>
	/// Draws previously measured data.
	/// </summary>
	public void Draw(DrawData data)
	{
		// If there was no prior measuring, we don't have to draw anything either.
		if (MeasuredBoxes == null) return;

		var addressOffsetX = 0;
		var addressOffsetY = 0;

		// Draw title.
		if (MeasuredBoxes.TitlePos != null && data.Title != null)
		{
			data.DrawInfo.FontRenderer.Draw(
			context: data.DrawInfo.Context,
				text: data.Title,
				color: data.DrawInfo.TextColour,
				point: MeasuredBoxes.TitlePos.Value
			);
		}

		// Draw all boxes.
		for (var i = 0; i < MeasuredBoxes.Boxes.Count; i++)
		{
			var box = MeasuredBoxes.Boxes[i];

			// Notify caller about the item we'll start handling.
			data.WillStartHandlingItem(box.Item);

			// Draw section name, unless it overlaps with title (we only check for the first section)
			if (box.SectionName != null && (i > 0 || MeasuredBoxes.IsFirstSectionTitleVisible))
			{
				data.DrawInfo.FontRenderer.Draw(
					context: data.DrawInfo.Context,
					text: box.SectionName,
					color: data.DrawInfo.TextColour,
					point: box.SectionNamePos
				);
			}

			// Draw top header, only when drawing top layer row.
			if (box.Coordinate.Y == 0)
			{
				addressOffsetX = data.AddressXOffset(box.Item, box.Coordinate.X, addressOffsetX);

				data.DrawInfo.SmallFontRenderer.Draw(
					context: data.DrawInfo.Context,
					text: addressOffsetX.ToString(),
					color: data.DrawInfo.TextColour,
					point: box.TopHeaderPos
				);

				addressOffsetX += data.GlobalOptions.CharInfo.BytesPerCharIndex;
			}

			// Draw left header, only when drawing left layer column.
			if (box.Coordinate.X == 0)
			{
				addressOffsetY = data.AddressYOffset(box.Item, box.Coordinate.Y, addressOffsetY);

				data.DrawInfo.SmallFontRenderer.Draw(
					context: data.DrawInfo.Context,
					text: addressOffsetY.ToString().PadLeft(MeasuredBoxes.MaxLeftHeaderSize, ' '),
					color: data.DrawInfo.TextColour,
					point: box.LeftHeaderPos
				);

				addressOffsetY += box.RowSize;
			}

			// For screen data we either draw the image or transparent character box. For attributes we leave this part empty as data doesn't represent an image.
			if (MeasuredBoxes.IsUsingImages && data.ShouldDrawImage(box.Item))
			{
				if (data.IsImageFullyTransparent(box.Item))
				{
					data.DrawInfo.Context.Draw(
						color: data.DrawInfo.TextFadedColour,
						thickness: 1f,
						box.CharTransparentBox
					);
				}
				else
				{
					var image = data.ItemImage(box.Item);
					if (image != null)
					{
						data.DrawInfo.Context.DrawImageAt(
							image: image,
							destination: box.CharImageBox
						);
					}
				}
			}

			// Draw text data.
			data.DrawInfo.SmallFontRenderer.Draw(
				context: data.DrawInfo.Context,
				text: data.ItemText(box.Item),
				color: data.DrawInfo.TextColour,
				point: box.DataPos
			);
		}
	}

	#region Declarations

	public class MeasureData
	{
		/// <summary>
		/// Various common options that affect rendering.
		/// </summary>
		public GlobalOptions GlobalOptions { get; set; } = null!;

		/// <summary>
		/// Measures class that handles the ground work for measuring a box.
		/// </summary>
		public BaseImageExporter.Measures Measures { get; init; } = null!;

		/// <summary>
		/// The box measurer that handled the ground work for measuring a box.
		/// </summary>
		public BaseImageExporter.Measures.BoxMeasurer BoxMeasurer { get; init; } = null!;

		/// <summary>
		/// The width of the box in characters.
		/// </summary>
		public int Width { get; init; }

		/// <summary>
		/// The height of the box in characters.
		/// </summary>
		public int Height { get; init; }

		/// <summary>
		/// The size of each row in bytes.
		/// </summary>
		public int RowSize { get; init; }

		/// <summary>
		/// Maximum number of letters in left header.
		/// </summary>
		public int MaxLeftHeaderSize { get; init; } = 4;

		/// <summary>
		/// Enables or disables section titles rendered with chess pattern as well as specifies the modulo of the section title index to elevate.
		/// 
		/// If the value is negative, then no chess pattern is used and all section titles are rendered in the same line.
		/// 
		/// If >= 0, then the value is first trimmed to 0-1 range. Then, if 0, every odd section title is rendered elevated (1st, 3rd, 5th etc), if 1, every even title is rendered elevated (2nd, 4th, 6th etc).
		/// </summary>
		public int SectionTitlesChessPatternStart { get; init; } = -1;

		/// <summary>
		/// Whether to draw title elevated on top section titles (true), or vertically aligned with section titles (false, first section title is not rendered in this case).
		/// </summary>
		public bool IsUsingElevatedTitle { get; init; } = false;

		/// <summary>
		/// Whether to use images or just text.
		/// </summary>
		public bool IsUsingImages { get; init; } = true;

		/// <summary>
		/// Provides item at the given coordinates.
		/// </summary>
		public Func<int, int, T> ItemAt { get; init; } = null!;

		/// <summary>
		/// Provides name of the section from the given item.
		/// </summary>
		public Func<int, int, T, string?> SectionName { get; init; } = null!;

		/// <summary>
		/// Function that determines whether the element at the given coordinate should use horizontal separation or not.
		/// 
		/// Default implementation never uses separation.
		/// </summary>
		public Func<int, int, T, bool> SeparatorBefore { get; init; } = (_, _, _) => false;
	}

	public class DrawData
	{
		/// <summary>
		/// Various common options that affect rendering.
		/// </summary>
		public GlobalOptions GlobalOptions { get; set; } = null!;

		/// <summary>
		/// All the drawing functionality.
		/// </summary>
		public BaseImageExporter.DrawInfo DrawInfo { get; init; } = null!;

		/// <summary>
		/// Title to use.
		/// </summary>
		public string? Title { get; init; }

		/// <summary>
		/// Informs caller the given item will start to be handled.
		/// 
		/// This is the first callback for each item. Caller can use it to prepare associated data they will be asked for with other callbacks for example.
		/// </summary>
		public Action<T> WillStartHandlingItem { get; set; } = (_) => { };

		/// <summary>
		/// Determines whether image should be drawn for the given item.
		/// 
		/// Default implementation always allows drawing.
		/// </summary>
		public Func<T, bool> ShouldDrawImage { get; set; } = (_) => true;

		/// <summary>
		/// Determines if the image the given item represents is fully transparent or not.
		/// 
		/// Default implementation always assumes opaque image.
		/// </summary>
		public Func<T, bool> IsImageFullyTransparent { get; set; } = (_) => false;

		/// <summary>
		/// Prepares address X offset for the given column.
		/// 
		/// Default implementation simply returns proposed value.
		/// </summary>
		public Func<T, int, int, int> AddressXOffset = (_, _, proposed) => proposed;

		/// <summary>
		/// Prepares address Y offset for the given row.
		/// 
		/// Default implementation simply returns proposed value.
		/// </summary>
		public Func<T, int, int, int> AddressYOffset = (_, _, proposed) => proposed;

		/// <summary>
		/// Returns the text associated with the given item.
		/// 
		/// Default implementation returns empty string.
		/// </summary>
		public Func<T, string> ItemText { get; set; } = (_) => string.Empty;

		/// <summary>
		/// Returns the image associated with the given item.
		/// 
		/// This is only called if image is allowed to be rendered for the item (so it's perfectly valid to not implement if caller is absolutely sure images will not be used).
		/// 
		/// Default implementation returns null which means no image will be drawn.
		/// </summary>
		public Func<T, Image<Argb32>?> ItemImage { get; set; } = (_) => null;
	}

	#endregion

	#region Private Declarations

	private class BoxesData
	{
		public int MaxLeftHeaderSize { get; init; }
		public bool IsUsingImages { get; init; }
		public bool IsFirstSectionTitleVisible { get; set; }
		public Point? TitlePos { get; set; }
		public List<BoxData> Boxes { get; } = new();
	}

	private class BoxData
	{
		public T Item { get; set; } = null!;
		public string? SectionName { get; set; }
		public int RowSize { get; set; }
		public Point Coordinate { get; set; }
		public Point LeftHeaderPos { get; set; }
		public Point TopHeaderPos { get; set; }
		public Point SectionNamePos { get; set; }
		public Rectangle CharImageBox { get; set; }
		public Rectangle CharTransparentBox { get; set; }
		public Point DataPos { get; set; }
	}

	#endregion
}
