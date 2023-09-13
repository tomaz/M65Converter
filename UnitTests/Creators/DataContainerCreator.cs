using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners.Options;

using SixLabors.ImageSharp;

using UnitTests.Models;

namespace UnitTests.Creators;

public class DataContainerCreator
{
	public CharColourMode CharType { get; init; }
	public bool IsRRBEnabled { get; init; }

	private DataContainer? data;

	public DataContainer Get()
	{
		data ??= new TestDataContainer
		{
			GlobalOptions = new GlobalOptions
			{
				ColourMode = CharType,
			},
			
			CharOptions = new CharOptions
			{
				OutputCharsStream = new MemoryStreamProvider(),
				OutputPaletteStream = new MemoryStreamProvider()
			},
			
			ScreenOptions = new ScreenOptions
			{
				Inputs = new[] { ResourcesCreator.CharsInput() },
				ScreenSize = new Size(40, 25),
				ScreenBaseAddress = 0x10000,
				CharsBaseAddress = 0x20000,
				IsRasterRewriteBufferSupported = IsRRBEnabled
			}
		};

		return data;
	}

	public class TestDataContainer : DataContainer
	{
		protected override IStreamProvider? OutputStreamProvider(ScreenData data, Func<FileInfo?> templatePicker)
		{
			// We always create a memory stream, regardless of whether name template is setup or not. This way we don't have to setup each and every name template in options above.
			return new MemoryStreamProvider
			{
				Filename = templatePicker()?.Name ?? string.Empty,
			};
		}
	}
}
