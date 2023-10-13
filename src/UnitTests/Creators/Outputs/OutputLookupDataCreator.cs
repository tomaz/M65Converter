using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Providers;

using UnitTests.Models;

namespace UnitTests.Creators.Outputs;

public class OutputLookupDataCreator : BaseOutputDataCreator
{
	protected override IStreamProvider? OnGetActualStream(DataContainer.OutputStreams outputs)
	{
		// We only produce single lookup tables data in unit tests, except when no screen output is expected.
		return IsScreensRunnerEnabled
			? outputs.ScreenLookupDataStreams[0]
			: MemoryStreamProvider.Empty("export-lookup.inf");
	}

	protected override IStreamProvider? OnGetExpectedStream()
	{
		return new MemoryStreamProvider
		{
			Data = (ColourMode, IsCharsInputUsed, IsScreensInputUsed, IsRRBSpritesInputUsed, IsRRBEnabled) switch
			{
				(CharColourMode.NCM, false, true, false, false) => Resources.export_ncm_screens_lookup,
				(CharColourMode.NCM, false, true, false, true) => Resources.export_ncm_screens_rrb_lookup,
				(CharColourMode.NCM, true, true, false, false) => Resources.export_ncm_base_screens_lookup,
				(CharColourMode.NCM, true, true, false, true) => Resources.export_ncm_base_screens_rrb_lookup,

				(CharColourMode.NCM, false, true, true, false) => Resources.export_ncm_sprites_lookup,
				(CharColourMode.NCM, false, true, true, true) => Resources.export_ncm_sprites_rrb_lookup,
				(CharColourMode.NCM, true, true, true, false) => Resources.export_ncm_base_sprites_lookup,
				(CharColourMode.NCM, true, true, true, true) => Resources.export_ncm_base_sprites_rrb_lookup,

				(CharColourMode.FCM, false, true, false, false) => Resources.export_fcm_screens_lookup,
				(CharColourMode.FCM, false, true, false, true) => Resources.export_fcm_screens_rrb_lookup,
				(CharColourMode.FCM, true, true, false, false) => Resources.export_fcm_base_screens_lookup,
				(CharColourMode.FCM, true, true, false, true) => Resources.export_fcm_base_screens_rrb_lookup,

				(CharColourMode.FCM, false, true, true, false) => Resources.export_fcm_sprites_lookup,
				(CharColourMode.FCM, false, true, true, true) => Resources.export_fcm_sprites_rrb_lookup,
				(CharColourMode.FCM, true, true, true, false) => Resources.export_fcm_base_sprites_lookup,
				(CharColourMode.FCM, true, true, true, true) => Resources.export_fcm_base_sprites_rrb_lookup,

				_ => Array.Empty<byte>()
			},

			Filename = "export-lookup.inf"
		};
	}
}
