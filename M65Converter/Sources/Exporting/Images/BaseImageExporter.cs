using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;

using SixLabors.Fonts;

using System.Reflection;

using static System.Formats.Asn1.AsnWriter;

namespace M65Converter.Sources.Exporting.Images;

/// <summary>
/// Provides common functionality for info image exporters.
/// </summary>
public abstract class BaseImageExporter
{
	/// <summary>
	/// The scale at which to draw. Should be 1 or greater.
	/// </summary>
	public int Scale { get; set; } = 1;

	private static Point ImagePadding = new(8, 8);
	private static Point TextToDataMargin = new(2, 2);
	private static Point ElementMargin = new(4, 4);
	private static Point SectionMargin = new(32, 32);

	#region Subclass

	/// <summary>
	/// Subclass should reset all of its drawing calculations.
	/// </summary>
	protected virtual void OnResetCalculations()
	{
		// Nothing to do by default.
	}

	/// <summary>
	/// Subclass should setup all of its drawing calculations needed for subsequent drawing, and return the size of the drawing area.
	/// 
	/// The given <see cref="Measures"/> class can be used to get various common measures, including font. All values are already scaled. Subclass can cache pre-calculated measures to unify subsequent drawing.
	/// 
	/// Note: subclass should also take into account image padding, however if using <see cref="Measures.BoxMeasurer"/>, this will be greatly simiplified.
	/// </summary>
	protected abstract Size OnCalculateDrawingSize(Measures measures);

	/// <summary>
	/// Subclass should draw its data.
	/// </summary>
	protected abstract void OnDraw(DrawInfo drawings);

	#endregion

	#region Drawing

	/// <summary>
	/// Draws all the data into the image saved to the given path.
	/// </summary>
	public void Draw(string path)
	{
		OnResetCalculations();

		// Ask subclass to measure the size it needs for drawing.
		var measures = new Measures(Scale);
		var imageSize = OnCalculateDrawingSize(measures);

		// Append bottom right image padding.
		imageSize.Width += measures.ImagePadding.X;
		imageSize.Height += measures.ImagePadding.Y;

		// Prepare drawing values.
		var drawings = new DrawInfo(measures);

		// Prepare image of given size and ask subclass to draw.
		using var image = new Image<Argb32>(
			width: imageSize.Width,
			height: imageSize.Height,
			backgroundColor: drawings.BackgroundColour
		);

		// Draw the image.
		image.Mutate(context =>
		{
			drawings.Context = context;

			OnDraw(drawings);
		});

		image.Save(path);
	}

	#endregion

	#region Declarations

	/// <summary>
	/// Provides access to various common measures, all scaled.
	/// </summary>
	protected class Measures
	{
		public FontRenderer FontRenderer { get; init; }
		public FontRenderer SmallFontRenderer { get; init; }
		public Point ImagePadding { get; init; }
		public Point ElementMargin { get; init; }
		public Point SectionMargin { get; init; }
		public Point TextToDataMargin { get; init; }
		public Rectangle BoundingBox { get => boundingBox; }
		public Size MeasuredSize { get => new Size(boundingBox.Width, boundingBox.Height); }

		private int Scale { get; set; }
		private Rectangle lastBox;
		private Rectangle boundingBox;

		#region Initialization & Disposal

		public Measures(int scale)
		{
			Scale = scale;
			ImagePadding = Scaled(BaseImageExporter.ImagePadding);
			ElementMargin = Scaled(BaseImageExporter.ElementMargin);
			SectionMargin = Scaled(BaseImageExporter.SectionMargin);
			TextToDataMargin = Scaled(BaseImageExporter.TextToDataMargin);
			FontRenderer = new FontRenderer(scale);
			SmallFontRenderer = new FontRenderer(scale, fontSize: 7);
		}

		#endregion

		#region Measuring

		/// <summary>
		/// Wrapper for measuring boxed data.
		/// 
		/// The two offset values define the relation between this new box and previous box:
		/// - negative: draw at image left/top border
		/// - 0: draw at previous box left/top
		/// - positive: draw at previous box right/bottom
		/// 
		/// Returns the area covered by all measured data (if <see cref="MeasureBoxedData"/> is called multiple times, the result is always the bounding box around all boxes.
		/// </summary>
		public Rectangle MeasureBoxedData(Position offsetX, Position offsetY, Action<BoxMeasurer> handler)
		{
			var position = new Point();

			switch (offsetX)
			{
				case Position.ImagePadding:
					position.X = ImagePadding.X;
					break;
				case Position.LastStart:
					position.X = lastBox.X;
					break;
				case Position.LastEnd:
					position.X = lastBox.Right + SectionMargin.X;
					break;
				case Position.MaxEnd:
					position.X = boundingBox.Right + SectionMargin.X;
					break;
			}

			switch (offsetY)
			{
				case Position.ImagePadding:
					position.Y = ImagePadding.Y;
					break;
				case Position.LastStart:
					position.Y = lastBox.Y;
					break;
				case Position.LastEnd:
					position.Y = lastBox.Bottom + SectionMargin.Y;
					break;
				case Position.MaxEnd:
					position.Y = boundingBox.Bottom + SectionMargin.Y;
					break;
			}

			var builder = new BoxMeasurer(this, position);

			handler(builder);

			lastBox = new Rectangle(
				x: builder.TopLeft.X,
				y: builder.TopLeft.Y,
				width: builder.BottomRight.X - builder.TopLeft.X,
				height: builder.BottomRight.Y - builder.TopLeft.Y
			);

			// Adjust bounding box. Only width and height need adjusting, it always starts at (0,0).
			if (lastBox.Right > boundingBox.Right) boundingBox.Width += (lastBox.Right - boundingBox.Right);
			if (lastBox.Bottom > boundingBox.Bottom) boundingBox.Height += (lastBox.Bottom - boundingBox.Bottom);

			return lastBox;
		}

