namespace M65Converter.Sources.Data.Intermediate.Images;

/// <summary>
/// Wrapper around a colour that provides additional information besides just ARGB.
/// </summary>
public class ColourData
{
	/// <summary>
	/// The colour itself.
	/// </summary>
	public Argb32 Colour { get; set; }

	/// <summary>
	/// Specifies whether this colour is used or not.
	/// 
	/// Unused colours are colours added to palette while exporting to fill in empty slots.
	/// </summary>
	public bool IsUsed { get; set; } = true;

	/// <summary>
	/// Specifies whether the colour is fully transparent or not.
	/// </summary>
	public bool IsTransparent { get => Colour.A == 0; }

	#region Overrides

	public override string ToString()
	{
		var used = IsUsed ? "" : "X";
		var transparent = IsTransparent ? "T" : "";
		return $"{Colour} {used}{transparent}";
	}

	#endregion
}
