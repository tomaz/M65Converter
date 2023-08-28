using M65Converter.Sources.Data.Intermediate;
using M65Converter.Sources.Helpers.Utils;
using System.Text.Json;

namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Describes level.
/// </summary>
public class LevelData
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
	/// The list of all layers.
	/// </summary>
	public List<LayerData> Layers { get; set; } = new();

	#region Initialization & Disposal

	/// <summary>
	/// Parses LDtk data. Input path can etiher be:
	/// 
	/// - folder: `data.json` file is taken from the folder)
	/// - JSON file: it's expected to be simplified export JSON data
	/// 
	/// Either way, the method creates new <see cref="LevelData"/> instance describing parsed data.
	/// </summary>
	public static LevelData ParseLDtk(FileInfo input)
	{
		// If path is directory search search for `data.json` file in it. Otherwise assume it's `data.json` file (name doesn't matter in this case, as long as the data is in correct format).
		var dataJsonPath = input.FullName;
		var attributes = File.GetAttributes(dataJsonPath);
		if ((attributes & FileAttributes.Directory) != 0)
		{
			dataJsonPath = Path.Combine(dataJsonPath, "data.json");
		}

		// Load the data JSON file.
		Logger.Verbose.Message($"Parsing {Path.GetFileName(dataJsonPath)}");
		var json = new StreamReader(File.OpenRead(dataJsonPath)).ReadToEnd();
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
		var inputPath = Path.GetDirectoryName(dataJsonPath)!;
		var layers = data.Layers.Select(filename =>
		{
			Logger.Verbose.Option($"{filename}");

			var path = Path.Combine(inputPath, filename);
			var image = Image.Load<Argb32>(path);

			return new LayerData
			{
				Path = path,
				Image = image
			};
		});

		// Get additional data.
		var rootAndLevelName = LevelRootFolder(dataJsonPath);

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
