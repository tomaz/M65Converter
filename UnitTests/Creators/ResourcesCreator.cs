using M65Converter.Sources.Data.Intermediate;

using UnitTests.Models;

namespace UnitTests.Creators;

public static class ResourcesCreator
{
	public static MemoryStreamProvider CharsInput()
	{
		return new MemoryStreamProvider
		{
			Data = Resources.input_level,
			Filename = "input-chars.aseprite"
		};
	}

	#region Declarations

	public class CharsCreator : BaseCreator
	{
		protected override MemoryStreamProvider CreateStreamProvider()
		{
			return new MemoryStreamProvider
			{
				Data = CharType switch
				{
					CharColourMode.NCM => 
						IsRRBEnabled 
							? Resources.export_ncm_rrb_chars 
							: Resources.export_ncm_chars,

					_ =>
						IsRRBEnabled
							? Resources.export_fcm_rrb_chars
							: Resources.export_fcm_chars,
				},

				Filename = "export-chars.bin"
			};
		}
	}

	public class PaletteCreator : BaseCreator
	{
		protected override MemoryStreamProvider CreateStreamProvider()
		{
			return new MemoryStreamProvider
			{
				Data = CharType switch
				{
					CharColourMode.NCM =>
						IsRRBEnabled
							? Resources.export_ncm_rrb_palette
							: Resources.export_ncm_palette,

					_ =>
						IsRRBEnabled
							? Resources.export_fcm_rrb_palette
							: Resources.export_fcm_palette,
				},

				Filename = "export-palette.pal"
			};
		}
	}

	public class ScreenCreator : BaseCreator
	{
		protected override MemoryStreamProvider CreateStreamProvider()
		{
			return new MemoryStreamProvider
			{
				Data = CharType switch
				{
					CharColourMode.NCM =>
						IsRRBEnabled
							? Resources.export_ncm_rrb_screen
							: Resources.export_ncm_screen,

					_ =>
						IsRRBEnabled
							? Resources.export_fcm_rrb_screen
							: Resources.export_fcm_screen,
				},

				Filename = "export-screen.bin"
			};
		}
	}

	public class ColourCreator : BaseCreator
	{
		protected override MemoryStreamProvider CreateStreamProvider()
		{
			return new MemoryStreamProvider
			{
				Data = CharType switch
				{
					CharColourMode.NCM =>
						IsRRBEnabled
							? Resources.export_ncm_rrb_colour
							: Resources.export_ncm_colour,

					_ =>
						IsRRBEnabled
							? Resources.export_fcm_rrb_colour
							: Resources.export_fcm_colour,
				},

				Filename = "export-screen.bin"
			};
		}
	}

	public class LookupCreator : BaseCreator
	{
		protected override MemoryStreamProvider CreateStreamProvider()
		{
			return new MemoryStreamProvider
			{
				Data = CharType switch
				{
					CharColourMode.NCM =>
						IsRRBEnabled
							? Resources.export_ncm_rrb_lookup
							: Resources.export_ncm_lookup,

					_ =>
						IsRRBEnabled
							? Resources.export_fcm_rrb_lookup
							: Resources.export_fcm_lookup,
				},

				Filename = "export-lookup.inf"
			};
		}
	}

	public abstract class BaseCreator
	{
		public CharColourMode CharType { get; init; }
		public bool IsRRBEnabled { get; init; }

		private MemoryStreamProvider? provider;

		protected abstract MemoryStreamProvider CreateStreamProvider();

		public MemoryStreamProvider Get()
		{
			provider ??= CreateStreamProvider();

			return provider;
		}
	}

	#endregion
}
