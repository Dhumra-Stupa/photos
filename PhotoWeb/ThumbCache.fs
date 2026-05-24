module ThumbCache

open System
open System.IO
open System.Security.Cryptography
open SkiaSharp

let private thumbDir =
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoIndexer", "thumbs")

// SHA-256 of the source path → stable filename, no path-character issues
let private cachePath (fullPath: string) =
    let hash = Convert.ToHexString(SHA256.HashData(Text.Encoding.UTF8.GetBytes(fullPath)))
    Path.Combine(thumbDir, hash + ".jpg")

// Resize so the image fits within 400×400 px, then save as JPEG quality 85.
// Writes to a .tmp file first, then renames for atomicity.
let private generate (sourcePath: string) (destPath: string) =
    use bmp = SKBitmap.Decode(sourcePath)
    if isNull bmp then failwithf "Cannot decode: %s" sourcePath

    let maxEdge = 400
    let scale = min (float maxEdge / float bmp.Width) (float maxEdge / float bmp.Height)
    let w = max 1 (int (float bmp.Width  * scale))
    let h = max 1 (int (float bmp.Height * scale))

    let sampling = SKSamplingOptions(SKFilterMode.Linear)
    use resized = bmp.Resize(SKImageInfo(w, h), sampling)
    use img     = SKImage.FromBitmap(resized)
    use encoded = img.Encode(SKEncodedImageFormat.Jpeg, 85)

    let tmp = destPath + ".tmp"
    File.WriteAllBytes(tmp, encoded.ToArray())
    File.Move(tmp, destPath, overwrite = true)

/// Returns the path to the cached thumbnail, generating it on first call.
/// Throws if the source file cannot be decoded (caller should fall back to the original).
let getOrCreate (fullPath: string) : string =
    Directory.CreateDirectory(thumbDir) |> ignore
    let dest = cachePath fullPath
    if not (File.Exists dest) then
        generate fullPath dest
    dest
