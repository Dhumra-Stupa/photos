using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

var file = @"D:\backup\pictures\2019-04-03\IMG_0002.JPG";
var dirs = ImageMetadataReader.ReadMetadata(file);
foreach (var dir in dirs)
{
    Console.WriteLine($"=== {dir.Name} ===");
    foreach (var tag in dir.Tags)
        Console.WriteLine($"  [{tag.Type:X4}] {tag.Name} = {tag.Description}");
}
