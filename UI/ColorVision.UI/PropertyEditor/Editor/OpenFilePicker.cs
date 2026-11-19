using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.UI;

public sealed class FilePickerHistoryConfig : IConfig
{
    public Dictionary<string, string> LastOpenDirectories { get; } = new();
}

public static class OpenFilePicker
{
    public static string? ShowDialog(OpenFileDialog dialog, Window? owner, string? currentPath, string historyKey, string? previousDirectory = null)
    {
        var configService = ConfigService.Instance;
        var history = configService?.GetRequiredService<FilePickerHistoryConfig>();
        string? lastDirectory = history?.LastOpenDirectories.GetValueOrDefault(historyKey) ?? previousDirectory;
        var (directory, fileName) = PathSelectionHelper.GetFileDialogStart(currentPath, lastDirectory);
        dialog.InitialDirectory = directory ?? string.Empty;
        dialog.FileName = fileName ?? string.Empty;

        if ((owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true)
            return null;

        string? selectedDirectory = Path.GetDirectoryName(dialog.FileName);
        if (history != null && selectedDirectory != null)
        {
            history.LastOpenDirectories[historyKey] = selectedDirectory;
            configService!.Save<FilePickerHistoryConfig>();
        }
        return dialog.FileName;
    }
}
