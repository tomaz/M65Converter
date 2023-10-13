using M65Converter.Runners;
using M65Converter.Sources.Data.Intermediate.Containers;
using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Exporting;
using M65Converter.Sources.Helpers.Images;
using M65Converter.Sources.Helpers.Utils;
using M65Converter.Sources.Runners.Helpers;
using M65Converter.Sources.Runners.Options;

namespace M65Converter.Sources.Runners;

/// <summary>
/// Parses source images and converts them to sprite data.
/// 
/// Raster-rewrite-buffer sprites are appended to the end of the screen and colour data on Mega 65. Each individual sprite requires its own GOTOX bytes followed by the characters that define the 8 pixel tall sprite row. This needs to be repeated for each sprite that is to be displayed on screen. Thus, each screen character row becomes much wider than just the part that's visible on screen at any given time (and of course screen rows themselves may be much larger than what is needed for single screen to implement scrolling, but that doesn't change how RRB sprites work).
/// 
/// For this reason <see cref="RRBSpritesRunner"/> is implemented to work hand in hand with prior <see cref="ScreensRunner"/>. Screens runner is where the "level background" is declared. Then sprites runner appends all sprites to the end. However, it's slightly more complicated - each sprite input file is assumed to contain multiple frames of the same sprite (for example all frames of a single animation). Additionally, single sprite may need multiple animations, for example one for idle state, one for running, one for jumping etc. So we may end up with multiple files where each file defines a single animation of otherwise the same sprite. Or maybe user has all frames, for all animations defined in a single file. To allow user the freedom to setup their inputs in a way that makes most sense, while keeping sprites runner as simple as possible, this is how it works:
/// 
/// - `sprites` commands can be chained in a single command line
/// - Each sprites command can specify as many inputs as needed
/// - Each input is treated as multiple frames of a separate sprite
/// - If `--append-screen` option is used, first frame of each sprite is appended to screen and colour data
/// - If sprites are appended, they are appended to ALL screens declared by `screens` command
/// 
/// So for example user's player sprite may be saved into 3 files, each file declaring frames of specific animation. But we only need to display player sprite once on each screen. Therefore we need to use `sprites` command twice, once with `--append-screen` option and specifying just 1 input, the other without append option but taking the other input files. The output, apart from a single sprite appended to the end of the screen data, is 3 frame tables. Each table contains all the frames of the corresponding input file. The frames data is simple left-to-right, top-to-bottom character indices, so can be easily copied into the screen and colour memory when frames need changing.
/// 
/// Apart from frame data, this runner also creates lookup table with pointers to each frame. That's a convenience only, since each sprite is expected to have all frames of the same size, this data could also be hard coded in Mega 65 programs.
/// </summary>
public class RRBSpritesRunner : BaseRunner
{
	public RRBSpritesOptions Options { get; init; } = null!;

	private List<Sprite> ParsedSprites = new();
	private List<SpriteExportData> ExportedSprites = new();
	private static bool IsScreensFinalized = false;

	#region Overrides

	public override string? Title() => "RRB sprites";

	public override void OnValidateRunPosition(RunnersRegister runners)
	{
		// RRB sprites require that at least one screen command is invoked before. Since we are adding sprites after screen data, we need something to be displayed in the background. Otherwise the display will only include sprites which is almost certainly not what user wants. However if we are not appending data to screens - aka we only generate sprite frames and lookup tables, sprites command can be inserted anywhere.
		if (Options.IsAppendingToScreenDataEnabled)
		{
			runners.ValidatePositionAfter(this, typeof(ScreensRunner), (spritesIndex, lastScreensIndex) =>
				Tools.MultilineString(
					"RRB sprites command must be placed after screens when appending data",
					"Either move sprites command after screens, or remove --append-screens option"
				)
			);
		}
	}

	public override void OnDescribeStep()
	{
		Logger.Debug.Message("All chars will be placed to even indices, if needed transparent images will be inserted before");

		if (Options.IsAppendingToScreenDataEnabled)
		{
			Logger.Debug.Message("First frame of each input sprite will be added after every screen data as RRB layer");
		}

		if (Options.FrameSize != null)
		{
			Logger.Debug.Message($"Source image will be split into {Options.FrameSize.Value.Width}x{Options.FrameSize.Value.Height} chunks if applicable");
		}

		if (Options.OutputFramesTemplate == null)
		{
			Logger.Debug.Message("--out-frame option not provided, sprite frames will not be generated");
		}
	}

