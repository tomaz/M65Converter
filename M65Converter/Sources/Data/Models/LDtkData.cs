using M65Converter.Sources.Helpers.Utils;
using System.Text.Json;

namespace M65Converter.Sources.Data.Models;

/// <summary>
/// Describes various LDtk related data.
/// </summary>
public class LDtkData
{
	public int Width { get; set; }
	public int Height { get; set; }
	public List<LayerData> Layers { get; set; } = new();

	#region Parsing

	public static LDtkData Parse(FileInfo inputFolder)
	{
		var inputPath = inputFolder.FullName;
		var dataFilename = "data.json";

		// If path is not directory, throw exception.
		var attributes = File.GetAttributes(inputPath);
		if ((attributes & FileAttributes.Directory) == 0)
		{
			throw new InvalidDataException("Input path is not directory");
		}

		// Load the data JSON file.
		Logger.Verbose.Message($"Parsing {dataFilename}");
		var json = new StreamReader(File.OpenRead(Path.Combine(inputPath, dataFilename))).ReadToEnd();
		var data = JsonSerializer.Deserialize<LDtkJsonData>(json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		})
			?? throw new InvalidDataException("Failed loading data.json");

		// Load all layer images.
		Logger.Verbose.Message("Loading layers");
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

		// Prepare result.
		return new LDtkData
		{
			Width = data.Width,
			Height = data.Height,
			Layers = layers.ToList() ?? new()
		};
	}

	#endregion

	#region Declarations

	public class LayerData
	{
		public string Path { get; set; } = null!;
		public Image<Argb32> Image { get; set; } = null!;
	}

	private class LDtkJsonData
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public string[] Layers { get; set; } = null!;
	}

	#endregion
}
