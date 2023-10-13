namespace M65Converter.Sources.Data.Intermediate.Images;

/// <summary>
/// A generic image group container.
/// </summary>
public class ImageGroupData
{
	/// <summary>
	/// Group description. This is mainly used for logging purposes.
	/// </summary>
	public string? Description { get; init; }

	/// <summary>
	/// The list of all images of this group.
	/// </summary>
	public IReadOnlyList<ImageData> Images { get; init; } = null!;

	#region Overrides

	public override string ToString() => $"Image group {Description}, {Images.Count} images";

	#endregion
}
