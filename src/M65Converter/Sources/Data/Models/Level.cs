﻿using M65Converter.Sources.Data.Intermediate.Images;
using M65Converter.Sources.Data.Parsing;
using M65Converter.Sources.Data.Providers;

namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Describes source level in a generic, source type independent way.
/// </summary>
public class Level
{
	/// <summary>
	/// Width of the level in pixels.
	/// </summary>
	public int Width { get; set; }

	/// <summary>
	/// Height of the level in pixels.
	/// </summary>
	public int Height { get; set; }

	/// <summary>
	/// Name of the level.
	/// </summary>
	public string LevelName { get; set; } = null!;

	/// <summary>
	/// Level root folder (where the level source file is located).
	/// </summary>
	public string RootFolder { get; set; } = null!;

	/// <summary>
	/// Optional composite layer. Only available with some types of sources. If available, it should be used instead of manually merging the layers as it's more accurate representation of the source.
	/// </summary>
	public LayerData? CompositeLayer { get; set; }

	/// <summary>
	/// The list of all layers.
	/// </summary>
	public List<LayerData> Layers { get; set; } = new();

	#region Initialization & Disposal

	/// <summary>
	/// Parses input. Input can etiher be:
	/// 
	/// - folder: `data.json` file is taken from the folder)
	/// - JSON file: it's expected to be simplified export JSON data
	/// - Aseprite file
	/// 
	/// Either way, the method creates new <see cref="Level"/> instance describing parsed data.
	/// </summary>
	public static Level Parse(IStreamProvider input)
	{
		var provider = input;

		// If path is a directory, we assume it's LDtk simplified export so we prepare for `data.json` file in it.
		// Note: don't use folder memory streams, this code assumes the paths are valid.
		if (input.IsFolder())
		{
			var path = Path.Combine(input.GetFilename().ToLower(), "data.json");
			provider = new FileStreamProvider
			{
				FileInfo = new FileInfo(path)
			};
		}

		// If path points to a `data.json`, use LDtk parser.
		if (Path.GetFileName(provider.GetFilename()).ToLower() == "data.json")
		{
			return new LDtkSimplifiedExportParser().Parse(provider);
		}

		// If path points to Aseprite file, use Aseprite parser.
		var extension = Path.GetExtension(provider.GetFilename().ToLower());
		if (extension == ".ase" || extension == ".aseprite")
		{
			return new AsepriteLevelParser().Parse(provider);
		}

		// Otherwise we don't know how to parse so throw exception.
		throw new InvalidDataException($"Unknown input type {provider.GetFilename()}");
	}

	#endregion

	#region Declarations

	/// <summary>
	/// Describes individual layer.
	/// </summary>
	public class LayerData
	{
		/// <summary>
		/// Path to layer bitmap.
		/// </summary>
		public string Path { get; set; } = null!;

		/// <summary>
		/// Name of this layer - depending the source, could be the file name of the source image for example.
		/// </summary>
		public string Name { get; set; } = null!;

		/// <summary>
		/// Layer bitmap image.
		/// </summary>
		public Image<Argb32> Image { get; set; } = null!;

		/// <summary>
		/// Indexed image, representing indices to characters in chars container. Assigned from outside class, only available after parsing completes.
		/// </summary>
		public IndexedImage IndexedImage { get; set; } = null!;
	}

	#endregion
}
