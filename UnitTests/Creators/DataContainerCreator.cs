using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Helpers.Inputs;

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
				IsRasterRewriteBufferSupported = IsRRBEnabled
			}
		};

		return data;
	}

	public class TestDataContainer : DataContainer
	{
		protected override IStreamProvider? OutputStreamProvider(ScreenData data, Func<FileInfo?> templatePicker) => new MemoryStreamProvider
		{
			Filename = templatePicker()?.Name ?? string.Empty,
		};
	}
}
