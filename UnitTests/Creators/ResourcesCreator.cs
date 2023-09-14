using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;

using UnitTests.Models;

namespace UnitTests.Creators;

public static class ResourcesCreator
{
	#region Input files

	/// <summary>
	/// Creates the array of chars command inputs.
	/// </summary>
	public class InputBaseCharsCreator : BaseCreator<MemoryStreamProvider[]?>
	{
		public InputBaseCharsCreator(IBaseCreator source) : base(source)
		{
		}

		protected override MemoryStreamProvider[]? OnCreateObject()
		{
			MemoryStreamProvider[] CreateInputs() => new[] {
				new MemoryStreamProvider
				{
					Data = Resources.input_base_chars,
					Filename = "input-base-chars.png"
				}
			};

			return IsCharsRunnerEnabled switch
			{
				true => CreateInputs(),
				_ => null
			};
		}

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			// We should not call this method on input data!
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Creates the array of screens command inputs.
	/// </summary>
	public class InputScreensCreator : BaseCreator<MemoryStreamProvider[]>
	{
		public InputScreensCreator(IBaseCreator source) : base(source)
		{
		}

		protected override MemoryStreamProvider[] OnCreateObject()
		{
			MemoryStreamProvider[] CreateInputs() => new[] {
				new MemoryStreamProvider
				{
					Data = Resources.input_level,
					Filename = "input-level.aseprite"
				}
			};

			return IsScreensRunnerEnabled switch
			{
				true => CreateInputs(),
				_ => Array.Empty<MemoryStreamProvider>()
			};
		}

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			// We should not call this method on input data.
			throw new NotImplementedException();
		}
	}

	#endregion

	#region Expected output files