	public override void OnParseInputs()
	{
		new InputFilesHandler
		{
			Sources = Options.Inputs
		}
		.Run((index, input) =>
		{
			try
			{
				Data.CharsContainer.SetRestorePoint();

				// Parse input file.
				var sprite = Sprite.Parse(input, Options.FrameSize);

				// Add all extra data to sprite and then add all extra chars from all sprite frames.
				SetupInfoForSprite(sprite);
				AppendExtraCharsFromFrames(sprite);

				// If all went well, add the sprite to the results.
				ParsedSprites.Add(sprite);
			}
			catch (Exception e)
			{
				// If parsing fails, restore characters and proceed with next file.
				Logger.Info.Box(e, "WARNING", "Sprites from this file will be ignored");

				Data.CharsContainer.ResetDataToRestorePoint();
			}
		});
	}

	public override void OnPrepareExportData()
	{
		// As long as we're preparing export data, screens are not finalized yet.
		IsScreensFinalized = false;

		PrepareScreenExportData();
		PrepareSpriteExportData();
	}

	public override void OnFinalizeExportData()
	{
		// We only need to finalize export data once per cmd line run, even if we have multiple RRB sprites commands. And even then only if we append sprites data. Each command had already added export data, so we do it on the first of the commands that asks for appending.
		if (!Options.IsAppendingToScreenDataEnabled) return;
		if (IsScreensFinalized) return;
		IsScreensFinalized = true;

		// All rows need to be finalized with a GOTOX with horizontal position of 1px after right edge.
		var position = Data.GlobalOptions.ScreenPixelsSize.Width;

		foreach (var screen in Data.Screens)
		{
			for (var y = 0; y < screen.Screen.Rows.Count; y++)
			{
				var screenRow = screen.Screen.Rows[y];
				var colourRow = screen.Colour.Rows[y];

				var tag = y == 0 ? "END" : null;

				// Screen data contains the position.
				screenRow.AddScreenAttributesColumn(x: position, tag: tag);
				screenRow.AddScreenDataColumn(charIndex: 0, charAddressInRAM: 0);

				// Colour data is just a standaed GOTOX marker with colour mode.
				colourRow.AddColourAttributesColumn(tag: tag);
				colourRow.AddColourDataColumn(Data.GlobalOptions.ColourMode);
			}
		}

		Logger.Verbose.Message($"Added ending layer to all {Data.Screens.Count} screens");
	}

	public override void OnExportData()
	{
		// We only export frames and lookup data here; screen and colour layers are exported as RRB data with screens runner
		foreach (var sprite in Data.Sprites)
		{
			ExportSpriteFrames(sprite);
			ExportSpriteLookup(sprite);
		}

		// Also export info image - one per each RRB sprites command with all sprites and frames in it.
		ExportInfoImage();
	}

	#endregion

	#region Parsing

	/// <summary>
	/// Sets all extra data for the sprite.
	/// </summary>
	private void SetupInfoForSprite(Sprite sprite)
	{
		// Assign character size for this sprite.
		sprite.CharactersWidth = (int)Math.Ceiling((double)sprite.Width / (double)Data.GlobalOptions.CharInfo.Width);
		sprite.CharactersHeight = (int)Math.Ceiling((double)sprite.Height / (double)Data.GlobalOptions.CharInfo.Height);
	}

	/// <summary>
	/// Appends all extra characters needed for rendering frames of the given sprite.
	/// </summary>
	private void AppendExtraCharsFromFrames(Sprite sprite)
	{
		for (var i = 0; i < sprite.Frames.Count; i++)
		{
			var frame = sprite.Frames[i];

			Logger.Debug.Message($"Adding characters from {sprite.SpriteName}, frame {i}");

			// RRB sprites require specific character setup to allow vertical per-pixel positioning. In essence: characters need to be placed in top-down left-right order with transparent characters on top. For example, if we have 2x2 sprite:
			// 00 01
			// 02 03
			//
			// Then characters memory needs to be:
			// TC 00 02 TC 01 03 TC
			//
			// TC represents transparent character. Note: last character byte must be followed by a transparent character as well because vertical scrolling takes couple lines from subsequent character in the memory. `ImageSplitter` automatically takes care of this due to the options we use when constructing. Also note: we must use one transparent image after the column and then a new one before next column, otherwise vertical scrolling will take additional pixels from first subsequent data character of next column or sprite.
			//
			// Answers to my question on Mega65 Discord channel:
			// https://discord.com/channels/719326990221574164/782757495180361778/1154014348654690395
			// https://discord.com/channels/719326990221574164/782757495180361778/1154077212232929300
			var result = new ImageSplitter
			{
				ItemWidth = Data.GlobalOptions.CharInfo.Width,
				ItemHeight = Data.GlobalOptions.CharInfo.Height,
				TransparentImageInsertion = TransparentImageInsertion.BeforeAndAfter,
				TransparentImageInsertionRule = TransparentImageRule.AlwaysAdd,
				TransparencyOptions = TransparencyOptions.KeepAll,
				DuplicatesOptions = DuplicatesOptions.KeepAll,
				ParsingOrder = ParsingOrder.ColumnByColumn
			}
			.Split(frame.Image, Data.CharsContainer);

			// We have to keep all chars of each frame to be part of the same palette bank. Otherwise vertical scrolling won't work correctly.
			Data.CharsContainer.RequireSamePaletteBank(new ImageGroupData
			{
				Description = $"sprite {sprite.SpriteName}, frame {i}",
				Images = result.Items
			});

			// Assign indexed image to frame.
			frame.IndexedImage = result.IndexedImage;

			Logger.Verbose.Message($"Found {result.ParsedCount}, added {result.AddedCount} unique characters");
		}

		Logger.Verbose.Separator();
		Logger.Debug.Message($"{Data.CharsContainer.Images.Count} total characters found");
	}

