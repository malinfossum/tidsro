namespace Tidsro.Services;

/// <summary>The Save/Open file dialogs behind an interface, so view-model tests never open a real one.
/// Both return null when the user cancels. Same reason <see cref="IStartupService"/> exists.</summary>
public interface IFileDialogService
{
    string? AskSavePath(string suggestedFileName);
    string? AskOpenPath();
}
