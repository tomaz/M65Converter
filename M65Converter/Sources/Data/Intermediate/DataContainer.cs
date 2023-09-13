using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Data.Providers;
using M65Converter.Sources.Exporting;
using M65Converter.Sources.Exporting.Utils;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Options;

namespace M65Converter.Sources.Data.Intermediate;

/// <summary>
/// Container for all parsed data.
/// 
/// Besides being a container, this class also validates and exports common data.
/// </summary>
public class DataContainer
{
	/// <summary>
	/// Global options.
	/// </summary>
	public GlobalOptions GlobalOptions { get; set; } = null!;

	/// <summary>
	/// All options for generating characters data.
	/// </summary>
	public CharOptions CharOptions { get; set; } = null!;

	/// <summary>
	/// All options for generating screens and colours data.
	/// </summary>
	public ScreenOptions ScreenOptions { get; set; } = null!;

	/// <summary>
	/// All the parsed characters.
	/// </summary>
	public ImagesContainer CharsContainer { get; } = new();

	/// <summary>
	/// All colours needed for all characters.
	/// </summary>
	public List<ColourData> Palette { get; set; } = new();

	/// <summary>
	/// Screen and colour data in a form suitable for exporting.
	/// </summary>
	public List<ScreenData> Screens { get; } = new();

	/// <summary>
	/// Contains all stream providers used for output.
	/// 
	/// Only valid after <see cref="ExportData"/> completes. This is mainly used for unit testing reasons.
	/// </summary>
	public OutputStreams UsedOutputStreams { get; private set; } = new();

	#region Subclass

	/// <summary>
	/// Prepares the output stream for the given screen using the given template.
	/// 
	/// A subclass that wants to change how all output streams are prepared can override this and customize as needed. Default implementation prepares file stream from the template returned from template picker.
	/// </summary>
	protected virtual IStreamProvider? OutputStreamProvider(ScreenData data, Func<FileInfo?> templatePicker)
	{
		// If template is not provided, user doesn't want to export this data.
		var template = templatePicker();
		if (template == null) return null;

		var path = PathFromTemplate(template.FullName, data);

		return new FileStreamProvider
		{
			FileInfo = new FileInfo(path)
		};
	}

	/// <summary>
	/// Converts the given template path into the actual one.
	/// 
	/// A subclass that only needs to change how templates are converted into proper paths, can override this and customize the path.
	/// </summary>
	protected virtual string PathFromTemplate(string template, ScreenData screen)
	{
		return template.Replace("{level}", screen.LevelName);
	}

	#endregion

	#region Validating

	/// <summary>
	/// Validates input data to make sure we can export it.
	/// 
	/// Note: this doesn't take care of all possible issues. It only checks for common issues.
	/// </summary>
	public void ValidateData()
	{
		if (CharsContainer.Images.Count > 8192)
		{
			throw new InvalidDataException("Too many characters to fit 2 bytes, adjust source files");
		}

		if (Palette.Count > 256)
		{
			throw new InvalidDataException("Too many colours in the palette, adjust source files");
		}
	}

	#endregion

	#region Exporting

	/// <summary>
	/// Exports all generated data.
	/// </summary>
	public void ExportData()
	{
		Logger.Debug.Separator();

		new TimeRunner
		{
			Title = "Exporting",
			Header = TimeRunner.DoubleLineHeader,
			Footer = TimeRunner.DoubleLineFooter
		}
		.Run(() =>
		{
			// Note: exporting is implemented in this class to have single method for exporting all data. This is also a simple way of keeping the knowledge to which data should be exported and which output stream to use and keep exporter classes focused on single job of exporting the data
			ExportChars();
			ExportPalette();

			foreach (var data in Screens)
			{
				ExportScreenColour(data);
				ExportScreenData(data);
				ExportLookupTable(data);
				ExportInfoImage(data);
			}

			// As the last output we print potential export issues. We want them as prominent as possible hence at the end of likely quite long output.
			PrintPotentialExportIssues();
		});
	}

	private void ExportChars()
	{
		UsedOutputStreams.CharsStream = CharOptions.OutputCharsStream;
		
		if (CharOptions.OutputCharsStream == null) return;

		CreateExporter("chars", CharOptions.OutputCharsStream).Export(writer =>
		{
			new CharsExporter
			{
				Data = this,
				Chars = CharsContainer
			}
			.Export(writer);
		});
	}

	private void ExportPalette()
	{
		UsedOutputStreams.PaletteStreram = CharOptions.OutputPaletteStream;

		if (CharOptions.OutputPaletteStream == null) return;

		CreateExporter("palette", CharOptions.OutputPaletteStream).Export(writer =>
		{
			new PaletteExporter
			{
				Data = this,
				Palette = Palette.Select(x => x.Colour).ToList(),
			}
			.Export(writer);
		});
	}

