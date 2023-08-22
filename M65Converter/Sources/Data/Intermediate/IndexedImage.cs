using M65Converter.Sources.Helpers.Utils;

using System.Text;

namespace M65Converter.Sources.Data.Intermediate;

/// <summary>
/// Image specified as indexes.
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
	/// Gets the width of the image as number of indexes.
	/// </summary>
	public int Width
	{
		get => Indexes.Count > 0 ? Indexes[0].Count : 0;
	}

	/// <summary>
	/// Gets the height of the image as number of indexes.
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

	#region Changing

	/// <summary>
	/// Remaps all indexes accorgind to given map where keys are source (original) and values destination (new) indexes.
	/// </summary>
	public void Remap(Dictionary<int, int> map, int itemIndex)
	{
		var isItemChangeLogged = false; // used to only log item if it has new colours
		var formatter = Logger.Verbose.IsEnabled ? new ChangesTableFormatter() : null;

		var changeDescriptions = new List<StringBuilder>();
		var headerBuilder = Logger.Verbose.IsEnabled ? new StringBuilder() : null;
		var headerLineBuilder = headerBuilder != null ? new StringBuilder() : null;

		for (var y = 0; y < Height;	y++)
		{
			formatter?.StartNewLine();

			for (var x = 0; x < Width; x++)
			{
				// Replace original index with mapped. Note this will throw exception if the dictionary doesn't have the original index, however that shouldn't happen in our program. If it does it's an indication of an underlying data issue anyway, so it's better to crash to be able to handle it. We do have a try/catch though but that's only to get more meaningful error message.
				try
				{
					var original = this[x, y];
					var mapped = map[original];
					if (mapped != original)
					{
						isItemChangeLogged = true;
						formatter?.AppendChange(original, mapped);
						this[x, y] = mapped;
					}
					else
					{
						formatter?.AppendNoChange(original);
					}
				}
				catch (Exception e)
				{
					throw new ArgumentException($"Invalid mapping at ({x},{y}): original {this[x, y]} doesn't have corresponding mapping!", e);
				}
			}
		}

		if (isItemChangeLogged && formatter != null)
		{
			Logger.Verbose.Separator();
			Logger.Verbose.Message($"Remapping colour indexes for char {itemIndex}");
			formatter.ExportLines(Logger.Verbose.Option);
		}
	}

	#endregion
}
