namespace M65Converter.Sources.Data.Intermediate;

public enum TransparencyOptionsType
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

public enum DuplicatesOptionsType
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
