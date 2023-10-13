namespace M65Converter.Sources.Data.Intermediate.Images;

/// <summary>
/// Image specified as indices.
/// 
/// Note: index can represent any kind of lookup - characters, colours etc. based on context in which the indexed image is used.
/// </summary>
public class IndexedImage
{
	private List<List<int>> Indexes { get; } = new();

	#region Overrides

	public override string ToString()
	{
		return $"{GetType().Name} {Width}x{Height}";
	}

	#endregion

	#region Accessing

	/// <summary>
	/// Colour palette bank. Only relevant for 4-bit colours.
	/// </summary>
	public int Bank { get; set; } = 0;

	/// <summary>
	/// Number of colours per bank. Only relevant for 4-bit colours. This can be used to calculate the base colour index in merged palette.
	/// </summary>
	public int ColoursPerBank { get; set; } = 0;

	/// <summary>
	/// Gets the width of the image as number of indices.
	/// </summary>
	public int Width
	{
		get => Indexes.Count > 0 ? Indexes[0].Count : 0;
	}

	/// <summary>
	/// Gets the height of the image as number of indices.
	/// </summary>
	public int Height
	{
		get => Indexes.Count;
	}

	/// <summary>
	/// Gets or sets the index at the given coordinate.
	/// 
	/// Note: setting should only be attempted for prefilled images.
	/// </summary>
	public int this[int x, int y]
	{
		get => Indexes[y][x];
		set => Indexes[y][x] = value;
	}

	#endregion

	#region Adding

	/// <summary>
	/// Adds a new row.
	/// </summary>
	public void AddRow()
	{
		Indexes.Add(new List<int>());
	}

	/// <summary>
	/// Adds a new index to the last added row.
	/// </summary>
	public void AddColumn(int index)
	{
		Indexes[^1].Add(index);
	}

	#endregion

	#region Filling

	/// <summary>
	/// Creates an image of the given size pre-filled with the given index.
	/// </summary>
	public void Prefill(int width, int height, int index)
	{
		for (var y = 0; y < height; y++)
		{
			AddRow();

			for (var x = 0; x < width; x++)
			{
				AddColumn(index);
			}
		}
	}

	/// <summary>
	/// Inserts a new column of the given indices to the end of the rows.
	/// 
	/// If the given list is larger than height, only the starting values are copied until the height is filled. If the list if shorter, the remaining values are filled with 0.
	/// 
	/// Requires at least one pre-existing row to be added (to determine the width). Otherwise nothing happens.
	/// </summary>
	public void AddColumn(IEnumerable<int> values)
	{
		if (Indexes.Count == 0) return;

		InsertColumn(
			column: Indexes[0].Count,
			values: values
		);
	}

	/// <summary>
	/// Inserts a whole column of the given indices.
	/// 
	/// If the given list is larger than height, only the starting values are copied until the height is filled. If the list if shorter, the remaining values are filled with 0.
	/// 
	/// Requires at least one pre-existing row to be added (to determine the width). Otherwise nothing happens.
	/// </summary>
	public void InsertColumn(int column, IEnumerable<int> values)
	{
		if (Height == 0) return;

		// Prepare the list with the given values, but only up to our expected height.
		var rows = values
			.Take(Math.Min(Height, values.Count()))
			.ToList();

		// If rows are still missing, fill in with 0.
		while (rows.Count < Height) rows.Add(0);

		// Insert all values into the given column.
		var index = 0;
		foreach (var row in Indexes)
		{
			row.Insert(column, rows[index]);
			index++;
		}
	}

	/// <summary>
	/// Adds a new row with the given indices
	/// 
	/// If the given list is larger than width, only the starting values are copied until the width is filled. If the list if shorter, the remaining values are filled with 0.
	/// 
	/// Requires at least one pre-existing row to be added (to determine the width). Otherwise nothing happens.
	/// </summary>
	public void AddRow(IEnumerable<int> values)
	{
		InsertRow(
			row: Indexes.Count,
			values: values
		);
	}

	/// <summary>
	/// Inserts a new row filled in with the given indices.
	/// 
	/// If the given list is larger than width, only the starting values are copied until the width is filled. If the list if shorter, the remaining values are filled with 0.
	/// 
	/// Requires at least one pre-existing row to be added (to determine the width). Otherwise nothing happens.
	/// </summary>
	public void InsertRow(int row, IEnumerable<int> values)
	{
		if (Width == 0) return;

		// Prepare the list with the given values, but only up to our expected width.
		var columns = values
			.Take(Math.Min(Width, values.Count()))
			.ToList();

		// If columns are still missing, fill in with 0.
		while (columns.Count < Width) columns.Add(0);

		Indexes.Insert(row, columns);
	}

	#endregion

	#region Changing

	/// <summary>
	/// Remaps all indices accorging to given map where keys are source (original) and values destination (new) indices.
	/// 
	/// Caller can assign 2 optional callbacks:
	/// - rowCallback: called for every new pixel row. Arguments:
	///		- int: row y coordinate
	///	- colourCallback: called for every pixel. Arguments:
	///		- int: x location of the pixel
	///		- int: y location of the pixel
	///		- int: original image index
	///		- int: merged (new) image index (may be the same as original of course)
	/// </summary>
	public void Remap(
		Dictionary<int, int> map,
		Action<int>? rowCallback = null,
		Action<int, int, int, int>? colourCallback = null
	)
	{
		for (var y = 0; y < Height; y++)
		{
			rowCallback?.Invoke(y);

			for (var x = 0; x < Width; x++)
			{
				// Replace original index with mapped. Note this will throw exception if the dictionary doesn't have the original index, however that shouldn't happen in our program. If it does it's an indication of an underlying data issue anyway, so it's better to crash to be able to handle it. We do have a try/catch though but that's only to get more meaningful error message.
				try
				{
					var original = this[x, y];
					var mapped = map[original];
					if (mapped != original)
					{
						this[x, y] = mapped;
					}

					colourCallback?.Invoke(x, y, original, mapped);
				}
				catch (Exception e)
				{
					throw new ArgumentException($"Invalid mapping at ({x},{y}): index {this[x, y]} not found in mapping {map}!", e);
				}
			}
		}
	}

	#endregion
}
