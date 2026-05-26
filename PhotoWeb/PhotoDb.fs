module PhotoDb

open Microsoft.Data.Sqlite
open System
open System.IO

type Photo = {
    FullPath: string
    RelativePath: string
    SizeBytes: int64
    LastModified: string
    DateTaken: string
    CameraModel: string
    LensModel: string
    FNumber: Nullable<float>
    ExposureTimeMs: Nullable<float>
    IsoSpeed: Nullable<int>
    FocalLengthMm: Nullable<float>
}

type DayEntry = { Day: int }

type MonthEntry = {
    Month: int
    MonthName: string
    Days: int array
}

type YearEntry = {
    Year: int
    Months: MonthEntry array
}

let private dbPath =
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoIndexer", "photos.db")

let private openConn () =
    let c = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly")
    c.Open()
    c

let private str (r: SqliteDataReader) i = if r.IsDBNull(i) then null else r.GetString(i)
let private dbl (r: SqliteDataReader) i = if r.IsDBNull(i) then Nullable() else Nullable(r.GetDouble(i))
let private i32 (r: SqliteDataReader) i = if r.IsDBNull(i) then Nullable() else Nullable(r.GetInt32(i))

let private mapPhoto (r: SqliteDataReader) = {
    FullPath      = r.GetString(0)
    RelativePath  = r.GetString(1)
    SizeBytes     = r.GetInt64(2)
    LastModified  = r.GetString(3)
    DateTaken     = str r 4
    CameraModel   = str r 5
    LensModel     = str r 6
    FNumber       = dbl r 7
    ExposureTimeMs= dbl r 8
    IsoSpeed      = i32 r 9
    FocalLengthMm = dbl r 10
}

let private buildConds (date: string option) (camera: string option) (lens: string option) (focalLength: int option) =
    [
        if date.IsSome        then "date(date_taken) = date($date)"
        if camera.IsSome      then "camera_model = $camera"
        if lens.IsSome        then "lens_model = $lens"
        if focalLength.IsSome then "CAST(ROUND(focal_length_mm) AS INTEGER) = $focalLength"
    ]

let private bindParams (cmd: SqliteCommand) (date: string option) (camera: string option) (lens: string option) (focalLength: int option) =
    date        |> Option.iter (fun d -> cmd.Parameters.AddWithValue("$date",        d) |> ignore)
    camera      |> Option.iter (fun c -> cmd.Parameters.AddWithValue("$camera",      c) |> ignore)
    lens        |> Option.iter (fun l -> cmd.Parameters.AddWithValue("$lens",        l) |> ignore)
    focalLength |> Option.iter (fun f -> cmd.Parameters.AddWithValue("$focalLength", f) |> ignore)

let getPhotos (date: string option) (camera: string option) (lens: string option) (focalLength: int option) (offset: int) (limit: int) =
    use c = openConn()
    use cmd = c.CreateCommand()
    let conds = buildConds date camera lens focalLength
    let where = if conds.IsEmpty then "" else "WHERE " + String.concat " AND " conds
    cmd.CommandText <- $"""
        SELECT full_path, relative_path, size_bytes, last_modified,
               date_taken, camera_model, lens_model, f_number, exposure_time_ms, iso_speed, focal_length_mm
        FROM photos {where}
        ORDER BY date_taken DESC NULLS LAST, full_path
        LIMIT $limit OFFSET $offset
        """
    bindParams cmd date camera lens focalLength
    cmd.Parameters.AddWithValue("$limit",  limit)  |> ignore
    cmd.Parameters.AddWithValue("$offset", offset) |> ignore
    use r = cmd.ExecuteReader()
    [| while r.Read() do yield mapPhoto r |]

let getPhotosCount (date: string option) (camera: string option) (lens: string option) (focalLength: int option) =
    use c = openConn()
    use cmd = c.CreateCommand()
    let conds = buildConds date camera lens focalLength
    let where = if conds.IsEmpty then "" else "WHERE " + String.concat " AND " conds
    cmd.CommandText <- $"SELECT COUNT(*) FROM photos {where}"
    bindParams cmd date camera lens focalLength
    cmd.ExecuteScalar() :?> int64 |> int

let getFocalLengths (date: string option) (camera: string option) (lens: string option) =
    use c = openConn()
    use cmd = c.CreateCommand()
    let conds = [
        "focal_length_mm IS NOT NULL"
        if date.IsSome   then "date(date_taken) = date($date)"
        if camera.IsSome then "camera_model = $camera"
        if lens.IsSome   then "lens_model = $lens"
    ]
    cmd.CommandText <- $"""
        SELECT DISTINCT CAST(ROUND(focal_length_mm) AS INTEGER)
        FROM photos
        WHERE {String.concat " AND " conds}
        ORDER BY 1
        """
    date   |> Option.iter (fun d -> cmd.Parameters.AddWithValue("$date",   d) |> ignore)
    camera |> Option.iter (fun c -> cmd.Parameters.AddWithValue("$camera", c) |> ignore)
    lens   |> Option.iter (fun l -> cmd.Parameters.AddWithValue("$lens",   l) |> ignore)
    use r = cmd.ExecuteReader()
    [| while r.Read() do yield r.GetInt32(0) |]

let getDates () =
    use c = openConn()
    use cmd = c.CreateCommand()
    cmd.CommandText <- """
        SELECT DISTINCT date(date_taken)
        FROM photos
        WHERE date_taken IS NOT NULL
        ORDER BY date(date_taken) DESC
        """
    use r = cmd.ExecuteReader()
    [| while r.Read() do if not (r.IsDBNull(0)) then yield r.GetString(0) |]

let getCameraModels () =
    use c = openConn()
    use cmd = c.CreateCommand()
    cmd.CommandText <- """
        SELECT DISTINCT camera_model
        FROM photos
        WHERE camera_model IS NOT NULL AND camera_model <> ''
        ORDER BY camera_model
        """
    use r = cmd.ExecuteReader()
    [| while r.Read() do yield r.GetString(0) |]

let getLenses (camera: string option) =
    use c = openConn()
    use cmd = c.CreateCommand()
    let conds = [
        "lens_model IS NOT NULL AND lens_model <> ''"
        if camera.IsSome then "camera_model = $camera"
    ]
    cmd.CommandText <- $"""
        SELECT DISTINCT lens_model
        FROM photos
        WHERE {String.concat " AND " conds}
        ORDER BY lens_model
        """
    camera |> Option.iter (fun c -> cmd.Parameters.AddWithValue("$camera", c) |> ignore)
    use r = cmd.ExecuteReader()
    [| while r.Read() do yield r.GetString(0) |]

let pathExists (path: string) =
    use c = openConn()
    use cmd = c.CreateCommand()
    cmd.CommandText <- "SELECT COUNT(1) FROM photos WHERE full_path = $path"
    cmd.Parameters.AddWithValue("$path", path) |> ignore
    (cmd.ExecuteScalar() :?> int64) > 0L
