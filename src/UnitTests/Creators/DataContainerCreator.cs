using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Runners;
using M65Converter.Sources.Runners.Options;

using SixLabors.ImageSharp;
using UnitTests.Models;

namespace UnitTests.Creators;

public class DataContainerCreator : BaseCreator<DataContainer>
{
	#region Overrides

	protected override DataContainer OnCreateInstance()
	{
		var result = new TestDataContainer
		{
			GlobalOptions = new GlobalOptions
			{
				ColourMode = TestData.ColourMode,
				ScreenSize = new Size(40, 25),
				ScreenBaseAddress = 0x10000,
				CharsBaseAddress = 0x20000,
			}
		};

		CreateAndRegisterRunners(result);

		return result;
	}

	#endregion

	#region Running tests

	private void CreateAndRegisterRunners(DataContainer container)
	{
		if (TestData.IsCharsRunnerEnabled)
		{
			container.Runners.Register(
				new CharsRunner
				{
					Data = container,

					Options = new CharOptions
					{
						// Note: we always create output streams, regardless of options, but input is only used if chars runner is requested.
						Inputs = TestData.GetInputBaseCharsDataCreator().Get(),
						OutputCharsStream = new MemoryStreamProvider(),
						OutputPaletteStream = new MemoryStreamProvider()
					}
				}
			);
		}

		if (TestData.IsScreensRunnerEnabled)
		{
			container.Runners.Register(
				new ScreensRunner
				{
					Data = container,

					Options = new ScreenOptions
					{
						// Note: we don't have to provide output file template options since our `TestDataContainer` class always generates an output memory stream - see below.
						Inputs = TestData.GetInputScreensDataCreator().Get(),
						IsRasterRewriteBufferSupported = TestData.IsRRBEnabled
					}
				}
			);
		}
	}

	#endregion

	#region Declarations

	public class TestDataContainer : DataContainer
	{
		public override IStreamProvider? ScreenOutputStreamProvider(ScreenExportData data, Func<FileInfo?> templatePicker)
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
