using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoIndexer;

public static class Store
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string IndexPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhotoIndexer",
            "index.json");

    public static IndexStore Load()
    {
        var path = IndexPath;
        if (!File.Exists(path))
            return new IndexStore();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<IndexStore>(json, JsonOptions) ?? new IndexStore();
        }
        catch
        {
            Console.Error.WriteLine($"Warning: could not read existing index at {path}, starting fresh.");
            return new IndexStore();
        }
    }

    public static void Save(IndexStore store)
    {
        var path = IndexPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(store, JsonOptions));
        Console.WriteLine($"Index saved to: {path}");
    }

    public static string GetIndexPath() => IndexPath;
}
