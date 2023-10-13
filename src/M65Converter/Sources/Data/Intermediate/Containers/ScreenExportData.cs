using M65Converter.Sources.Data.Intermediate.Helpers;
using M65Converter.Sources.Helpers.Utils;

namespace M65Converter.Sources.Data.Intermediate.Containers;

/// <summary>
/// Contains all post-processed information about a screen, ready for exporting or rendering.
/// </summary>
public class ScreenExportData
{
	/// <summary>
	/// The width of the longest layer.
	/// </summary>
	public int LayerWidth { get; set; }

	/// <summary>
	/// The height of the tallest layer.
	/// </summary>
	public int LayerHeight { get; set; }

	/// <summary>
	/// Level name.
	/// </summary>
	public string LevelName { get; set; } = null!;

	/// <summary>
	/// Root folder where level source files are located.
	/// </summary>
	public string RootFolder { get; set; } = null!;

	/// <summary>
	/// The screen RAM data.
	/// </summary>
	public Layer Screen { get; set; } = new();

	/// <summary>
	/// The colour RAM data.
	/// </summary>
	public Layer Colour { get; set; } = new();

	#region Helpers

	/// <summary>
	/// Converts given absolute Y coordinate into components suitable for Mega65 hardware.
	/// 
	/// This is primarily used for RRB sprites.
	/// </summary>
	public static YComponents RRBYComponents(int y, CharInfo? charInfo = null)
	{
		// RRB vertical positioning is not completely straightforward.
		//
		// The bulk positioning occurs on screen data where we influence the vertical position on 8px boundary simply by the row in which we start drawing sprite characters. This part is quite simple, we divide the Y coordinate by 8 and get the row index.
		//
		// Pixel positioning however is more complicated. We can assign an offset in the range of 0-7 pixels in the 2 screen GOTOX bytes. The value represents the amount of pixels by which the layer is vertically shifted. But the complication the shift occurs upwards. So while the base value calculation is a simple modulo by 8, we have to reverse the value since the larger the absolute Y, more down we need to shift. This is also simple operation of subtracting the modulo result from 7. This yields: for 0 -> 7, 1 -> 6 etc. So this means that Y of 0 will shift the layer upwards by 7 pixels. Therefore we have to compensate and increment the row index we calculated above. Even so, there's 1 pixel discrepancy (at coordinate 0, layer is rendered at screen pixel 1). But I'm fine with this (for now ;)

		// Constants - we allow hard coded values to simplify method calls.
		var charHeight = charInfo?.Height ?? 8;

		// First calculate the row index.
		var row = y / charHeight;

		// Now prepare Y pixel offset.
		var offsetModulo = Math.Abs(y) % charHeight;	// 0 -> 0, 1 -> 1 ...

		// For positive Y we have to use reversed modulo and if it's positive, we also have to increment the row.
		// Note: on Mega65 reversed offset can be calculated with `eor #7`, simpler and faster than `sec + sbc #7`.
		var offsetReversed = 7 - offsetModulo;			// 0 -> 7, 1 -> 6 ...
		if (y >= 0)
		{
			offsetModulo = offsetReversed;
			if (offsetReversed > 0) row++;
		}

		return new()
		{
			Row = row,
			Offset = offsetModulo
		};
	}

	#endregion

	#region Declarations

	/// <summary>
	/// The "data" - this can be anything that uses rows and columns format.
	/// </summary>
	public class Layer
	{
		/// <summary>
		/// The name of this layer. Note: for RRB we only produce 1 layer, so this name is the name of the first layer (aka the bottom most one in terms of rendering).
		/// </summary>
		public string Name { get; set; } = null!;

		/// <summary>
		/// Layer width in characters.
		/// </summary>
		public int Width { get => Rows.First().Columns.Count; }

		/// <summary>
		/// Layer height in characters (this is convenience so we can treat height the same way as width instead of having to use rows count - even though this is in fact simple rows count).
		/// </summary>
		public int Height { get => Rows.Count; }

		/// <summary>
		/// Number of all elements in this structure.
		/// </summary>
		public int Count { get => Width * Height; }

		/// <summary>
		/// Returns the column at the given coordinate.
		/// </summary>
		public Column this[int x, int y]
		{
			get => Rows[y].Columns[x];
		}

