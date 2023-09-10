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

	public static CharsCreator CharsOutput() => new();

	public static PaletteCreator PaletteOutput() => new();

	public static ScreenCreator ScreenOutput() => new();

	public static ColourCreator ColourOutput() => new();

	#region Declarations

	public class CharsCreator : BaseCreator
	{
		public override MemoryStreamProvider Get()
		{
			return new MemoryStreamProvider
			{
				Data = CharsType switch
				{
					CharsType.NCM => 
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
		public override MemoryStreamProvider Get()
		{
			return new MemoryStreamProvider
			{
				Data = CharsType switch
				{
					CharsType.NCM =>
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
		public override MemoryStreamProvider Get()
		{
			return new MemoryStreamProvider
			{
				Data = CharsType switch
				{
					CharsType.NCM =>
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
		public override MemoryStreamProvider Get()
		{
			return new MemoryStreamProvider
			{
				Data = CharsType switch
				{
					CharsType.NCM =>
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

	public abstract class BaseCreator
	{
		protected CharsType CharsType { get; private set; }
		protected bool IsRRBEnabled { get; private set; }

		public BaseCreator Chars(CharsType type)
		{
			CharsType = type;
			return this;
		}

		public BaseCreator RRB(bool enabled)
		{
			IsRRBEnabled = enabled;
			return this;
		}

		public abstract MemoryStreamProvider Get();
	}

	#endregion
}
