using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners.Options;

using SixLabors.ImageSharp;

using UnitTests.Models;

namespace UnitTests.Creators;

public class DataContainerCreator : BaseCreator<DataContainer>
{
	#region Overrides

	protected override DataContainer OnCreateObject()
	{
		return new TestDataContainer
		{
			GlobalOptions = new GlobalOptions
			{
				ColourMode = ColourMode,
				ScreenSize = new Size(40, 25),
				ScreenBaseAddress = 0x10000,
				CharsBaseAddress = 0x20000,
			},

			CharOptions = new CharOptions
			{
				// Note: we always create output streams, regardless of options, but input is only used if chars runner is requested.
				Inputs = new ResourcesCreator.InputBaseCharsCreator(this).Get(),
				OutputCharsStream = new MemoryStreamProvider(),
				OutputPaletteStream = new MemoryStreamProvider()
			},

			ScreenOptions = new ScreenOptions
			{
				// Note: we don't have to provide output file template options since our `TestDataContainer` class always generates an output memory stream - see below.
				Inputs = new ResourcesCreator.InputScreensCreator(this).Get(),
				IsRasterRewriteBufferSupported = IsRRBEnabled
			}
		};
	}

	protected override IStreamProvider? OnGetActualStream(DataContainer data)
	{
		// We should not call this method for data container!
		throw new NotImplementedException();
	}

	#endregion

	#region Declarations

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

	#endregion
}
