using PhotoIndexer;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "index":
        return RunIndex(args);
    case "list":
        return RunList(args);
    case "show":
        return RunShow(args);
    case "find":
        return RunFind(args);
    case "dump":
        return RunDump(args);
    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        PrintUsage();
        return 1;
}

static int RunIndex(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: PhotoIndexer index <folder>");
        return 1;
    }

    var folder = args[1];
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"Folder not found: {folder}");
        return 1;
    }

    Console.WriteLine($"Scanning: {folder}");
    var folderIndex = Scanner.Scan(folder);
    Console.WriteLine($"Found {folderIndex.Files.Count} JPEG file(s).");

    var store = Store.Load();
    store.Indexes[folderIndex.RootFolder] = folderIndex;
    Store.Save(store);
    SqliteStore.Save(folderIndex);

    return 0;
}

static int RunList(string[] args)
{
    var store = Store.Load();

    if (store.Indexes.Count == 0)
    {
        Console.WriteLine("No folders indexed yet. Run: PhotoIndexer index <folder>");
        return 0;
    }

    var files = store.Indexes.Values
        .SelectMany(idx => idx.Files)
        .OrderBy(f => f.DateTaken ?? f.LastModified)
        .ToList();

    if (files.Count == 0)
    {
        Console.WriteLine("No files in index.");
        return 0;
    }

    Console.WriteLine("FullPath,DateTaken,SizeBytes,LensModel,FNumber,ExposureTimeMs,IsoSpeed,FocalLengthMm");
    foreach (var f in files)
    {
        Console.WriteLine(string.Join(",",
            CsvField(f.FullPath),
            CsvField(f.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
            f.SizeBytes,
            CsvField(f.LensModel ?? ""),
            f.FNumber.HasValue ? f.FNumber.Value.ToString("F1") : "",
            f.ExposureTimeMs.HasValue ? f.ExposureTimeMs.Value.ToString("F3") : "",
            f.IsoSpeed.HasValue ? f.IsoSpeed.Value.ToString() : "",
            f.FocalLengthMm.HasValue ? f.FocalLengthMm.Value.ToString("F1") : ""));
        Console.WriteLine();
    }

    return 0;
}

static string CsvField(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        return $"\"{value.Replace("\"", "\"\"")}\"";
    return value;
}

static string FormatSize(long bytes)
{
    if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
    if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
    if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
    return $"{bytes} B";
}

static int RunShow(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: PhotoIndexer show <folder>");
        return 1;
    }

    var folder = Path.GetFullPath(args[1]);
    var store = Store.Load();

    if (!store.Indexes.TryGetValue(folder, out var idx))
    {
        Console.Error.WriteLine($"No index found for: {folder}");
        Console.Error.WriteLine("Run: PhotoIndexer index <folder>");
        return 1;
    }

    Console.WriteLine($"Folder : {idx.RootFolder}");
    Console.WriteLine($"Indexed: {idx.IndexedAt:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"Files  : {idx.Files.Count}");
    Console.WriteLine();

    foreach (var f in idx.Files.OrderBy(f => f.RelativePath))
        Console.WriteLine($"  {f.RelativePath,-60}  {f.SizeBytes,10:N0} bytes  {f.LastModified:yyyy-MM-dd}");

    return 0;
}

static int RunDump(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: PhotoIndexer dump <file>");
        return 1;
    }

    var dirs = MetadataExtractor.ImageMetadataReader.ReadMetadata(args[1]);
    foreach (var dir in dirs)
    {
        Console.WriteLine($"=== {dir.Name} ===");
        foreach (var tag in dir.Tags)
            Console.WriteLine($"  [{tag.Type:X4}] {tag.Name} = {tag.Description}");
    }
    return 0;
}

static int RunFind(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: PhotoIndexer find <file>");
        return 1;
    }

    var store = Store.Load();
    var entry = store.FindEntry(args[1]);

    if (entry is null)
    {
        Console.Error.WriteLine($"No index entry found for: {args[1]}");
        return 1;
    }

    Console.WriteLine($"FullPath    : {entry.FullPath}");
    Console.WriteLine($"RelativePath: {entry.RelativePath}");
    Console.WriteLine($"Size        : {FormatSize(entry.SizeBytes)}");
    Console.WriteLine($"LastModified: {entry.LastModified:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"DateTaken   : {(entry.DateTaken.HasValue ? entry.DateTaken.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-")}");
    Console.WriteLine($"LensModel   : {entry.LensModel ?? "-"}");
    Console.WriteLine($"F-Stop      : {(entry.FNumber.HasValue ? $"f/{entry.FNumber.Value:F1}" : "-")}");
    Console.WriteLine($"Exposure    : {(entry.ExposureTimeMs.HasValue ? $"{entry.ExposureTimeMs.Value:F3} ms" : "-")}");
    Console.WriteLine($"ISO         : {(entry.IsoSpeed.HasValue ? entry.IsoSpeed.Value.ToString() : "-")}");
    Console.WriteLine($"FocalLength : {(entry.FocalLengthMm.HasValue ? $"{entry.FocalLengthMm.Value:F1} mm" : "-")}");

    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("PhotoIndexer — JPEG file indexer");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  index <folder>   Scan folder recursively and update the index");
    Console.WriteLine("  list             List all indexed files with date taken, size, and lens");
    Console.WriteLine("  show  <folder>   Show all indexed files for a folder");
    Console.WriteLine("  find  <file>     Look up a specific file in the index");
    Console.WriteLine();
    Console.WriteLine($"Index file: {Store.GetIndexPath()}");
}
