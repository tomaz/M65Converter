using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting.Images;

namespace M65Converter.Sources.Exporting;

/// <summary>
/// Exports info image for the given screen using the given stream (not binary stream!).
/// </summary>
public class ScreenInfoImageExporter : BaseExporter
{
	/// <summary>
	/// The screen for which to export info image.
	/// </summary>
	public ScreenExportData Screen { get; init; } = null!;

	#region Overrides

	public override void Export(IStreamProvider streamProvider)
	{
		// Note: this class is just a wrapper around char images exporter. It serves as adapter so we can use common exporter interface and not expose functionality of image exporters.
		new ScreenImageExporter
		{
			Data = Data,
			ScreenData = Screen,
		}
		.Draw(streamProvider);
	}

	#endregion
}
