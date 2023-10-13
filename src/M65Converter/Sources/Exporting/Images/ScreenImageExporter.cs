using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Exporting.Images.Helpers;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

using SixLabors.ImageSharp.Drawing.Processing;

using System.ComponentModel;

using static M65Converter.Sources.Data.Intermediate.Containers.SpriteExportData;

namespace M65Converter.Sources.Exporting.Images;

/// <summary>
/// Exports info image for a specific screen.
/// </summary>
public class ScreenImageExporter : BaseImageExporter
{
	/// <summary>
	/// The screen data we should export.
	/// </summary>
	public ScreenExportData ScreenData { get; init; } = null!;

	private BoxData<CharPositionsType> CharPositions { get; set; } = null!;
	private BoxData<ColourPositionsType> ColourPositions { get; set; } = null!;
	private ImageBoxHandler<ScreenExportData.Column> ScreenLayerHandler { get; set; } = null!;
	private ImageBoxHandler<ScreenExportData.Column> ColourLayerHandler { get; set; } = null!;

	private static Size ColourBoxSize = new(16, 16);

	#region Overrides

	protected override void OnResetCalculations()
	{
		CharPositions = new();
		ColourPositions = new();
		ScreenLayerHandler = new();
		ColourLayerHandler = new();
	}

	protected override Size OnCalculateDrawingSize(Measures measures)
	{
		MeasureChars(measures);
		MeasurePalette(measures);
		MeasureLayers(measures, ScreenData.Screen, ScreenLayerHandler);
		MeasureLayers(
			measures: measures, 
			layer: ScreenData.Colour, 
			handler: ColourLayerHandler, 
			isUsingImages: false, 
			onlyIfFontIsAvailable: true // colours are only composed of text, so there's no point in exporting if fonts are not available
		);

		return measures.MeasuredSize;
	}

	protected override void OnDraw(DrawInfo drawings)
	{
		DrawPalette(drawings);
		DrawChars(drawings);
		DrawLayer(drawings, "SCREEN", ScreenData.Screen, ScreenLayerHandler);
		DrawLayer(drawings, "COLOUR", ScreenData.Colour, ColourLayerHandler);
	}

	#endregion

	#region Measuring

	private void MeasureChars(Measures measures)
	{
		measures.MeasureBoxedData(
			offsetX: Measures.Position.ImagePadding,
			offsetY: Measures.Position.ImagePadding,
			builder =>
			{
				// Prepare text sizes. Note: if we have 0 characters this will yield invalid values, but the result of the action will still be empty rectangle because builder enumeration will not happen.
				var lastCharIndex = Data.CharsContainer.Images.Count - 1;
				var indexTextSize = measures.FontRenderer.Measure(lastCharIndex.ToMeasureString());
				var addressTextSize = Data.GlobalOptions.CharsBaseAddress >= 0
					? measures.FontRenderer.Measure(CharRamAddressString(lastCharIndex))
					: new Size();

				// Prepare other sizes.
				var scaledCharSize = measures.Scaled(Data.GlobalOptions.CharInfo.Size);
				var charBoxSize = new Size(scaledCharSize.Width + 2, scaledCharSize.Height + 2);    // 1px frame around char

				// Prepare image item size.
				var imageItemWidth =
					charBoxSize.Width +             // image width
					addressTextSize.Width +         // address text size (will be 0 if font not available)
					measures.TextToDataMargin.X;    // image to address text margin (will be 0 if font not available)
				var imageItemHeight =
					charBoxSize.Height +            // image height
					indexTextSize.Height +          // index text height (will be 0 if font not available)
					measures.TextToDataMargin.Y;    // text to image margin (will be 0 if font not available)

				// Setup box measurer.
				builder.UseTitle();
				builder.SetupElementWidth(imageItemWidth);
				builder.SetupElementHeight(imageItemHeight);
				builder.SetupBox(
					count: lastCharIndex + 1,
					width: 16,
					ifRowsLessThan: 10,
					thenDesiredWidthShouldBe: 8
				);

				// Measure all elements.
				builder.Measure((index, position) =>
				{
					// Image box is rendered below index text and has fixed size - just enough to wrap the character
					var imageBox = new Rectangle(
						x: position.X,
						y: position.Y + indexTextSize.Height + measures.TextToDataMargin.Y,
						width: charBoxSize.Width,
						height: charBoxSize.Height
					);

					// Image is rendered inside image box with 1px frame offset.
					var image = new Rectangle(
						x: imageBox.X + 1,
						y: imageBox.Y + 1,
						width: imageBox.Width - 2,
						height: imageBox.Height - 2
					);

					// Index text is rendered horizontally centered on the box and at the top.
					var indexTextPos = new Point(
						x: position.X + (charBoxSize.Width - indexTextSize.Width) / 2,
						y: position.Y
					);

					// Address index text is rendered to the right of the colour box and vertically centered to it.
					var addressTextPos = new Point(
						x: imageBox.Right + measures.TextToDataMargin.X,
						y: imageBox.Top + (imageBox.Height - addressTextSize.Height) / 2
					);

					// Add measures to the array.
					CharPositions.Boxes.Add(new()
					{
						ImageBox = imageBox,
						Image = image,
						IndexTextPos = indexTextPos,
						AddressTextPos = addressTextPos
					});
				});

				CharPositions.TitlePos = builder.TitleLeftTop;
			}
		);
	}

