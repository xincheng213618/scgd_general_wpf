namespace ColorVision;

// Keep the main shell's existing entry point while sharing the actual theme with tools.
internal sealed class AvalonDockTheme : Solution.Themes.AvalonDockTheme
{
    internal AvalonDockTheme(bool isDark) : base(isDark)
    {
    }
}
