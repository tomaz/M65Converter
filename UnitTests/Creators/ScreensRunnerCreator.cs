using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Inputs;
using M65Converter.Sources.Runners;

using UnitTests.Models;

namespace UnitTests.Creators;

public class ScreensRunnerCreator
{
	public DataContainer Data { get; init; } = null!;
	public ScreenOptionsType.CharColourType CharType { get; init; } = ScreenOptionsType.CharColourType.FCM;
	public int CharsBaseAddress { get; init; } = 0x20000;
	public bool IsRRBEnabled { get; init; } = false;

	private ScreensRunner? runner;

	public ScreensRunner Get()
	{
		if (runner == null)
		{
			Data.ScreenOptions = new ScreenOptionsType
			{
				InputsOutputs = new[]
				{
				new ScreenOptionsType.InputOutput
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

				CharColour = CharType,
				CharsBaseAddress = CharsBaseAddress,
				IsRasterRewriteBufferSupported = IsRRBEnabled,
			};

			runner = new ScreensRunner()
			{
				Data = Data
			};
		}

		return runner;
	}
}
