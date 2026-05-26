module Program

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open System.Text.Json
open System.Threading.Tasks

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.ConfigureHttpJsonOptions(fun o ->
        o.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase) |> ignore

    builder.Services.AddCors(fun o ->
        o.AddDefaultPolicy(fun p ->
            p.WithOrigins("http://localhost:5173")
             .AllowAnyHeader()
             .AllowAnyMethod() |> ignore)) |> ignore

    let app = builder.Build()
    app.UseCors() |> ignore

    // GET /api/photos?date=...&camera=...&lens=...&focalLength=...&offset=0&limit=60
    app.MapGet("/api/photos", Func<string, string, string, Nullable<int>, Nullable<int>, Nullable<int>, IResult>(fun date camera lens focalLength offset limit ->
        let dateOpt        = Option.ofObj date   |> Option.filter (fun d -> d <> "all")
        let cameraOpt      = Option.ofObj camera
        let lensOpt        = Option.ofObj lens
        let focalLengthOpt = if focalLength.HasValue then Some focalLength.Value else None
        let off = if offset.HasValue then offset.Value else 0
        let lim = if limit.HasValue  then limit.Value  else 1_000_000
        Results.Ok(PhotoDb.getPhotos dateOpt cameraOpt lensOpt focalLengthOpt off lim))) |> ignore

    // GET /api/photos/count?date=...&camera=...&lens=...&focalLength=...
    app.MapGet("/api/photos/count", Func<string, string, string, Nullable<int>, IResult>(fun date camera lens focalLength ->
        let dateOpt        = Option.ofObj date   |> Option.filter (fun d -> d <> "all")
        let cameraOpt      = Option.ofObj camera
        let lensOpt        = Option.ofObj lens
        let focalLengthOpt = if focalLength.HasValue then Some focalLength.Value else None
        Results.Ok(PhotoDb.getPhotosCount dateOpt cameraOpt lensOpt focalLengthOpt))) |> ignore

    // GET /api/focal-lengths?date=...&camera=...&lens=...
    app.MapGet("/api/focal-lengths", Func<string, string, string, IResult>(fun date camera lens ->
        let dateOpt   = Option.ofObj date   |> Option.filter (fun d -> d <> "all")
        let cameraOpt = Option.ofObj camera
        let lensOpt   = Option.ofObj lens
        Results.Ok(PhotoDb.getFocalLengths dateOpt cameraOpt lensOpt))) |> ignore

    // GET /api/dates  →  year/month/day tree, descending
    app.MapGet("/api/dates", Func<IResult>(fun () ->
        let tree =
            PhotoDb.getDates()
            |> Array.choose (fun d ->
                match d.Split('-') with
                | [| y; m; day |] ->
                    match Int32.TryParse y, Int32.TryParse m, Int32.TryParse day with
                    | (true, yi), (true, mi), (true, di) -> Some(yi, mi, di)
                    | _ -> None
                | _ -> None)
            |> Array.groupBy (fun (y, _, _) -> y)
            |> Array.map (fun (yr, entries) ->
                let months =
                    entries
                    |> Array.groupBy (fun (_, m, _) -> m)
                    |> Array.map (fun (mo, mes) ->
                        {| month = mo
                           monthName = Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(mo)
                           days = mes |> Array.map (fun (_, _, d) -> d) |> Array.sortDescending |})
                    |> Array.sortByDescending (fun m -> m.month)
                {| year = yr; months = months |})
            |> Array.sortByDescending (fun y -> y.year)
        Results.Ok(tree))) |> ignore

    // GET /api/cameras
    app.MapGet("/api/cameras", Func<IResult>(fun () ->
        Results.Ok(PhotoDb.getCameraModels()))) |> ignore

    // GET /api/lenses?camera=...
    app.MapGet("/api/lenses", Func<string, IResult>(fun camera ->
        let cameraOpt = Option.ofObj camera
        Results.Ok(PhotoDb.getLenses cameraOpt))) |> ignore

    // GET /api/image?path=<full-path>
    // Verifies path is in the DB before serving to prevent arbitrary file access.
    app.MapGet("/api/image", Func<string, Task<IResult>>(fun path -> task {
        if isNull path then
            return Results.BadRequest("path query parameter required")
        elif not (PhotoDb.pathExists path) then
            return Results.NotFound()
        elif not (File.Exists path) then
            return Results.NotFound()
        else
            return Results.File(path, "image/jpeg")
    })) |> ignore

    // GET /api/thumb?path=<full-path>
    // Serves a cached low-resolution thumbnail (≤400px on the long edge).
    // Generates and caches on first request; falls back to the original on error.
    app.MapGet("/api/thumb", Func<string, Task<IResult>>(fun path -> task {
        if isNull path then
            return Results.BadRequest("path query parameter required")
        elif not (PhotoDb.pathExists path) then
            return Results.NotFound()
        elif not (File.Exists path) then
            return Results.NotFound()
        else
            try
                let thumbPath = ThumbCache.getOrCreate path
                return Results.File(thumbPath, "image/jpeg")
            with _ ->
                return Results.File(path, "image/jpeg")
    })) |> ignore

    app.Run()
    0