	private void ExportScreenColour(ScreenData data)
	{
		var streamProvider = OutputStreamProvider(data, () => ScreenOptions.OutputColourTemplate);
		if (streamProvider == null) return;

		UsedOutputStreams.ColourDataStreams.Add(streamProvider);

		CreateExporter("colour ram", streamProvider).Export(writer =>
		{
			new ScreenColoursExporter
			{
				Data = this,
				Screen = data
			}
			.Export(writer);
		});
	}

	private void ExportScreenData(ScreenData data)
	{
		var streamProvider = OutputStreamProvider(data, () => ScreenOptions.OutputScreenTemplate);
		if (streamProvider == null) return;

		UsedOutputStreams.ScreenDataStreams.Add(streamProvider);

		CreateExporter("screen data", streamProvider).Export(writer =>
		{
			new ScreenDataExporter
			{
				Data = this,
				Screen = data
			}
			.Export(writer);
		});
	}

	private void ExportLookupTable(ScreenData data)
	{
		var streamProvider = OutputStreamProvider(data, () => ScreenOptions.OutputLookupTemplate);
		if (streamProvider == null) return;

		UsedOutputStreams.LookupDataStreams.Add(streamProvider);

		CreateExporter("layer info", streamProvider).Export(writer =>
		{
			new ScreenLookupExporter
			{
				Data = this,
				Screen = data
			}
			.Export(writer);
		});
	}

	private void ExportInfoImage(ScreenData data)
	{
		if (GlobalOptions.InfoImageRenderingScale <= 0) return;

		var streamProvider = OutputStreamProvider(data, () => ScreenOptions.OutputInfoTemplate);
		if (streamProvider == null) return;

		UsedOutputStreams.InfoImageStreams.Add(streamProvider);

		CreateExporter("info image", streamProvider).Prepare(stream =>
		{
			new ScreenInfoImageExporter
			{
				Data = this,
				Screen = data
			}
			.Export(stream);
		});
	}

	private void PrintPotentialExportIssues()
	{
		var isCharsOut = CharOptions.OutputCharsStream != null;
		var isPaletteOut = CharOptions.OutputPaletteStream != null;

		var isScreenOut = ScreenOptions.OutputScreenTemplate != null;
		var isColourOut = ScreenOptions.OutputColourTemplate != null;
		var isLookupOut = ScreenOptions.OutputLookupTemplate != null;
		var isAnyLevelOut = isScreenOut || isColourOut || isLookupOut;

		var isHeaderPrinted = false;

		void Exclaim(string message)
		{
			if (!isHeaderPrinted)
			{
				Logger.Info.Separator();
				Logger.Info.Exclamation("IMPORTANT:");
				isHeaderPrinted = true;
			}

			Logger.Info.Exclamation(message);
		}

		if (!isCharsOut && isPaletteOut)
		{
			Exclaim("--out-palette used but --out-chars not. This may result in invalid char colours!");
		}

		if (isCharsOut && !isPaletteOut)
		{
			Exclaim("--out-chars used but --out-palette not. This may result in invalid char colours!");
		}

		if (!isCharsOut && isAnyLevelOut)
		{
			Exclaim("--out-chars not used but at least one of screen data is (--out-screen, --out-colour, --out-lookup). This may result in invalid characters displayed");
		}

		if (isScreenOut && !isColourOut)
		{
			Exclaim("--out-screen used but --out-colour not. This may result in garbled screen display.");
		}

		if (!isScreenOut && isColourOut)
		{
			Exclaim("--out-colour used but --out-screen not. This may result in garbled screen display.");
		}

		if (!isScreenOut && !isColourOut && isLookupOut)
		{
			Exclaim("--out-screen and --out-colour not used but --out-lookup is. This may result in invalid addresses from lookup tables being used.");
		}
	}

	private Exporter CreateExporter(string description, IStreamProvider provider)
	{
		return new()
		{
			LogDescription = description,
			Stream = provider
		};
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Takes "relative" character index (0 = first generated character) and converts it to absolute character index as needed for Mega 65 hardware, taking into condideration char base address.
	/// </summary>
	public int CharIndexInRam(int relativeIndex) => GlobalOptions.CharInfo.CharIndexInRam(ScreenOptions.CharsBaseAddress, relativeIndex);

	#endregion

	#region Declarations

	public class OutputStreams
	{
		public IStreamProvider? CharsStream { get; set; }
		public IStreamProvider? PaletteStreram { get; set; }
		public List<IStreamProvider> ScreenDataStreams { get; } = new();
		public List<IStreamProvider> ColourDataStreams { get; } = new();
		public List<IStreamProvider> LookupDataStreams { get; } = new();
		public List<IStreamProvider> InfoImageStreams { get; } = new();
	}

	#endregion
}
