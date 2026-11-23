using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string Filter = "Tidsro backup (*.json)|*.json|All files (*.*)|*.*";

    // WAV only, and no "All files" escape hatch: SoundPlayer plays PCM wave audio and nothing else,
    // so offering an .mp3 here would only produce a file Tidsro then has to refuse.
    private const string WavFilter = "WAV audio (*.wav)|*.wav";

    private static string Documents =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private static string Music =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    public string? AskSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".json",
            Filter = Filter,
            InitialDirectory = Documents,
            OverwritePrompt = true,   // the Windows dialog asks; we do not double up
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? AskOpenPath()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = Filter,
            InitialDirectory = Documents,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? AskCsvSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = "CSV spreadsheet (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = Documents,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? AskWavPath()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".wav",
            Filter = WavFilter,
            InitialDirectory = Music,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