	private void MeasurePalette(Measures measures)
	{
		measures.MeasureBoxedData(
			offsetX: Measures.Position.LastEnd,
			offsetY: Measures.Position.LastStart,
			builder =>
			{
				// Prepare text sizes.
				var indexTextSize = measures.FontRenderer.Measure("888");
				var rgbTextSize = measures.FontRenderer.Measure("888888");

				// Prepare other sizes.
				var scaledColourBoxSize = measures.Scaled(ColourBoxSize);
				var minColourWidth = Math.Max(indexTextSize.Width, scaledColourBoxSize.Width);
				var minColourHeight = Math.Max(rgbTextSize.Height, scaledColourBoxSize.Height);

				// Prepare colour item size.
				var colourWidth =
					minColourWidth +                // colour box width
					rgbTextSize.Width +             // RGB text width (0 if text is not rendered)
					measures.TextToDataMargin.X;    // margin between box and text (0 if text is not rendered)
				var colourHeight =
					minColourHeight +               // colour box height
					indexTextSize.Height +          // index text on top (0 if text is not rendered)
					measures.TextToDataMargin.Y;    // margin between text and box (0 if text is not rendered)

				// Setup element sizes so builder will advance correctly. We use standard element margins for both.
				builder.UseTitle();
				builder.SetupElementWidth(colourWidth);
				builder.SetupElementHeight(colourHeight);

				// Colour palette is 16x16 max, so we show 16 colours horizontally up to 16 vertically. However if we have less than 10 lines of colours, we instead use 8 colour per line to avoid horizontally too long image.
				builder.SetupBox(
					count: Data.Palette.Count,
					width: 16,
					ifRowsLessThan: 10,
					thenDesiredWidthShouldBe: 8
				);

				// Measure all data for palette box.
				builder.Measure((index, position) =>
				{
					// Colour box is rendered below index text.
					var colourBox = new Rectangle(
						x: position.X,
						y: position.Y + indexTextSize.Height + measures.TextToDataMargin.Y,
						width: minColourWidth,
						height: minColourHeight
					);

					// Index text is rendered horizontally centered on the box and at the top.
					var indexTextPos = new Point(
						x: position.X + (minColourWidth - indexTextSize.Width) / 2,
						y: position.Y
					);

					// RGB text is rendered to the right of the colour box and vertically centered to it.
					var rgbTextPos = new Point(
						x: colourBox.Right + measures.TextToDataMargin.X,
						y: colourBox.Top + (minColourHeight - rgbTextSize.Height) / 2
					);

					// Add measures to the array.
					ColourPositions.Boxes.Add(new()
					{
						ColourBox = colourBox,
						IndexTextPos = indexTextPos,
						RGBTextPos = rgbTextPos
					});
				});

				ColourPositions.TitlePos = builder.TitleLeftTop;
			}
		);
	}

	private void MeasureLayers(
		Measures measures, 
		ScreenExportData.Layer layer, 
		ImageBoxHandler<ScreenExportData.Column> handler,
		bool isUsingImages = true,
		bool onlyIfFontIsAvailable = false)
	{
		// If we are only outputting text, there's no point in generating anything if font is not available.
		if (onlyIfFontIsAvailable && !measures.FontRenderer.IsEnabled) return;

		measures.MeasureBoxedData(
			offsetX: Measures.Position.ImagePadding,
			offsetY: Measures.Position.MaxEnd,
			handler: (builder) =>
			{
				handler.Measure(new ImageBoxHandler<ScreenExportData.Column>.MeasureData
				{
					GlobalOptions = Data.GlobalOptions,
					Measures = measures,
					BoxMeasurer = builder,

					Width = layer.Width,
					Height = layer.Height,
					RowSize = layer.Rows.Count > 0 ? layer.Rows[0].Size : 0,

					SectionTitlesChessPatternStart = 0,		// layer names with indices 1, 3, 5 etc should be rendered elevated to avoid overlap
					IsUsingImages = isUsingImages,
					
					ItemAt = (x, y) => layer[x, y],
					SectionName = (x, y, item) => item.LayerName,
					SeparatorBefore = (x, y, item) => item.Type == ScreenExportData.Column.DataType.Attribute
				});
			}
		);
	}

	#endregion

	#region Drawing