		/// <summary>
		/// All rows of the data.
		/// </summary>
		public List<Row> Rows { get; } = new();
	}

	/// <summary>
	/// Data for individual row.
	/// </summary>
	public class Row
	{
		/// <summary>
		/// All columns of this row.
		/// </summary>
		public List<Column> Columns { get; } = new();

		/// <summary>
		/// Size of the row in bytes.
		/// </summary>
		public int Size
		{
			get => Columns.Count > 0
				? Columns.Count * Columns[0].Values.Count
				: 0;
		}

		/// <summary>
		/// Convenience for adding a new <see cref="Column"/> with the given values to the end of <see cref="Columns"/> list.
		/// </summary>
		public Column AddColumn(params byte[] values)
		{
			var result = new Column
			{
				Values = values.ToList()
			};

			Columns.Add(result);

			return result;
		}

		/// <summary>
		/// Adds new column as screen data column.
		/// </summary>
		/// <param name="charIndex">Zero based index of the character.</param>
		/// <param name="charAddressInRAM">Memory address based index of the character.</param>
		/// <param name="tag">Optional tag.</param>
		/// <param name="type">Type of this data. Defaults to <see cref="Column.DataType.Data"/></param>
		public Column AddScreenDataColumn(
			int charIndex,
			int charAddressInRAM,
			string? tag = null,
			Column.DataType type = Column.DataType.Data
		)
		{
			// Char index is always the same regardless of mode.
			byte byte1 = (byte)(charAddressInRAM & 0xff);
			byte byte2 = (byte)(charAddressInRAM >> 8 & 0xff);

			var column = AddColumn(byte1, byte2);

			// Assign data type and layer name.
			column.LayerName = tag;
			column.Type = type;

			// For chars data1 is char index, data2 is "index in ram" or "address" (of sorts).
			column.CharIndex = charIndex;
			column.CharAddress = charAddressInRAM;

			return column;
		}

		/// <summary>
		/// Adds new column as colour data column.
		/// </summary>
		/// <param name="colourMode">Colour mode</param>
		/// <param name="paletteBank">Palette bank, only relevant for NCM mode</param>
		/// <param name="tag">Optional tag.</param>
		/// <param name="type">Type of this data. Defaults to <see cref="Column.DataType.Data"/></param>
		public Column AddColourDataColumn(
			CharColourMode colourMode,
			int paletteBank = 0,
			string? tag = null,
			Column.DataType type = Column.DataType.Data
		)
		{
			switch (colourMode)
			{
				case CharColourMode.FCM:
					{
						// For FCM colours are not important (until we implement char flipping for example), we always use 0.
						var column = AddColumn(0x00, 0x00);
						column.LayerName = tag;
						column.Type = type;
						return column;
					}

				case CharColourMode.NCM:
					{
						// For NCM colours RAM is where we set FCM mode for the character as well as palette bank.

						//             +-------------- vertically flip character
						//             |+------------- horizontally flip character
						//             ||+------------ alpha blend mode
						//             |||+----------- gotox
						//             ||||+---------- use 4-bits per pixel and 16x8 chars
						//             |||||+--------- trim pixels from right char side
						//             |||||| +------- number of pixels to trim
						//             |||||| |
						//             ||||||-+
						byte byte1 = 0b00001000;

						//             +--------------- underline
						//             |+-------------- bold
						//             ||+------------- reverse
						//             |||+------------ blink
						//             |||| +---------- colour bank 0-16
						//             |||| |
						//             ||||-+--
						byte byte2 = 0b00000000;
						byte2 |= (byte)(paletteBank & 0x0f);

						// No sure why colour bank needs to be in high nibble. According to documentation this is needed if VIC II multi-colour-mode is enabled, however in my code this is also needed if VIC III extended attributes are enabled (AND VIC II MCM is disabled).
						byte2 = byte2.SwapNibble();

						var column = AddColumn(byte1, byte2);

						// For colours data1 represents colour bank (only meaningful for NCM).
						column.PaletteBank = paletteBank;
						column.LayerName = tag;
						column.Type = type;

						return column;
					}

				default:
					{
						throw new InvalidDataException($"Unknown colour mode {colourMode}");
					}
			}
		}

