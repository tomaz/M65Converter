using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting.Utils;

/// <summary>
/// Exports a given palette to format compatible with Mega 65 hardware.
/// 
/// Note: this is a simpler helper class with sole purpose of unifying palette export accross different exporters.
/// </summary>
public class PaletteExporter
{
	/// <summary>
	/// Exports the given palette to the given writer.
	/// </summary>
	public void Export(IReadOnlyList<Argb32> palette, BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"First all {palette.Count} red values");
		Logger.Verbose.Option($"Followed by {palette.Count} green values");
		Logger.Verbose.Option($"Followed by {palette.Count} blue values");
		Logger.Verbose.Option("Each RGB component is 1 byte");

		// Note: the only reason for swapping in advance vs. on the fly is to get more meaningful verbose output.
		var swapped = ConvertToMega65Format(palette);

		// If needed, log out the palette.
		if (Logger.Verbose.IsEnabled)
		{
			var formatter = new TableFormatter
			{
				IsHex = true,
				MinValueLength = 2,
				Prefix = " ",
				Suffix = " ",
				Headers = new[] { "R", "G", "B" }
			};

			foreach (var colour in swapped)
			{
				formatter.StartNewRow();
				formatter.AppendData(colour.R);
				formatter.AppendData(colour.G);
				formatter.AppendData(colour.B);
			}

			Logger.Verbose.Separator();
			Logger.Verbose.Message("Exported palette:");
			formatter.Log(Logger.Verbose.Option);
		}

		void Export(Func<Argb32, byte> picker)
		{
			foreach (var colour in swapped)
			{
				writer.Write(picker(colour));
			}
		}

		Export((colour) => colour.R);
		Export((colour) => colour.G);
		Export((colour) => colour.B);
	}

	#region Helpers

	private IReadOnlyList<Argb32> ConvertToMega65Format(IReadOnlyList<Argb32> colours)
	{
		var result = new List<Argb32>();

		foreach (var colour in colours)
		{
			var swapped = new Argb32(
				r: colour.R.SwapNibble(),
				g: colour.G.SwapNibble(),
				b: colour.B.SwapNibble(),
				a: colour.A.SwapNibble()    // alpha is not important since we don't export it, but let's swap it anyway...
			);

			result.Add(swapped);
		}

		return result;
	}

	#endregion
}
