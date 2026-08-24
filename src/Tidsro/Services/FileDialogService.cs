using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string Filter = "Tidsro backup (*.json)|*.json|All files (*.*)|*.*";

    private static string Documents =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

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
}
