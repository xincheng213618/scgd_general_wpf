namespace ColorVision;

/// <summary>Selects a shell only at startup; existing windows never change type in place.</summary>
internal static class MainWindowFactory
{
    internal static MainWindow Create(bool useCompactTitleBar)
#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
    {
        bool selected = Windowing.MainWindowResizeDiagnostics.SelectMode(useCompactTitleBar, out bool modeOverrideApplied);
        MainWindow window = selected ? new CompactMainWindow() : new MainWindow();
        Windowing.MainWindowResizeDiagnostics.Register(window, useCompactTitleBar, selected, modeOverrideApplied);
        return window;
    }
#else
        => useCompactTitleBar ? new CompactMainWindow() : new MainWindow();
#endif
}
