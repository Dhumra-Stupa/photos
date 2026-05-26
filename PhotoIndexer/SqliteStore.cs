using Microsoft.Data.Sqlite;

namespace PhotoIndexer;

public static class SqliteStore
{
    public static string DbPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhotoIndexer",
            "photos.db");

    public static void Save(FolderIndex folderIndex)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        using var conn = Open();
        EnsureSchema(conn);

        using var tx = conn.BeginTransaction();

        using var del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM photos WHERE root_folder = $root";
        del.Parameters.AddWithValue("$root", folderIndex.RootFolder);
        del.ExecuteNonQuery();

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT OR REPLACE INTO photos
                (full_path, relative_path, root_folder, size_bytes, last_modified,
                 date_taken, camera_model, lens_model, f_number, exposure_time_ms, iso_speed, focal_length_mm)
            VALUES
                ($full_path, $relative_path, $root_folder, $size_bytes, $last_modified,
                 $date_taken, $camera_model, $lens_model, $f_number, $exposure_time_ms, $iso_speed, $focal_length_mm)
            """;

        foreach (var p in folderIndex.Files)
        {
            ins.Parameters.Clear();
            ins.Parameters.AddWithValue("$full_path", p.FullPath);
            ins.Parameters.AddWithValue("$relative_path", p.RelativePath);
            ins.Parameters.AddWithValue("$root_folder", folderIndex.RootFolder);
            ins.Parameters.AddWithValue("$size_bytes", p.SizeBytes);
            ins.Parameters.AddWithValue("$last_modified", p.LastModified.ToString("yyyy-MM-ddTHH:mm:ss"));
            ins.Parameters.AddWithValue("$date_taken", (object?)p.DateTaken?.ToString("yyyy-MM-ddTHH:mm:ss") ?? DBNull.Value);
            ins.Parameters.AddWithValue("$camera_model", (object?)p.CameraModel ?? DBNull.Value);
            ins.Parameters.AddWithValue("$lens_model", (object?)p.LensModel ?? DBNull.Value);
            ins.Parameters.AddWithValue("$f_number", (object?)p.FNumber ?? DBNull.Value);
            ins.Parameters.AddWithValue("$exposure_time_ms", (object?)p.ExposureTimeMs ?? DBNull.Value);
            ins.Parameters.AddWithValue("$iso_speed", (object?)p.IsoSpeed ?? DBNull.Value);
            ins.Parameters.AddWithValue("$focal_length_mm", (object?)p.FocalLengthMm ?? DBNull.Value);
            ins.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void EnsureSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS photos (
                full_path        TEXT PRIMARY KEY NOT NULL,
                relative_path    TEXT NOT NULL,
                root_folder      TEXT NOT NULL,
                size_bytes       INTEGER NOT NULL,
                last_modified    TEXT NOT NULL,
                date_taken       TEXT,
                camera_model     TEXT,
                lens_model       TEXT,
                f_number         REAL,
                exposure_time_ms REAL,
                iso_speed        INTEGER,
                focal_length_mm  REAL
            );
            CREATE INDEX IF NOT EXISTS idx_date ON photos(date_taken);
            CREATE INDEX IF NOT EXISTS idx_lens ON photos(lens_model);
            CREATE INDEX IF NOT EXISTS idx_root ON photos(root_folder);
            """;
        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }
}
