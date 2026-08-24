using System.IO;
using System.Text.Json;
using Tidsro.Models;

namespace Tidsro.Services;

/// <summary>File-level export and import: write a file the user chose, read one back with validation,
/// and keep a single copy of the state an import is about to replace. No WPF here — the dialogs live
/// behind <see cref="IFileDialogService"/> and the decisions live in the view model.</summary>
public sealed class DataTransferService
{
    /// <summary>A file the user picked by accident — a log, a video — must not be read into memory.
    /// 8 MB is thousands of alarms; OutOfMemoryException is otherwise outside the caught set and
    /// would reach the global handler as a crash.</summary>
    public const long MaxImportBytes = 8 * 1024 * 1024;

    // A JSON object deserialises into TidsroData whatever it contains, so a document carrying none of
    // these keys is not a Tidsro file however well-formed it is. This gate is what stops a mistyped
    // file reading as a legitimate empty backup.
    private static readonly string[] KnownKeys =
        { "SchemaVersion", "Settings", "Alarms", "RecurringAlarms" };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // No polymorphic/$type handling. Default, non-polymorphic contracts only.
    };

    private readonly string _dataPath;
    public DataTransferService(string dataPath) => _dataPath = dataPath;

    /// <summary>Where the pre-import copy goes: one file beside the live data, overwritten each time.</summary>
    public string SnapshotPath =>
        Path.Combine(Path.GetDirectoryName(_dataPath)!, "data-before-import.json");

    /// <summary>Write the current state to a file the user chose. Throws on failure — the caller
    /// reports it, because an export that fails silently leaves the user believing they have a backup
    /// they do not have.</summary>
    public void Export(string path, TidsroData data) => PersistenceService.WriteTo(path, data);

    /// <summary>Read and validate a file the user chose. Returns null for anything that is not a
    /// usable Tidsro document; the caller shows the error and changes nothing.</summary>
    public TidsroData? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxImportBytes) return null;

            var json = File.ReadAllText(path);
            if (!LooksLikeTidsroDocument(json)) return null;

            var data = JsonSerializer.Deserialize<TidsroData>(json, Options);
            return data?.Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException
                                     or UnauthorizedAccessException or OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Copy the state an import is about to replace. Best effort: an import must never be
    /// blocked by a snapshot that could not be written.</summary>
    public void SnapshotBeforeImport()
    {
        try { if (File.Exists(_dataPath)) File.Copy(_dataPath, SnapshotPath, overwrite: true); }
        catch { /* the snapshot must never throw */ }
    }

    private static bool LooksLikeTidsroDocument(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var key in KnownKeys)
                if (doc.RootElement.TryGetProperty(key, out _)) return true;
            return false;
        }
        catch (JsonException) { return false; }
    }
}
