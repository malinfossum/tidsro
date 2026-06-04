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

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return AppSettings.Defaults();
            var dto = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options);
            return dto?.Sanitized() ?? AppSettings.Defaults();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Quarantine();
            return AppSettings.Defaults();   // never fail to launch on a bad file
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));   // flushed on close
        if (File.Exists(_path)) File.Replace(tmp, _path, null);                // atomic, same volume
        else File.Move(tmp, _path);
    }

    private void Quarantine()
    {
        try { if (File.Exists(_path)) File.Copy(_path, _path + ".corrupt", overwrite: true); }
        catch { /* quarantine must never throw */ }
    }
}
