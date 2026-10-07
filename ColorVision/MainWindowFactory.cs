using ColorVision.Windowing;

namespace ColorVision;

/// <summary>Selects a shell only at startup; existing windows never change type in place.</summary>
internal static class MainWindowFactory
{
    internal static bool ShouldUseCompactMainWindow(bool configured, bool operatingSystemSupported) =>
        configured && operatingSystemSupported;

    internal static MainWindow Create(bool useCompactMainWindow)
    {
        bool selected = ShouldUseCompactMainWindow(useCompactMainWindow, CompactTitleBarChrome.IsSupportedOperatingSystem);
        return selected ? new CompactMainWindow() : new MainWindow();
    }
}