	private void DrawChars(DrawInfo info)
	{
		if (CharPositions.TitlePos != null)
		{
			info.FontRenderer.Draw(
				context: info.Context,
				text: "CHARACTERS",
				color: info.TextColour,
				point: CharPositions.TitlePos.Value
			);
		}

		for (var i = 0; i < Data.CharsContainer.Images.Count; i++)
		{
			var image = Data.CharsContainer.Images[i];
			var positions = CharPositions.Boxes[i];

			// Image index text.
			info.FontRenderer.Draw(
				context: info.Context,
				text: i.ToString(),
				color: info.TextColour,
				point: positions.IndexTextPos
			);

			// Address index text.
			info.FontRenderer.Draw(
				context: info.Context,
				text: CharRamAddressString(i),
				color: info.TextFadedColour,
				point: positions.AddressTextPos
			);

			// Image.
			info.Context.DrawImageAt(
				image: image.Image,
				destination: positions.Image
			);

			// Frame around image (only for fully transparent images).
			if (image.IsFullyTransparent)
			{
				info.Context.Draw(
					color: info.FrameColour,
					thickness: 1,
					shape: positions.ImageBox
				);
			}
		}
	}

	private void DrawPalette(DrawInfo info)
	{
		if (ColourPositions.TitlePos != null)
		{
			info.FontRenderer.Draw(
				context: info.Context,
				text: "PALETTE",
				color: info.TextColour,
				point: ColourPositions.TitlePos.Value
			);
		}

		var markerSize = info.SmallFontRenderer.Measure("M");

		void DrawMarker(string marker, Rectangle colourBox, Argb32 colour)
		{
			var textColour = colour.IsDark() ? Color.White : Color.Black;

			info.SmallFontRenderer.Draw(
				context: info.Context,
				text: marker,
				color: textColour,
				x: colourBox.X + (colourBox.Width - markerSize.Width) / 2,
				y: colourBox.Y + (colourBox.Height - markerSize.Height) / 2
			);
		}

		for (var i = 0; i < Data.Palette.Count; i++)
		{
			var colour = Data.Palette[i];
			var positions = ColourPositions.Boxes[i];

			var argb = colour.Colour;

			// Colour index.
			info.FontRenderer.Draw(
				context: info.Context,
				text: $"{i}",
				color: info.TextColour,
				point: positions.IndexTextPos
			);

			// Colour RGB.
			info.FontRenderer.Draw(
				context: info.Context,
				text: $"{argb.R:X2}{argb.G:X2}{argb.B:X2}",
				color: info.TextFadedColour,
				point: positions.RGBTextPos
			);

			// Colour itself.
			info.Context.Fill(
				color: argb,
				shape: positions.ColourBox
			);

			// Frame around colour.
			info.Context.Draw(
				color: info.FrameColour,
				thickness: 1,
				shape: positions.ColourBox
			);

			// Colour marker (if needed).
			if (colour.IsTransparent)
			{
				DrawMarker("T", positions.ColourBox, argb);
			}
			else if (!colour.IsUsed)
			{
				DrawMarker("X", positions.ColourBox, argb);
			}
		}
	}

	private void DrawLayer(
		DrawInfo info,
		string title,
		ScreenExportData.Layer layer,
		ImageBoxHandler<ScreenExportData.Column> handler)
	{
		// Remember:
		// screen values:
		//		- Type == FirstData:
		//			- Tag = layer name
		//			- Data1 = char index (0...)
		//			- Data2 = char "index in ram" ($800, $801 or similar).
		//		- Type == Data:
		//			- Data1 = char index (0...)
		//			- Data2 = char "index in ram" ($800, $801 or similar).
		// colour values:
		//		- Type == FirstData:
		//			- Tag = layer name
		//			- Data1 = colour bank (only for NCM mode, 0 for FCM)
		//		- Type == Data:
		//			- Data1 = colour bank (only for NCM mode, 0 for FCM)

		ImageData? currentItemImage = null;

		handler.Draw(new ImageBoxHandler<ScreenExportData.Column>.DrawData
		{
			GlobalOptions = Data.GlobalOptions,
			DrawInfo = info,
			Title = $"{title} {layer.Name}",

			WillStartHandlingItem = (item) => currentItemImage = Data.CharsContainer.Images[item.CharIndex],
			ShouldDrawImage = (item) => item.Type != ScreenExportData.Column.DataType.Attribute,
			IsImageFullyTransparent = (item) => currentItemImage?.IsFullyTransparent ?? false,
			ItemImage = (item) => currentItemImage?.Image,
			ItemText = (item) => item.LittleEndianData.ToString("X4"),
		});
	}

	#endregion

	#region Helpers

	private string CharRamAddressString(int index)
	{
		return $"${Data.CharIndexInRam(index):X}";
	}

	#endregion

	#region Declarations

	private class BoxData<T>
	{
		public Point? TitlePos { get; set; }
		public List<T> Boxes { get; } = new();
	}

	private class CharPositionsType
	{
		public Rectangle ImageBox { get; set; }
		public Rectangle Image { get; set; }
		public Point IndexTextPos { get; set; }
		public Point AddressTextPos { get; set; }
	}

	private class ColourPositionsType
	{
		public Rectangle ColourBox { get; set; }
		public Point IndexTextPos { get; set; }
		public Point RGBTextPos { get; set; }
	}

	#endregion
}