		#endregion

		#region Helpers

		public Point Scaled(Point point)
		{
			return new Point(
				x: point.X * Scale,
				y: point.Y * Scale
			);
		}

		public Size Scaled(Size size)
		{
			return new Size(
				width: size.Width * Scale,
				height: size.Height * Scale
			);
		}

		#endregion

		#region Declarations

		public enum Position
		{
			ImagePadding,
			LastStart,
			LastEnd,
			MaxEnd
		}

		/// <summary>
		/// Helper class that unifies and simplifies boxed data measuring.
		/// </summary>
		public class BoxMeasurer
		{
			/// <summary>
			/// Title position, only available if title is used.
			/// </summary>
			public Point? TitleLeftTop { get; private set; }

			/// <summary>
			/// The height of the title and margin to data.
			/// </summary>
			public int TitleHeight { get; private set; }

			/// <summary>
			/// Left header position, for first item only.
			/// </summary>
			public Point LeftHeader
			{
				get => new(
					x: TopLeft.X - leftHeaderWidth, 
					y: TopLeft.Y - topHeaderHeight
				); 
			}

			/// <summary>
			/// Top-left most pixel of the box.
			/// </summary>
			public Point TopLeft { get => topLeft; }

			/// <summary>
			/// Bottom-right most pixel coordinate of the box.
			/// </summary>
			public Point BottomRight { get => bottomRight; }

			/// <summary>
			/// Current index (x, y) within the data.
			/// </summary>
			public Point CurrentIndex { get => index; }

			private Measures measures;

			private Point index = new();
			private Point topLeft = new();
			private Point bottomRight = new();
			private Size elementSize = new();
			private Point elementMargin = new();

			private int elementsCount = 0;
			private int elementsBoxWidth = 16;
			private int topHeaderHeight = 0;
			private int leftHeaderWidth = 0;
			private bool isUsingTitle = false;
			private bool isMeasured = false;

			#region Initialization & Disposal

			public BoxMeasurer(Measures measures, Point initialTopLeft)
			{
				this.measures = measures;
				topLeft = initialTopLeft;
			}

			#endregion

			#region Setup
			
			/// <summary>
			/// Enables or disables title.
			/// 
			/// IMPORTANT: to simplify handling, this MUST be called BEFORE <see cref="Measure"/> method is invoked. If called afterwards, an exception will be thrown!
			/// </summary>
			public void UseTitle(bool isUsed = true)
			{
				// If measure was already called, throw an exception.
				if (isMeasured)
				{
					throw new InvalidOperationException("Title must be set before measuring box!");
				}

				// We don't have to change anything if the same status is provided.
				if (isUsed == isUsingTitle) return;

				// Calculate title height and margin between title and content.
				var titleSize = measures.FontRenderer.Measure("Xy");
				var titleMargin = titleSize.Height + measures.ElementMargin.Y;

				// If title is not used, reset the value. We must also reduce top left accordingly.
				if (!isUsed)
				{
					TitleLeftTop = null;
					TitleHeight = 0;
					topLeft.Y -= titleMargin;
					isUsingTitle = false;
					return;
				}

				// Otherwise prepare for title area.
				TitleLeftTop = new Point(
					x: topLeft.X,
					y: topLeft.Y
				);

				// Setup title height.
				TitleHeight = titleSize.Height;

				// Also move top left downwards.
				topLeft.Y += titleMargin;

				isUsingTitle = true;
			}

			/// <summary>
			/// Enables (height > 0) or disables (height <= 0) top header of the given size.
			/// </summary>
			public void SetupTopHeader(int height)
			{
				// If measure was already called, throw an exception.
				if (isMeasured)
				{
					throw new InvalidOperationException("Top header must be set before measuring box!");
				}

				// If no change ignore.
				if (topHeaderHeight == height) return;

				if (height < 0)
				{
					// If top header not used, reduce previously set height from top-left of the box.
					topLeft.Y -= topHeaderHeight;
					topHeaderHeight = 0;
				}
				else
				{
					// Otherwise add it to top-left coordinate of the box.
					topLeft.Y += height;
					topHeaderHeight = height;
				}
			}

