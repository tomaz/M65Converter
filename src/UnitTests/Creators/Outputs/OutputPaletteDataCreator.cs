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
			Data = (ColourMode, IsCharsInputUsed, IsScreensInputUsed, IsRRBSpritesInputUsed, IsRRBEnabled) switch
			{
				(CharColourMode.NCM, false, true, false, false) => Resources.export_ncm_screens_palette,
				(CharColourMode.NCM, false, true, false, true) => Resources.export_ncm_screens_rrb_palette,
				(CharColourMode.NCM, true, false, false, false) => Resources.export_ncm_base_palette,
				(CharColourMode.NCM, true, false, false, true) => Resources.export_ncm_base_rrb_palette,
				(CharColourMode.NCM, true, true, false, false) => Resources.export_ncm_base_screens_palette,
				(CharColourMode.NCM, true, true, false, true) => Resources.export_ncm_base_screens_rrb_palette,

				(CharColourMode.NCM, false, true, true, false) => Resources.export_ncm_sprites_palette,
				(CharColourMode.NCM, false, true, true, true) => Resources.export_ncm_sprites_rrb_palette,
				(CharColourMode.NCM, true, true, true, false) => Resources.export_ncm_base_sprites_palette,
				(CharColourMode.NCM, true, true, true, true) => Resources.export_ncm_base_sprites_rrb_palette,

				(CharColourMode.FCM, false, true, false, false) => Resources.export_fcm_screens_palette,
				(CharColourMode.FCM, false, true, false, true) => Resources.export_fcm_screens_rrb_palette,
				(CharColourMode.FCM, true, false, false, false) => Resources.export_fcm_base_palette,
				(CharColourMode.FCM, true, false, false, true) => Resources.export_fcm_base_rrb_palette,
				(CharColourMode.FCM, true, true, false, false) => Resources.export_fcm_base_screens_palette,
				(CharColourMode.FCM, true, true, false, true) => Resources.export_fcm_base_screens_rrb_palette,

				(CharColourMode.FCM, false, true, true, false) => Resources.export_fcm_sprites_palette,
				(CharColourMode.FCM, false, true, true, true) => Resources.export_fcm_sprites_rrb_palette,
				(CharColourMode.FCM, true, true, true, false) => Resources.export_fcm_base_sprites_palette,
				(CharColourMode.FCM, true, true, true, true) => Resources.export_fcm_base_sprites_rrb_palette,

				_ => Array.Empty<byte>()
			},

			Filename = "export-palette.pal"
		};
	}
}
