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
			Data = (ColourMode, IsCharsInputUsed, IsScreensInputUsed, IsRRBEnabled) switch
			{
				(CharColourMode.NCM, false, true, false) => Resources.export_ncm_screens_lookup,
				(CharColourMode.NCM, false, true, true) => Resources.export_ncm_screens_rrb_lookup,
				(CharColourMode.NCM, true, true, false) => Resources.export_ncm_base_screens_lookup,
				(CharColourMode.NCM, true, true, true) => Resources.export_ncm_base_screens_rrb_lookup,

				(CharColourMode.FCM, false, true, false) => Resources.export_fcm_screens_lookup,
				(CharColourMode.FCM, false, true, true) => Resources.export_fcm_screens_rrb_lookup,
				(CharColourMode.FCM, true, true, false) => Resources.export_fcm_base_screens_lookup,
				(CharColourMode.FCM, true, true, true) => Resources.export_fcm_base_screens_rrb_lookup,

				_ => Array.Empty<byte>()
			},

			Filename = "export-lookup.inf"
		};
	}
}
