namespace M65Converter.Sources.Data.Intermediate.Helpers;

public enum CharColourMode
{
	/// <summary>
	/// Full colour mode.
	/// </summary>
	FCM,

	/// <summary>
	/// Nibble colour mode
	/// </summary>
	NCM
}

public enum TransparentImageInsertion
{
	/// <summary>
	/// No transparent image is inserted.
	/// </summary>
	None,

	/// <summary>
	/// Transparent image is inserted before (what exactly "before" means depends on context where used). Note: transparent image is only inserted if one not present yet.
	/// </summary>
	Before,

	/// <summary>
	/// Transparent image is inserted after (what exactly "after" means depends on context where used). Note: transparent image is only inserted if one not present yet.
	/// </summary>
	After,

	/// <summary>
	/// Transparent image is inserted before and after (the exact meaning of "before" and "after" depends on context where used). Note: either transparent image is only inserted if one not present yet.
	/// </summary>
	BeforeAndAfter
}

public enum TransparentImageRule
{
	/// <summary>
	/// Reuses the first transparent image if available (that's the <see cref="TransparentImage"/>.
	/// </summary>
	ReuseFirst,

	/// <summary>
	/// If last image already in the list is transparent it's reused, otherwise new one is created.
	/// </summary>
	ReusePrevious,

	/// <summary>
	/// Always adds new transparent image regardless of whether one already exists - aka force adds.
	/// </summary>
	AlwaysAdd
}

public enum TransparencyOptions
{
	/// <summary>
	/// Removes all fully transparent items.
	/// </summary>
	OpaqueOnly,

	/// <summary>
	/// Keeps all transparent items.
	/// </summary>
	KeepAll,
}

public enum DuplicatesOptions
{
	/// <summary>
	/// Only keep unique items.
	/// </summary>
	UniqueOnly,

	/// <summary>
	/// Treat duplicated items as unique.
	/// </summary>
	KeepAll
}

public enum ParsingOrder
{
	/// <summary>
	/// Parses columns row by row, starting at top towards the bottom.
	/// </summary>
	RowByRow,

	/// <summary>
	/// Parses rows column by column, starting at left towards the right.
	/// </summary>
	ColumnByColumn
}
