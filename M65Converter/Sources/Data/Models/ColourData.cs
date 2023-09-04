namespace M65Converter.Sources.Data.Models;

public class ColourData
{
	public Argb32 Colour { get; set; }
	public bool IsUsed { get; set; } = true;
	public bool IsTransparent { get => Colour.A == 0; }

	public override string ToString()
	{
		var used = IsUsed ? "" : "-";
		var transparent = IsTransparent ? "T" : "";
		return $"{Colour} {used}{transparent}";
	}
}
