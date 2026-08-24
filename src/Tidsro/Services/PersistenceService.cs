using System.IO;
using System.Text.Json;
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class PersistenceService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // No polymorphic/$type handling. Default, non-polymorphic contracts only.
    };

    private readonly string _path;
    public PersistenceService(string path) => _path = path;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidsro", "data.json");

    public TidsroData Load()
    {
        try
        {
            if (!File.Exists(_path)) return TidsroData.Defaults();
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<TidsroData>(json, Options);

            // A v1.0 file is a bare AppSettings with no "Settings" key -> Settings stays null. Adopt it,
            // keep the user's prefs, start with no alarms; the next Save writes it forward as TidsroData.
            if (data?.Settings is null)
            {
                var legacy = JsonSerializer.Deserialize<AppSettings>(json, Options);
                data = new TidsroData { Settings = legacy ?? AppSettings.Defaults(), Alarms = new() };
            }

            return data.Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Quarantine();
            return TidsroData.Defaults();   // never fail to launch on a bad file
        }
    }

    public void Save(TidsroData data)
    {
        WriteTo(_path, data);
        ClearQuarantine();   // a good save means any stale .corrupt recovery copy is no longer needed
    }

    /// <summary>The atomic write on its own: create the directory, write a temp file, replace. Export
    /// uses this rather than <see cref="Save"/> — Save also clears the quarantine copy, which is right
    /// for our own data file and quietly destructive against a path the user chose.</summary>
    public static void WriteTo(string path, TidsroData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));   // flushed on close
            if (File.Exists(path)) File.Replace(tmp, path, null);              // atomic, same volume
            else File.Move(tmp, path);
        }
        catch
        {
            // Never leave a half-written temp beside a file the user chose.
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    private void Quarantine()
    {
        try { if (File.Exists(_path)) File.Copy(_path, _path + ".corrupt", overwrite: true); }
        catch { /* quarantine must never throw */ }
    }

    private void ClearQuarantine()
    {
        try { var c = _path + ".corrupt"; if (File.Exists(c)) File.Delete(c); }
        catch { /* best effort; cleanup must never throw */ }
    }
}