			/// <summary>
			/// Enables (width > 0) or disables (width <= 0) top header of the given size.
			/// </summary>
			public void SetupLeftHeader(int width)
			{
				// If measure was already called, throw an exception.
				if (isMeasured)
				{
					throw new InvalidOperationException("Left header must be set before measuring box!");
				}

				// If no change ignore.
				if (leftHeaderWidth == width) return;

				if (width < 0)
				{
					// If top header not used, reduce previously set height from top-left of the box.
					topLeft.X -= leftHeaderWidth;
					leftHeaderWidth = 0;
				}
				else
				{
					// Otherwise add it to top-left coordinate of the box.
					topLeft.X += width;
					leftHeaderWidth = width;
				}
			}

			/// <summary>
			/// Sets up individual element width and margin to next element in the row.
			/// </summary>
			public void SetupElementWidth(int width, int margin = -1)
			{
				elementSize.Width = width;
				elementMargin.X = margin >= 0 ? margin : measures.ElementMargin.X;
			}

			/// <summary>
			/// Sets up individual element height and margin to next element in the coloumn.
			/// </summary>
			public void SetupElementHeight(int height, int margin = -1)
			{
				elementSize.Height = height;
				elementMargin.Y = margin >= 0 ? margin : measures.ElementMargin.Y;
			}

			/// <summary>
			/// Sets up box - must be called before iterating!
			/// 
			/// The most important data is the number of elements that will be drawn in the box.
			/// 
			/// The other parameters specify the width (how many elements are to be drawn horizontally before breaking into new row). It's possible to provide 2 widths: desired width is what the builder will aim for. However if falldown width is also provided AND rows count using desired width would below given minimum rows, then this box width will be used.
			/// </summary>
			public void SetupBox(
				int count,
				int width = 16,
				int ifRowsLessThan = -1,
				int thenDesiredWidthShouldBe = -1
			)
			{
				elementsCount = count;

				if (ifRowsLessThan > 0 && thenDesiredWidthShouldBe > 0 && count / width < ifRowsLessThan)
				{
					elementsBoxWidth = thenDesiredWidthShouldBe;
				}
				else
				{
					elementsBoxWidth = width;
				}
			}

			#endregion

			#region Measuring

			/// <summary>
			/// Measures all elements of the box.
			/// 
			/// For each element it calls the given action providing element index and its top-left coordinate.
			/// </summary>
			public void Measure(Action<int, Point> handler)
			{
				var pos = topLeft;
				bottomRight = topLeft;
				index = Point.Empty;

				// Update flags; as soon as measuring starts we must "lock" all other offsets.
				isMeasured = true;

				for (var i = 0; i < elementsCount; i++)
				{
					// Adjust for next element.
					if (i > 0 && i % elementsBoxWidth == 0)
					{
						index.X = 0;
						index.Y++;
						
						pos.X = topLeft.X;
						pos.Y += elementSize.Height + elementMargin.Y;
						
						if (pos.Y > bottomRight.Y) bottomRight.Y = pos.Y;
					}
					else if (i > 0)
					{
						index.X++;

						pos.X += elementSize.Width + elementMargin.X;
						
						if (pos.X > bottomRight.X) bottomRight.X = pos.X;
					}

					// Ask caller to measure it.
					handler(i, pos);
				}

				// If title is used, we need to adjust top left.
				if (TitleLeftTop != null)
				{
					topLeft = TitleLeftTop.Value;
				}

				// At this point bottom right point actually points to top-left of the bottom-right most element, so we need to take into account this.
				if (elementsCount > 0)
				{
					bottomRight.X += elementSize.Width;
					bottomRight.Y += elementSize.Height;
				}
			}

			/// <summary>
			/// Moves current top-left coordinate by the given point.
			/// 
			/// Useful for applying custom margins for example.
			/// </summary>
			public void MoveBy(Point point)
			{
				topLeft.X += point.X;
				topLeft.Y += point.Y;

				bottomRight.X += point.X;
				bottomRight.Y += point.Y;
			}

			#endregion
		}

		#endregion
	}

	/// <summary>
	/// Provides various drawing related values.
	/// </summary>
	protected class DrawInfo
	{
		public Argb32 BackgroundColour { get; init; }
		public Argb32 TextColour { get; init; }
		public Argb32 TextFadedColour { get; init; }
		public Argb32 TextInverseColour { get; init; }
		public Argb32 FrameColour { get; init; }

		public FontRenderer FontRenderer { get; init; }
		public FontRenderer SmallFontRenderer { get; init; }
		public IImageProcessingContext Context { get; set; } = null!;

		#region Initialization & Disposal

		public DrawInfo(Measures measures)
		{
			FontRenderer = measures.FontRenderer;
			SmallFontRenderer = measures.SmallFontRenderer;

			BackgroundColour = Color.DarkSlateBlue;
			FrameColour = BackgroundColour.IsDark() ? Color.White : Color.DarkSlateGray;
			TextColour = FrameColour;
			TextInverseColour = BackgroundColour.IsDark() ? Color.Black : Color.White;
			TextFadedColour = TextColour.WithAlpha(100);
		}

		#endregion
	}

	#endregion
}