	/// <summary>
	/// Generates expected output data for characters.
	/// </summary>
	public class ExpectedCharsCreator : BaseCreator<MemoryStreamProvider>
	{
		protected override MemoryStreamProvider OnCreateObject()
		{
			return new MemoryStreamProvider
			{
				Data = (ColourMode, IsCharsRunnerEnabled, IsScreensRunnerEnabled, IsRRBEnabled) switch
				{
					(CharColourMode.NCM, false, true, false) => Resources.export_ncm_screens_chars,
					(CharColourMode.NCM, false, true, true) => Resources.export_ncm_screens_rrb_chars,
					(CharColourMode.NCM, true, false, false) => Resources.export_ncm_base_chars,
					(CharColourMode.NCM, true, false, true) => Resources.export_ncm_base_rrb_chars,
					(CharColourMode.NCM, true, true, false) => Resources.export_ncm_base_screens_chars,
					(CharColourMode.NCM, true, true, true) => Resources.export_ncm_base_screens_rrb_chars,

					(CharColourMode.FCM, false, true, false) => Resources.export_fcm_screens_chars,
					(CharColourMode.FCM, false, true, true) => Resources.export_fcm_screens_rrb_chars,
					(CharColourMode.FCM, true, false, false) => Resources.export_fcm_base_chars,
					(CharColourMode.FCM, true, false, true) => Resources.export_fcm_base_rrb_chars,
					(CharColourMode.FCM, true, true, false) => Resources.export_fcm_base_screens_chars,
					(CharColourMode.FCM, true, true, true) => Resources.export_fcm_base_screens_rrb_chars,

					_ => Array.Empty<byte>()
				},

				Filename = "export-chars.bin"
			};
		}

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			return data.UsedOutputStreams.CharsStream;
		}
	}

	/// <summary>
	/// Generates expected output data for palette.
	/// </summary>
	public class ExpectedPaletteCreator : BaseCreator<MemoryStreamProvider>
	{
		protected override MemoryStreamProvider OnCreateObject()
		{
			return new MemoryStreamProvider
			{
				Data = (ColourMode, IsCharsRunnerEnabled, IsScreensRunnerEnabled, IsRRBEnabled) switch
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

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			return data.UsedOutputStreams.PaletteStream;
		}
	}

	/// <summary>
	/// Generates expected output data for screen.
	/// </summary>
	public class ExpectedScreenCreator : BaseCreator<MemoryStreamProvider>
	{
		protected override MemoryStreamProvider OnCreateObject()
		{
			return new MemoryStreamProvider
			{
				Data = (ColourMode, IsCharsRunnerEnabled, IsScreensRunnerEnabled, IsRRBEnabled) switch
				{
					(CharColourMode.NCM, false, true, false) => Resources.export_ncm_screens_screen,
					(CharColourMode.NCM, false, true, true) => Resources.export_ncm_screens_rrb_screen,
					(CharColourMode.NCM, true, true, false) => Resources.export_ncm_base_screens_screen,
					(CharColourMode.NCM, true, true, true) => Resources.export_ncm_base_screens_rrb_screen,

					(CharColourMode.FCM, false, true, false) => Resources.export_fcm_screens_screen,
					(CharColourMode.FCM, false, true, true) => Resources.export_fcm_screens_rrb_screen,
					(CharColourMode.FCM, true, true, false) => Resources.export_fcm_base_screens_screen,
					(CharColourMode.FCM, true, true, true) => Resources.export_fcm_base_screens_rrb_screen,

					_ => Array.Empty<byte>()
				},

				Filename = "export-screen.bin"
			};
		}

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			// We only produce single screen data in unit tests, except when no screen output is expected.
			return IsScreensRunnerEnabled
				? data.UsedOutputStreams.ScreenDataStreams[0]
				: MemoryStreamProvider.Empty("export-screen.bin");
		}
	}

	/// <summary>
	/// Generates expected output data for colour.
	/// </summary>
	public class ExpectedColourCreator : BaseCreator<MemoryStreamProvider>
	{
		protected override MemoryStreamProvider OnCreateObject()
		{
			return new MemoryStreamProvider
			{
				Data = (ColourMode, IsCharsRunnerEnabled, IsScreensRunnerEnabled, IsRRBEnabled) switch
				{
					(CharColourMode.NCM, false, true, false) => Resources.export_ncm_screens_colour,
					(CharColourMode.NCM, false, true, true) => Resources.export_ncm_screens_rrb_colour,
					(CharColourMode.NCM, true, true, false) => Resources.export_ncm_base_screens_colour,
					(CharColourMode.NCM, true, true, true) => Resources.export_ncm_base_screens_rrb_colour,

					(CharColourMode.FCM, false, true, false) => Resources.export_fcm_screens_colour,
					(CharColourMode.FCM, false, true, true) => Resources.export_fcm_screens_rrb_colour,
					(CharColourMode.FCM, true, true, false) => Resources.export_fcm_base_screens_colour,
					(CharColourMode.FCM, true, true, true) => Resources.export_fcm_base_screens_rrb_colour,

					_ => Array.Empty<byte>()
				},

				Filename = "export-colour.bin"
			};
		}

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			// We only produce single colour data in unit tests, except when no screen output is expected.
			return IsScreensRunnerEnabled
				? data.UsedOutputStreams.ColourDataStreams[0]
				: MemoryStreamProvider.Empty("export-colour.bin");
		}
	}

	/// <summary>
	/// Generates expected output data for lookup tables.
	/// </summary>
	public class ExpectedLookupCreator : BaseCreator<MemoryStreamProvider>
	{
		protected override MemoryStreamProvider OnCreateObject()
		{
			return new MemoryStreamProvider
			{
				Data = (ColourMode, IsCharsRunnerEnabled, IsScreensRunnerEnabled, IsRRBEnabled) switch
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

		protected override IStreamProvider? OnGetActualStream(DataContainer data)
		{
			// We only produce single lookup tables data in unit tests, except when no screen output is expected.
			return IsScreensRunnerEnabled
				? data.UsedOutputStreams.LookupDataStreams[0]
				: MemoryStreamProvider.Empty("export-lookup.inf");
		}
	}

	#endregion
}
