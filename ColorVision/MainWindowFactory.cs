using ColorVision.Windowing;

namespace ColorVision;

/// <summary>Selects a shell only at startup; existing windows never change type in place.</summary>
internal static class MainWindowFactory
{
    internal static bool ShouldUseCompactMainWindow(bool configured, bool operatingSystemSupported) =>
        configured && operatingSystemSupported;

    internal static MainWindow Create(bool useCompactMainWindow)
#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
    {
        bool requested = MainWindowResizeDiagnostics.SelectMode(useCompactMainWindow, out bool modeOverrideApplied);
        bool selected = ShouldUseCompactMainWindow(requested, CompactTitleBarChrome.IsSupportedOperatingSystem);
        MainWindow window = selected ? new CompactMainWindow() : new MainWindow();
        MainWindowResizeDiagnostics.Register(window, useCompactMainWindow, selected, modeOverrideApplied);
        return window;
    }
#else
    {
        bool selected = ShouldUseCompactMainWindow(useCompactMainWindow, CompactTitleBarChrome.IsSupportedOperatingSystem);
        return selected ? new CompactMainWindow() : new MainWindow();
    }
#endif
}
