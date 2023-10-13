using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports a given Palette to format compatible with Mega 65 hardware.
/// </summary>
public class PaletteExporter : BaseExporter
{
	/// <summary>
	/// The Palette to export.
	/// </summary>
	public IReadOnlyList<Argb32> Palette { get; init; } = null!;

	#region Overrides

	public override void Export(BinaryWriter writer)
	{
		Logger.Verbose.Message("Format:");
		Logger.Verbose.Option($"First all {Palette.Count} red values");
		Logger.Verbose.Option($"Followed by {Palette.Count} green values");
		Logger.Verbose.Option($"Followed by {Palette.Count} blue values");
		Logger.Verbose.Option("Each RGB component is 1 byte");

		// Note: the only reason for swapping in advance vs. on the fly is to get more meaningful verbose output.
		var swapped = ConvertToMega65Format(Palette);

		// If needed, log out the Palette.
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
			Logger.Verbose.Message("Exported Palette:");
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

	#endregion

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
