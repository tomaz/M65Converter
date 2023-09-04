namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Contains all post-processed information about layers, ready for exporting or rendering.
/// </summary>
public class LayersData
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

	/// <summary>
	/// All colours.
	/// </summary>
	public List<ColourData> Palette { get; set; } = new();

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
		/// Optional extra data. This depends on what the parent layer represents.
		/// </summary>
		public int Data1 { get; set; }

		/// <summary>
		/// Optional extra data. This depends on what the parent layer represents.
		/// </summary>
		public int Data2 { get; set; }

		/// <summary>
		/// Optional tag. This depends on what the parent layer represents.
		/// </summary>
		public string? Tag { get; set; }

		/// <summary>
		/// Returns all values as single little endian value (only supports up to 4 bytes!)
		/// </summary>
		public int LittleEndianData
		{
			get
			{
				var result = 0;

				// Values are already in little endian order.
				foreach (var value in Values)
				{
					result <<= 8;
					result |= value;
				}

				return result;
			}
		}

		/// <summary>
		/// Returns all values as single big endian value (only supports up to 4 digits!)
		/// </summary>
		public int BigEndianData
		{
			get
			{
				var result = 0;

				// Values are little endian order, so we need to reverse the array.
				for (var i = Values.Count - 1; i >= 0; i--)
				{
					result <<= 8;
					result |= Values[i];
				}

				return result;
			}
		}

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

	#endregion
}