		/// <summary>
		/// Adds new column as screen attributes column.
		/// </summary>
		/// <param name="x">Layer X position</param>
		/// <param name="yOffset">Layer Y position offset 0-7, can use <see cref="RRBYComponents"/> to calculate.</param>
		/// <param name="tag">Optional tag.</param>
		public Column AddScreenAttributesColumn(
			int x = 0,
			int yOffset = 0,
			string? tag = null
		)
		{
			// Byte 0 is lower 8 bits of new X position for upcoming layer. We set it to 0 which means "render over the top of left-most character".
			byte byte1 = (byte)(x & 0xff);

			// Byte 1:
			//              +-------------- FCM char Y offset
			//              |  +----------- reserved
			//              |  |  +-------- upper 2 bits of X position
			//             /-\/+\|\
			byte byte2 = 0b00000000;
			byte yOffs = (byte)((yOffset & 0b00000111) << 5);
			byte xHi = (byte)(x >> 8 & 0xff); // 0b00000xxx
			byte2 |= (byte)(yOffs | xHi);

			var column = AddColumn(byte1, byte2);
			column.Type = Column.DataType.Attribute;
			column.LayerName = tag;

			return column;
		}

		/// <summary>
		/// Adds new column as colour attributes column.
		/// </summary>
		/// <param name="tag">Optional tag.</param>
		public Column AddColourAttributesColumn(string? tag = null)
		{
			// Byte 0:
			//             +-------------- 1 = don't draw transparent pixels
			//             |+------------- 1 = render following chars as background (sprites appear above)
			//             ||+------------ reserved
			//             |||+----------- GOTOX
			//             ||||+---------- 1 = use pixel row mask from byte2 
			//             |||||+--------- 1 = render following chars as foreground (sprites appear behind)
			//             |||||| +------- reserved
			//             |||||| |
			//             ||||||/\
			byte byte1 = 0b10010000;

			// Byte 1 = pixel row mask.
			byte byte2 = 0x00;

			var column = AddColumn(byte1, byte2);
			column.Type = Column.DataType.Attribute;
			column.LayerName = tag;

			return column;
		}
	}

	/// <summary>
	/// Data for individual column.
	/// </summary>
	public class Column
	{
		/// <summary>
		/// All bytes needed to describe this column, in little endian format.
		/// </summary>
		public List<byte> Values { get; init; } = new();

		/// <summary>
		/// The type of the data.
		/// </summary>
		public DataType Type { get; set; } = DataType.Data;

		/// <summary>
		/// Indicates whether this column represents sprite (true) or layer (false) data.
		/// </summary>
		public bool IsSprite { get; set; }

		/// <summary>
		/// Character zero based index.
		/// </summary>
		public int CharIndex { get; set; }

		/// <summary>
		/// Character address in RAM.
		/// </summary>
		public int CharAddress { get; set; }

		/// <summary>
		/// Palette bank (if applicable).
		/// </summary>
		public int PaletteBank { get; set; }

		/// <summary>
		/// Optional layer name. This depends on what the parent layer represents.
		/// </summary>
		public string? LayerName { get; set; }

		/// <summary>
		/// Returns all values as single little endian value (only supports up to 4 bytes!)
		/// </summary>
		public int LittleEndianData { get => (int)Values.ToArray().AsLittleEndianData(); }

		/// <summary>
		/// Returns all values as single big endian value (only supports up to 4 digits!)
		/// </summary>
		public int BigEndianData { get => (int)Values.ToArray().AsBigEndianData(); }

		#region Declarations

		public enum DataType
		{
			/// <summary>
			/// The value represents a first data of a layer, aka at position (0,0).
			/// </summary>
			FirstData,

			/// <summary>
			/// The value represents non-first data of a layer.
			/// </summary>
			Data,

			/// <summary>
			/// The value represents an attribute (GOTOX).
			/// </summary>
			Attribute,
		}

		#endregion
	}

	public class YComponents
	{
		/// <summary>
		/// Zero based row number into which the top row of the char data is to be copied.
		/// </summary>
		public int Row { get; init; }

		/// <summary>
		/// Offset 0-7 for screen data.
		/// </summary>
		public int Offset { get; init; }
	}

	#endregion
}
