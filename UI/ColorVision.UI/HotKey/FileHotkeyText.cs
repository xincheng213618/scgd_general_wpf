using System.Globalization;
using System.Resources;

namespace ColorVision.UI.HotKey;

/// <summary>Shared presentation for file actions contributed by the UI and workspace modules.</summary>
public static class FileHotkeyText
{
    private static readonly ResourceManager Text = new("ColorVision.UI.HotKey.FileHotkeyResources", typeof(FileHotkeyText).Assembly);
    private static string Get(string name) => Text.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string Category => Get(nameof(Category));
    public static string OpenFile => Get(nameof(OpenFile));
    public static string OpenFileDescription => Get(nameof(OpenFileDescription));
    public static string OpenWorkspace => Get(nameof(OpenWorkspace));
    public static string OpenWorkspaceDescription => Get(nameof(OpenWorkspaceDescription));
    public static string OpenFolder => Get(nameof(OpenFolder));
    public static string OpenFolderDescription => Get(nameof(OpenFolderDescription));
    public static string Save => Get(nameof(Save));
    public static string SaveDescription => Get(nameof(SaveDescription));
    public static string SaveAs => Get(nameof(SaveAs));
    public static string SaveAsDescription => Get(nameof(SaveAsDescription));
    public static string CloseDocument => Get(nameof(CloseDocument));
    public static string CloseDocumentDescription => Get(nameof(CloseDocumentDescription));
}
