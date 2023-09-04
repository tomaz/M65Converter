using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

using SixLabors.ImageSharp.Drawing.Processing;

namespace M65Converter.Sources.Exporting.Images;

/// <summary>
/// Exports info image for characters.
/// </summary>
public class CharsImageExporter : BaseImageExporter
{
	/// <summary>
	/// The layers data for export.
	/// </summary>
	public LayersData LayersData { get; set; } = null!;

	/// <summary>
	/// All characters.
	/// </summary>
	public ImagesContainer CharsContainer { get; set; } = null!;

	/// <summary>
	/// Information about characters.
	/// </summary>
	public CharInfo CharInfo { get; set; } = null!;

	/// <summary>
	/// The base address at which characters are stored on Mega 65 hardware. If < 0, this info is not printed.
	/// </summary>
	public int CharsBaseAddress { get; set; } = -1;

	private BoxData<CharPositionsType> CharPositions { get; set; } = null!;
	private BoxData<ColourPositionsType> ColourPositions { get; set; } = null!;
	private BoxData<LayerPositionsType> ScreenLayerPositions { get; set; } = null!;
	private BoxData<LayerPositionsType> ColourLayerPositions { get; set; } = null!;

	private static Size ColourBoxSize = new(16, 16);

	#region Overrides

	protected override void OnResetCalculations()
	{
		CharPositions = new();
		ColourPositions = new();
		ScreenLayerPositions = new();
		ColourLayerPositions = new();
	}

	protected override Size OnCalculateDrawingSize(Measures measures)
	{
		MeasureChars(measures);
		MeasurePalette(measures);
		MeasureLayers(measures, LayersData.Screen, ScreenLayerPositions);
		MeasureLayers(measures, LayersData.Colour, ColourLayerPositions, isUsingImages: false);

		return measures.MeasuredSize;
	}

