using System.Text.Json.Serialization;

namespace PhotoIndexer;

public sealed class PhotoEntry
{
    public string FullPath { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime? DateTaken { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public double? FNumber { get; set; }
    public double? ExposureTimeMs { get; set; }
    public int? IsoSpeed { get; set; }
    public double? FocalLengthMm { get; set; }
}

public sealed class FolderIndex
{
    public string RootFolder { get; set; } = "";
    public DateTime IndexedAt { get; set; }
    public List<PhotoEntry> Files { get; set; } = [];
}

public sealed class IndexStore
{
    [JsonPropertyName("indexes")]
    public Dictionary<string, FolderIndex> Indexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PhotoEntry? FindEntry(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);
        foreach (var idx in Indexes.Values)
            foreach (var entry in idx.Files)
                if (string.Equals(entry.FullPath, normalized, StringComparison.OrdinalIgnoreCase))
                    return entry;
        return null;
    }
}