	#endregion

	#region Converting

	private void PrepareScreenExportData()
	{
		// We only add if user explicitly allows it to avoid accidentally adding a frame from each and every input file.
		if (Options.IsAppendingToScreenDataEnabled == false) return;

		var screenWidth = Data.GlobalOptions.ScreenPixelsSize.Width;
		var spriteIndex = 0;
		var framesAdded = 0;
		var charsPerRowAdded = 0;

		foreach (var sprite in ParsedSprites)
		{
			// Ignore sprite if there's no frame. Probably won't happen, but better to be proactive than crash...
			if (sprite.Frames.Count == 0) continue;

			// Prepare position for this sprite. Default to top-right (just outside the screen).
			var position = new Point(Data.GlobalOptions.ScreenSize.Width * Data.GlobalOptions.CharInfo.Width, 0);
			if (Options.SpritePositions.Length > 0)
			{
				// After we reach positions end, we take the last one for all subsequent sprites.
				var positionIndex = spriteIndex >= Options.SpritePositions.Length
					? Options.SpritePositions.Length - 1
					: spriteIndex;
				position = Options.SpritePositions[positionIndex];
			}

			// Prepare Y components for Mega 65 hardware.
			var yComponents = ScreenExportData.RRBYComponents(position.Y, Data.GlobalOptions.CharInfo);

			Logger.Verbose.Message($"Sprite {spriteIndex} positioned at ({position.X},{position.Y})");
			Logger.Verbose.Option($"Data starts in row {yComponents.Row}, Y offset {yComponents.Offset}");
			Logger.Verbose.Option($"{sprite.Frames.Count} frames");

			// We need to add the first frame to ALL screens that were previously parsed.
			var frame = sprite.Frames[0];
			foreach (var screen in Data.Screens)
			{
				// Again, let's be proactive in case screen is empty.
				if (screen.Screen.Rows.Count == 0) continue;
				if (screen.Colour.Rows.Count == 0) continue;

				void FillRow(Action<int, ScreenExportData.Row, ScreenExportData.Row> handler)
				{
					// Screens and colour arrays have the same dimension, so we can use any count.
					for (var y = 0; y < screen!.Screen.Rows.Count; y++)
					{
						var screenRow = screen.Screen.Rows[y];
						var colourRow = screen.Colour.Rows[y];

						handler(y, screenRow, colourRow);
					}
				}

				void AddScreenDataColumn(ScreenExportData.Row row, int x, int y, int charIndex)
				{
					var column = row.AddScreenDataColumn(
						charIndex: charIndex,
						charAddressInRAM: Data.CharIndexInRam(charIndex)
					);

					column.IsSprite = true;

					if (framesAdded == 0) charsPerRowAdded++;
				}

				void AddColourDataColumn(ScreenExportData.Row row, int x, int y, int paletteBank = 0)
				{
					var column = row.AddColourDataColumn(
						colourMode: Data.GlobalOptions.ColourMode,
						paletteBank: paletteBank
					);

					column.IsSprite = true;

					if (framesAdded == 0) charsPerRowAdded++;
				}

				// First column of each row is GOTOX data. The only important piece is initial position of the sprite.
				FillRow((y, screen, colour) =>
				{
					screen.AddScreenAttributesColumn(x: position.X, yOffset: yComponents.Offset, tag: sprite?.SpriteName);
					colour.AddColourAttributesColumn(tag: sprite?.SpriteName);

					if (framesAdded == 0) charsPerRowAdded += 2;
				});

				// Note: for negative rows, this code will still behave correctly because of the Y loop below.
				var spriteTopRow = yComponents.Row;
				var spriteBottomRow = spriteTopRow + frame.Image.Height / Data.GlobalOptions.CharInfo.Height - 1;

				// Other columns contain sprite characters. We fill them in column by column.
				for (var x = 0; x < frame.IndexedImage.Width; x++)
				{
					FillRow((y, screen, colour) =>
					{
						// Prepare char zero based index.
						var charIndex = y switch
						{
							var ty when (y < spriteTopRow) =>
								// Anything above sprite needs to be filled with transparent characters - the specific ones that need to be placed above sprite row.
								frame.IndexedImage[x, 0],

							var sy when (y >= spriteTopRow && y <= spriteBottomRow) =>
								// Initially we fill in frame rows. However frame's indexed image has transparent characters in top and bottom row, we only insert the actual sprite "data" chars here. Note: we must use data container to get the correct bank, frame indexed image is not updated when palette is merged (in case of 4-bit NCM colour mode).
								frame.IndexedImage[x, y - spriteTopRow + 1],

							_ =>
								// And finally we fill in remaining rows with transparent characters. However not just any transparent character - we MUST use extra transparent characters from the bottom of our frame. This ensures sprite will correctly scroll vertically.
								frame.IndexedImage[x, frame.IndexedImage.Height - 1]
						};

						// Now that the character has been determined, fill in the data.
						var charData = Data.CharsContainer.Images[charIndex];
						AddScreenDataColumn(screen, x, y, charIndex: charIndex);
						AddColourDataColumn(colour, x, y, paletteBank: charData.IndexedImage.Bank);
					});
				}
			}

			// We increment frames here; we use a value of 0 to detect we're adding the first frame and thus need to count how many chars are appended.
			framesAdded++;
			spriteIndex++;
		}

		Logger.Debug.Message($"{framesAdded} sprites added");
		Logger.Debug.Message($"{charsPerRowAdded * Data.GlobalOptions.CharInfo.BytesPerCharIndex} bytes added to each screen & colour row");
	}

