using ColorVision.Windowing;

namespace ColorVision;

/// <summary>Selects the supported shell for the operating system at startup.</summary>
internal static class MainWindowFactory
{
    internal static MainWindow Create() =>
        CompactTitleBarChrome.IsSupportedOperatingSystem ? new CompactMainWindow() : new MainWindow();
}
