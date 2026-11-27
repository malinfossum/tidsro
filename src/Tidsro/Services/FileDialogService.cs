using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string Filter = "Tidsro backup (*.json)|*.json|All files (*.*)|*.*";

    // The two formats My sound takes, and no "All files" escape hatch: anything else would only
    // produce a file Tidsro then has to refuse.
    private const string SoundFilter = "Sound files (*.wav;*.mp3)|*.wav;*.mp3|WAV audio (*.wav)|*.wav|MP3 audio (*.mp3)|*.mp3";

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

    public string? AskSoundPath()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".wav",
            Filter = SoundFilter,
            InitialDirectory = Music,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