	private void PrepareSpriteExportData()
	{
		List<SpriteExportData.CharData> RowToAddTo(SpriteExportData.FrameData frameData, IndexedImage image, int y)
		{
			if (y == 0)
			{
				return frameData.StartingTransparentChars;
			}
			else if (y == image.Height - 1)
			{
				return frameData.EndingTransparentChars;
			}
			else
			{
				var row = new List<SpriteExportData.CharData>();
				frameData.Chars.Add(row);
				return row;
			}
		}

		foreach (var sprite in ParsedSprites)
		{
			var spriteData = new SpriteExportData
			{
				SpriteName = sprite.SpriteName,
				CharactersWidth = sprite.CharactersWidth,
				CharactersHeight = sprite.CharactersHeight + 2,
			};

			for (var i = 0; i < sprite.Frames.Count; i++)
			{
				var frame = sprite.Frames[i];
				var image = frame.IndexedImage;
				var frameData = new SpriteExportData.FrameData
				{
					Duration = frame.Duration
				};

				for (var y = 0; y < image.Height; y++)
				{
					// Prepare the row into which we'll be adding the chars.
					var row = RowToAddTo(frameData, image, y);

					// Add all char indices to the given row.
					for (var x = 0; x < image.Width; x++)
					{
						var charIndex = image[x, y];
						var charAddress = Data.CharIndexInRam(charIndex);

						row.Add(new SpriteExportData.CharData
						{
							CharIndex = charIndex,
							CharAddress = charAddress,
							Values = charAddress.ToLittleEndianList()
						});
					}
				}

				spriteData.Frames.Add(frameData);
			}

			Data.Sprites.Add(spriteData);
			ExportedSprites.Add(spriteData);
		}
	}

	#endregion

	#region Exporting

	private void ExportSpriteFrames(SpriteExportData sprite)
	{
		var streamProvider = Data.SpriteOutputStreamProvider(sprite, () => Options.OutputFramesTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.SpriteFramesDataStreams.Add(streamProvider);

		CreateExporter("sprite frames data", streamProvider).Export(writer =>
		{
			new SpriteDataExporter
			{
				Data = Data,
				Sprite = sprite
			}
			.Export(writer);
		});
	}

	private void ExportSpriteLookup(SpriteExportData sprite)
	{
		var streamProvider = Data.SpriteOutputStreamProvider(sprite, () => Options.OutputLookupTemplate);
		if (streamProvider == null) return;

		Data.UsedOutputStreams.SpriteLookupDataStreams.Add(streamProvider);

		CreateExporter("sprite lookup tables", streamProvider).Export(writer =>
		{
			new SpriteLookupDataExporter
			{
				Data = Data,
				Sprite = sprite
			}
			.Export(writer);
		});
	}

	public void ExportInfoImage()
	{
		if (Data.GlobalOptions.InfoImageRenderingScale <= 0) return;

		var streamProvider = Options.OutputInfoImageStream;
		if (streamProvider == null) return;

		Data.UsedOutputStreams.SpriteInfoImagesStreams.Add(streamProvider);

		CreateExporter("info image", streamProvider).Prepare(stream =>
		{
			new SpriteInfoImageExporter
			{
				Data = Data,
				Sprites = ExportedSprites,
			}
			.Export(stream);
		});
	}

	#endregion
}