	protected override void OnDraw(DrawInfo drawings)
	{
		DrawPalette(drawings);
		DrawChars(drawings);
		DrawLayer(drawings, "SCREEN", LayersData.Screen, ScreenLayerPositions);
		DrawLayer(drawings, "COLOUR", LayersData.Colour, ColourLayerPositions, isUsingImages: false);
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
				var lastCharIndex = CharsContainer.Images.Count - 1;
				var indexTextSize = measures.FontRenderer.Measure(lastCharIndex.ToMeasureString());
				var addressTextSize = CharsBaseAddress >= 0
					? measures.FontRenderer.Measure(CharRamAddressString(lastCharIndex))
					: new Size();

				// Prepare other sizes.
				var scaledCharSize = measures.Scaled(new Size(CharInfo.Width, CharInfo.Height));
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
				var minColourWidth = Math.Min(indexTextSize.Width, scaledColourBoxSize.Width);
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
					count: LayersData.Palette.Count,
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
		LayersData.Layer layer, 
		BoxData<LayerPositionsType> positions,
		bool isUsingImages = true)
	{
		measures.MeasureBoxedData(
			offsetX: Measures.Position.ImagePadding,
			offsetY: Measures.Position.MaxEnd,
			handler: (builder) =>
			{
				var rowSize = layer.Rows.Count > 0 ? layer.Rows[0].Size : 0;

				var scaledCharSize = measures.Scaled(new Size(CharInfo.Width, CharInfo.Height));
				var dataSize = measures.SmallFontRenderer.Measure("8888");
				var leftHeaderSize = measures.SmallFontRenderer.Measure("8888");
				var topHeaderSize = measures.SmallFontRenderer.Measure("8");	// only height is important
				var topHeaderMargin = 3;

				var charWidth = Math.Max(scaledCharSize.Width, dataSize.Width);
				var charHeight =
					(isUsingImages ? scaledCharSize.Height : 0) +	// character itself
					dataSize.Height +	// values
					2;					// some margin

				// For FCM mode we need some more horizontal separation to have readable hex values. Either way we also want some separation between layers.
				var horizontalMargin = CharInfo.Width == 8 ? 3 : 1;
				var layerHorizontaMargin = 5;

				builder.UseTitle();
				builder.SetupLeftHeader(leftHeaderSize.Width);
				builder.SetupTopHeader(topHeaderSize.Height);
				builder.SetupElementWidth(charWidth, margin: horizontalMargin);
				builder.SetupElementHeight(charHeight, margin: 1);
				
				builder.SetupBox(
					count: layer.Count,
					width: layer.Width
				);

				builder.Measure((index, position) =>
				{
					var coordinate = builder.CurrentIndex;
					var data = layer[coordinate.X, coordinate.Y];

					// Add some offset after attributes. But also before - for this we have to update the position we got. Note how we need to adjust margin after attribute twice - we need to take into account the offset we're applying to current position.
					var adjustedPosition = position;
					if (data.Type == LayersData.Column.DataType.Attribute)
					{
						adjustedPosition.X += layerHorizontaMargin;
						builder.OffsetBox(layerHorizontaMargin * 2, 0);
					}

					// Rectangle for char image.
					var charImageRect = new Rectangle(
						x: adjustedPosition.X,
						y: adjustedPosition.Y,
						width: scaledCharSize.Width,
						height: scaledCharSize.Height
					);

					// Rectangle for transparent char image.
					var charTransparentRect = new Rectangle(
						x: charImageRect.X,
						y: charImageRect.Y,
						width: charImageRect.Width - 1,		// to achieve 1px gap between successive transparent boxes
						height: charImageRect.Height - 1	// 1px gap between box and text
					);

					// Position for layer name.
					var layerNamePos = new Point(
						x: adjustedPosition.X,
						y: builder.TitleLeftTop?.Y ?? 0		// we should have title-left-top, but let's be proactive...
					);

					// Position for left header.
					var leftHeaderPos = new Point(
						x: builder.LeftHeader.X - 3,
						y: charImageRect.Y
					);

					// Position for top header.
					var topHeaderPos = new Point(
						x: adjustedPosition.X,
						y: layerNamePos.Y + builder.TitleHeight + topHeaderMargin
					);

					// Position for value.
					var valuesPos = new Point(
						x: charImageRect.X,
						y: isUsingImages ? charImageRect.Bottom : charImageRect.Top
					);

					positions.Boxes.Add(new()
					{
						CharData = data,
						RowSize = rowSize,
						Coordinate = coordinate,
						LayerName = data.Tag,
						LayerNamePos = layerNamePos,
						TopHeaderPos = topHeaderPos,
						LeftHeaderPos = leftHeaderPos,
						CharImageBox = charImageRect,
						CharTransparentBox = charTransparentRect,
						DataPos = valuesPos
					});
				});

				positions.TitlePos = builder.TitleLeftTop;
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

		for (var i = 0; i < CharsContainer.Images.Count; i++)
		{
			var image = CharsContainer.Images[i];
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

		for (var i = 0; i < LayersData.Palette.Count; i++)
		{
			var colour = LayersData.Palette[i];
			var positions = ColourPositions.Boxes[i];

			var argb = colour.Colour;
			var appliedColour = argb.A == 0 ? new Argb32(argb.R, argb.G, argb.B, 255) : argb;

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
				text: $"{appliedColour.R:X2}{appliedColour.G:X2}{appliedColour.B:X2}",
				color: info.TextFadedColour,
				point: positions.RGBTextPos
			);

			// Colour itself.
			info.Context.Fill(
				color: appliedColour,
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
				DrawMarker("T", positions.ColourBox, appliedColour);
			}
			else if (!colour.IsUsed)
			{
				DrawMarker("X", positions.ColourBox, appliedColour);
			}
		}
	}

	private void DrawLayer(
		DrawInfo info,
		string title,
		LayersData.Layer layer,
		BoxData<LayerPositionsType> positions,
		bool isUsingImages = true)
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

		if (positions.TitlePos != null)
		{
			info.FontRenderer.Draw(
				context: info.Context,
				text: $"{title} {layer.Name}",
				color: info.TextColour,
				point: positions.TitlePos.Value
			);
		}

		var addressOffsetX = 0;
		var addressOffsetY = 0;

		for (var i = 0; i < positions.Boxes.Count; i++)
		{
			var position = positions.Boxes[i];
			var charData = position.CharData;

			var charImage = CharsContainer.Images[charData.Data1];

			// Draw layer name. Don't do it for first layer since we already draw it as title above.
			if (position.LayerName != null && i > 0)
			{
				info.FontRenderer.Draw(
					context: info.Context,
					text: position.LayerName,
					color: info.TextColour,
					point: position.LayerNamePos
				);
			}

			// Draw top header, only when drawing top layer row.
			if (position.Coordinate.Y == 0)
			{
				info.SmallFontRenderer.Draw(
					context: info.Context,
					text: addressOffsetX.ToString(),
					color: info.TextColour,
					point: position.TopHeaderPos
				);

				addressOffsetX += CharInfo.PixelDataSize;
			}

			// Draw left header, only when drawing left layer column.
			if (position.Coordinate.X == 0)
			{
				info.SmallFontRenderer.Draw(
					context: info.Context,
					text: addressOffsetY.ToString().PadLeft(4, ' '),
					color: info.TextColour,
					point: position.LeftHeaderPos
				);

				addressOffsetY += position.RowSize;
			}

			// For screen data we either draw the image or transparent character box. For attributes we leave this part empty as data doesn't represent an image.
			if (isUsingImages && charData.Type != LayersData.Column.DataType.Attribute)
			{
				if (charImage.IsFullyTransparent)
				{
					info.Context.Draw(
						color: info.TextFadedColour,
						thickness: 1f,
						position.CharTransparentBox
					);
				}
				else
				{
					info.Context.DrawImageAt(
						image: charImage.Image,
						destination: position.CharImageBox
					);
				}
			}

			// Draw little-endian data.
			info.SmallFontRenderer.Draw(
				context: info.Context,
				text: charData.LittleEndianData.ToString("X4"),
				color: info.TextColour,
				point: position.DataPos
			);
		}
	}

	#endregion

	#region Helpers

	private string CharRamAddressString(int index)
	{
		return $"${CharInfo.CharIndexInRam(CharsBaseAddress, index):X}";
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

	private class LayerPositionsType
	{
		public LayersData.Column CharData { get; set; } = null!;
		public string? LayerName { get; set; }
		public int RowSize { get; set; }
		public Point Coordinate { get; set; }
		public Point LeftHeaderPos { get; set; }
		public Point TopHeaderPos { get; set; }
		public Point LayerNamePos { get; set; }
		public Rectangle CharImageBox { get; set; }
		public Rectangle CharTransparentBox { get; set; }
		public Point DataPos { get; set; }
	}

	#endregion
}
