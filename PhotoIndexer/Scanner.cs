using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PhotoIndexer;

public static class Scanner
{
    private static readonly HashSet<string> JpegExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" };

    public static FolderIndex Scan(string rootFolder)
    {
        var root = Path.GetFullPath(rootFolder);

        var index = new FolderIndex
        {
            RootFolder = root,
            IndexedAt = DateTime.UtcNow,
        };

        foreach (var file in System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (!JpegExtensions.Contains(Path.GetExtension(file)))
                continue;

            var info = new FileInfo(file);
            var (dateTaken, cameraModel, lensModel, fNumber, exposureTimeMs, isoSpeed, focalLengthMm) = ReadExif(file);

            index.Files.Add(new PhotoEntry
            {
                FullPath = file,
                RelativePath = Path.GetRelativePath(root, file),
                SizeBytes = info.Length,
                LastModified = info.LastWriteTimeUtc,
                DateTaken = dateTaken,
                CameraModel = cameraModel,
                LensModel = lensModel,
                FNumber = fNumber,
                ExposureTimeMs = exposureTimeMs,
                IsoSpeed = isoSpeed,
                FocalLengthMm = focalLengthMm,
            });
        }

        return index;
    }

    private static (DateTime? dateTaken, string? cameraModel, string? lensModel, double? fNumber, double? exposureTimeMs, int? isoSpeed, double? focalLengthMm) ReadExif(string file)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(file);

            var exifIfd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            var exifSub  = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            DateTime? dateTaken = null;
            if (exifSub is not null && exifSub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                dateTaken = dt;

            string? cameraModel = NullIfEmpty(exifIfd0?.GetDescription(ExifDirectoryBase.TagModel));

            // TagLensModel (0xA434) is often empty for third-party lenses; fall back to
            // maker-note "Lens Type" (Canon) or "Lens Specification" (focal-range string).
            string? lensModel =
                NullIfEmpty(exifSub?.GetDescription(ExifDirectoryBase.TagLensModel))
                ?? NullIfEmpty(FindTag(dirs, "Lens Type"))
                ?? NullIfEmpty(FindTag(dirs, "Lens Specification"));

            double? fNumber = null;
            if (exifSub is not null && exifSub.TryGetRational(ExifDirectoryBase.TagFNumber, out var fnRat) && fnRat.Denominator != 0)
                fNumber = (double)fnRat.Numerator / fnRat.Denominator;

            double? exposureTimeMs = null;
            if (exifSub is not null && exifSub.TryGetRational(ExifDirectoryBase.TagExposureTime, out var expRat) && expRat.Denominator != 0)
                exposureTimeMs = (double)expRat.Numerator / expRat.Denominator * 1000.0;

            int? isoSpeed = null;
            if (exifSub is not null && exifSub.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso))
                isoSpeed = iso;

            double? focalLengthMm = null;
            if (exifSub is not null && exifSub.TryGetRational(ExifDirectoryBase.TagFocalLength, out var flRat) && flRat.Denominator != 0)
                focalLengthMm = (double)flRat.Numerator / flRat.Denominator;

            return (dateTaken, cameraModel, lensModel, fNumber, exposureTimeMs, isoSpeed, focalLengthMm);
        }
        catch
        {
            return (null, null, null, null, null, null, null);
        }
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s)
        || s.Equals("N/A", StringComparison.OrdinalIgnoreCase)
        || s.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
            ? null : s;

    private static string? FindTag(IEnumerable<MetadataExtractor.Directory> dirs, string name) =>
        dirs.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == name)?.Description;
}
