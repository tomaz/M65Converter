using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;

using System.Reflection;

namespace M65Converter.Sources.Helpers.Utils;

/// <summary>
/// Handles everything font related.
/// 
/// Note: font needs to be manually copied into the folder where .exe is generated!
/// </summary>
public class FontRenderer
{
	/// <summary>
	/// Specifies whether font rendering is enabled or not.
	/// </summary>
	public bool IsEnabled { get => Font != null; }

	private Font? Font { get; set; }
	private Argb32? OutlineColour { get; set; }
	private TextOptions? Options { get; set; }

	#region Initialization & Disposal

	public FontRenderer(int scale, int fontSize = 9)
	{
		// If we don't have a font, font rendering will be disabled.
		var filename = CheckFontPresence();
		if (filename == null) return;

		// Initialize the font.
		var collection = new FontCollection();
		var family = collection.Add(filename);

		Font = family.CreateFont(fontSize * scale);
		Options = new TextOptions(Font);
	}

	#endregion

	#region Drawing

	/// <summary>
	/// Measures the given text and returns the bounding box size. Returns empty size if font rendering is disabled.
	/// </summary>
	public Size Measure(string text)
	{
		if (Font == null) return Size.Empty;

		var bounds = TextMeasurer.MeasureBounds(text, Options!);

		return new Size(
			width: (int)Math.Ceiling(bounds.Width),
			height: (int)Math.Ceiling(bounds.Height)
		);
	}

	/// <summary>
	/// Draws the given string with the given colour at the given location in the image processing context.
	/// </summary>
	public void Draw(IImageProcessingContext context, string text, Color color, int x, int y)
	{
		Draw(context, text, color, new PointF(x, y));
	}

	/// <summary>
	/// Draws the given string with the given colour at the given location in the image processing context.
	/// </summary>
	public void Draw(IImageProcessingContext context, string text, Color color, Point point)
	{
		Draw(context, text, color, new PointF(point.X, point.Y));
	}

	/// <summary>
	/// Draws the given string with the given colour at the given location in the image processing context.
	/// </summary>
	public void Draw(IImageProcessingContext context, string text, Color color, PointF point)
	{
		if (Font == null) return;

		if (OutlineColour != null)
		{
			context.DrawText(
				text, 
				Font, 
				new SolidBrush(color), 
				new SolidPen(OutlineColour.Value, 0.5f), 
				point
			);
			return;
		}

		context.DrawText(text, Font, color, point);
	}

	/// <summary>
	/// Enables font outlining for all draw calls made within the given action. All outlines will be made with the given outline colour.
	/// </summary>
	public void Outline(Argb32 color, Action action)
	{
		OutlineColour = color;

		action();

		OutlineColour = null;
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Checks for font in the expected paths.
	/// </summary>
	private static string? CheckFontPresence()
	{
		// The paths (in order) where the presence of the font is checked.
		var paths = new string[]
		{
			"",	// Current directory - this way users can override font on per-project basis.
			Assembly.GetExecutingAssembly().Location,	// executable directory
		};

		// Check if we can find the font on any of above paths.
		foreach (var path in paths)
		{
			var filename = CheckFontOnPath(path);
			if (filename != null) return filename;
		}

		return null;
	}

	/// <summary>
	/// Checks if font is available on the given path. If so, the path and filename is returned. Otherwise null is returned.
	/// </summary>
	private static string? CheckFontOnPath(string path)
	{
		var folder = Path.GetDirectoryName(path) ?? "";
		var filename = Path.Combine(folder, "font.ttf");
		return !File.Exists(filename) ? null : filename;
	}

	#endregion
}
