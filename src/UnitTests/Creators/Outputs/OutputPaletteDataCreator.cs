using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Providers;

using UnitTests.Models;

namespace UnitTests.Creators.Outputs;

public class OutputPaletteDataCreator : BaseOutputDataCreator
{
	protected override IStreamProvider? OnGetActualStream(DataContainer.OutputStreams outputs)
	{
		return outputs.PaletteStream;
	}

	protected override IStreamProvider? OnGetExpectedStream()
	{
		return new MemoryStreamProvider
		{
			Data = (ColourMode, IsCharsInputUsed, IsScreensInputUsed, IsRRBEnabled) switch
			{
				(CharColourMode.NCM, false, true, false) => Resources.export_ncm_screens_palette,
				(CharColourMode.NCM, false, true, true) => Resources.export_ncm_screens_rrb_palette,
				(CharColourMode.NCM, true, false, false) => Resources.export_ncm_base_palette,
				(CharColourMode.NCM, true, false, true) => Resources.export_ncm_base_rrb_palette,
				(CharColourMode.NCM, true, true, false) => Resources.export_ncm_base_screens_palette,
				(CharColourMode.NCM, true, true, true) => Resources.export_ncm_base_screens_rrb_palette,

				(CharColourMode.FCM, false, true, false) => Resources.export_fcm_screens_palette,
				(CharColourMode.FCM, false, true, true) => Resources.export_fcm_screens_rrb_palette,
				(CharColourMode.FCM, true, false, false) => Resources.export_fcm_base_palette,
				(CharColourMode.FCM, true, false, true) => Resources.export_fcm_base_rrb_palette,
				(CharColourMode.FCM, true, true, false) => Resources.export_fcm_base_screens_palette,
				(CharColourMode.FCM, true, true, true) => Resources.export_fcm_base_screens_rrb_palette,

				_ => Array.Empty<byte>()
			},

			Filename = "export-palette.pal"
		};
	}
}
