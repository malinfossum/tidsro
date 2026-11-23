using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>Canned answers for the two dialogs, plus what the view model suggested as a file name.</summary>
public sealed class FakeFileDialogService : IFileDialogService
{
    public string? SavePath { get; set; }
    public string? OpenPath { get; set; }
    public string? WavPath { get; set; }
    public string? LastSuggestedName { get; private set; }

    public string? AskSavePath(string suggestedFileName)
    {
        LastSuggestedName = suggestedFileName;
        return SavePath;
    }

    public string? AskOpenPath() => OpenPath;

    public string? AskWavPath() => WavPath;

    public string? CsvPath { get; set; }
    public string? AskCsvSavePath(string suggestedFileName)
    {
        LastSuggestedName = suggestedFileName;
        return CsvPath;
    }
}
