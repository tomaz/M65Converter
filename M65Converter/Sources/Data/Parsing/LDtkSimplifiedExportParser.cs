using M65Converter.Sources.Data.Models;
using M65Converter.Sources.Helpers.Utils;

using static M65Converter.Sources.Data.Models.LevelData;

using System.Text.Json;

namespace M65Converter.Sources.Data.Parsing;

/// <summary>
/// Parses <see cref="LevelData"/> from LDtk simplified export source.
/// </summary>
public class LDtkSimplifiedExportParser
{
	#region Parsing

	/// <summary>
	/// Parses data from `data.json` file.
	/// </summary>
	public LevelData Parse(string path)
	{
		// Load the data JSON file.
		Logger.Verbose.Message($"Parsing {Path.GetFileName(path)}");
		var json = new StreamReader(File.OpenRead(path)).ReadToEnd();
		var data = JsonSerializer.Deserialize<LDtkJsonData>(json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});

		// If data is invalid, throw exception and bail out.
		if (data == null)
		{
			throw new InvalidDataException("Failed loading data.json");
		}

		// Load all layer images.
		Logger.Verbose.Message("Loading layers");
		var inputPath = Path.GetDirectoryName(path)!;
		var layers = data.Layers.Select(filename =>
		{
			Logger.Verbose.Option($"{filename}");

			var path = Path.Combine(inputPath, filename);
			var image = Image.Load<Argb32>(path);

			return new LayerData
			{
				Path = path,
				Name = Path.GetFileNameWithoutExtension(filename),
				Image = image
			};
		});

		// Get additional data.
		var rootAndLevelName = LevelRootFolder(path);

		// Prepare result.
		return new LevelData
		{
			Width = data.Width,
			Height = data.Height,
			LevelName = rootAndLevelName.Item2,
			RootFolder = rootAndLevelName.Item1,
			Layers = layers.ToList() ?? new()
		};
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Returns a tuple with:
	/// 
	/// - full path to level root folder (where main LDtk json file exists)
	/// - name of the level
	/// </summary>
	private static Tuple<string, string> LevelRootFolder(string dataJsonFilename)
	{
		// Get the root folder of the level. Simplified export saves each level into its own folder with data.json file inside "{levelName}/simplified/AutoLayer" subfolder. So we remove 3 folders from layer image file to get to the root where LDtk source file is contained. Note how we dynamically swap source and root folder so that we still can get a valid result if `GetDirectoryName` returns null (aka folder is root).
		var levelFolder = Path.GetDirectoryName(dataJsonFilename)!;
		var rootFolder = levelFolder;
		for (int i = 0; i < 3; i++)
		{
			levelFolder = rootFolder;
			rootFolder = Path.GetDirectoryName(rootFolder);
			if (rootFolder == null)
			{
				rootFolder = levelFolder;
				break;
			}
		}

		var levelName = new DirectoryInfo(levelFolder!).Name;
		return new Tuple<string, string>(rootFolder, levelName);
	}

	#endregion

	#region Declarations

	private class LDtkJsonData
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public string[] Layers { get; set; } = null!;
	}

	#endregion
}
