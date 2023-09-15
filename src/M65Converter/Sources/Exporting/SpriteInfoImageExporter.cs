using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting.Images;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports sprite info image.
/// </summary>
public class SpriteInfoImageExporter : BaseExporter
{
	/// <summary>
	/// The sprites to export.
	/// </summary>
	public IReadOnlyList<SpriteExportData> Sprites { get; init; } = null!;

	#region Overrides

	public override void Export(IStreamProvider streamProvider)
	{
		// Note: this class is just a wrapper around char images exporter. It serves as adapter so we can use common exporter interface and not expose functionality of image exporters.
		new SpriteImageExporter
		{
			Data = Data,
			Sprites = Sprites,
		}
		.Draw(streamProvider);
	}

	#endregion
}
