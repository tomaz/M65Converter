using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners;

using UnitTests.Models;

namespace UnitTests.Creators;

public class CharsCreator
{
	private bool isRRBEnabled;
	private int charsBaseAddress = 0x20000;
	private CharsType charsType;

	public CharsCreator RRB(bool enabled)
	{
		isRRBEnabled = enabled;
		return this;
	}

	public CharsCreator CharsBaseAddress(int address)
	{
		charsBaseAddress = address;
		return this;
	}

	public CharsCreator OutputType(CharsType type)
	{
		charsType = type;
		return this;
	}

	public CharsRunner Get()
	{
		var options = new CharsRunner.OptionsType
		{
			InputsOutputs = new[]
			{
				new CharsRunner.OptionsType.InputOutput
				{
					Input = ResourcesCreator.CharsInput(),
					OutputCharsStream = new MemoryStreamProvider { Filename = "output-chars.bin" },
					OutputPaletteStream = new MemoryStreamProvider { Filename = "output-palette.pal" },
					OutputScreenStream = new MemoryStreamProvider { Filename = "output-screen.bin" },
					OutputColourStream = new MemoryStreamProvider { Filename = "output-colour.bin" },
					OutputInfoDataStream = new MemoryStreamProvider { Filename = "output-info.inf" },
					OutputInfoImageStream = new MemoryStreamProvider { Filename = "output-info.png" }
				}
			},

			CharColour = charsType switch
			{
				CharsType.NCM => CharsRunner.OptionsType.CharColourType.NCM,
				_ => CharsRunner.OptionsType.CharColourType.FCM
			},

			IsRasterRewriteBufferSupported = isRRBEnabled,
			CharsBaseAddress = charsBaseAddress,
		};

		return new CharsRunner()
		{
			Options = options
		};
	}
}
