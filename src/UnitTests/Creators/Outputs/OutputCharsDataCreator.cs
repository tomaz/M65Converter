using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Providers;

using UnitTests.Models;

namespace UnitTests.Creators.Outputs;

public class OutputCharsDataCreator : BaseOutputDataCreator
{
	protected override IStreamProvider? OnGetActualStream(DataContainer.OutputStreams outputs)
	{
		return outputs.CharsStream;
	}

	protected override IStreamProvider? OnGetExpectedStream()
	{
		return new MemoryStreamProvider
		{
			Data = (ColourMode, IsCharsInputUsed, IsScreensInputUsed, IsRRBSpritesInputUsed, IsRRBEnabled) switch
			{
				(CharColourMode.NCM, false, true, false, false) => Resources.export_ncm_screens_chars,
				(CharColourMode.NCM, false, true, false, true) => Resources.export_ncm_screens_rrb_chars,
				(CharColourMode.NCM, true, false, false, false) => Resources.export_ncm_base_chars,
				(CharColourMode.NCM, true, false, false, true) => Resources.export_ncm_base_rrb_chars,
				(CharColourMode.NCM, true, true, false, false) => Resources.export_ncm_base_screens_chars,
				(CharColourMode.NCM, true, true, false, true) => Resources.export_ncm_base_screens_rrb_chars,

				(CharColourMode.NCM, false, true, true, false) => Resources.export_ncm_sprites_chars,
				(CharColourMode.NCM, false, true, true, true) => Resources.export_ncm_sprites_rrb_chars,
				(CharColourMode.NCM, true, true, true, false) => Resources.export_ncm_base_sprites_chars,
				(CharColourMode.NCM, true, true, true, true) => Resources.export_ncm_base_sprites_rrb_chars,

				(CharColourMode.FCM, false, true, false, false) => Resources.export_fcm_screens_chars,
				(CharColourMode.FCM, false, true, false, true) => Resources.export_fcm_screens_rrb_chars,
				(CharColourMode.FCM, true, false, false, false) => Resources.export_fcm_base_chars,
				(CharColourMode.FCM, true, false, false, true) => Resources.export_fcm_base_rrb_chars,
				(CharColourMode.FCM, true, true, false, false) => Resources.export_fcm_base_screens_chars,
				(CharColourMode.FCM, true, true, false, true) => Resources.export_fcm_base_screens_rrb_chars,

				(CharColourMode.FCM, false, true, true, false) => Resources.export_fcm_sprites_chars,
				(CharColourMode.FCM, false, true, true, true) => Resources.export_fcm_sprites_rrb_chars,
				(CharColourMode.FCM, true, true, true, false) => Resources.export_fcm_base_sprites_chars,
				(CharColourMode.FCM, true, true, true, true) => Resources.export_fcm_base_sprites_rrb_chars,

				_ => Array.Empty<byte>()
			},

			Filename = "export-chars.bin"
		};
	}
}
