namespace ColorVision.UI
{
    public sealed record FileOpenRouteResult(
        bool Handled,
        bool Succeeded,
        string ErrorMessage = "",
        bool Canceled = false)
    {
        public static FileOpenRouteResult NotHandled { get; } = new(false, false);
    }

    public sealed record FileExportResult(
        bool Handled,
        bool Succeeded,
        string ErrorMessage = "",
        bool Canceled = false)
    {
        public static FileExportResult NotHandled { get; } = new(false, false);
    }

    /// <summary>
    /// Performs a non-editor open action such as installing a package,
    /// importing a license, or opening a flow-specific tool window.
    /// </summary>
    public interface IFileOpenActionProcessor
    {
        int Order { get; }
        FileOpenRouteResult OpenFile(string filePath);
    }

    /// <summary>
    /// Exports one existing source file. Exporters are discovered separately
    /// from open actions and document editors.
    /// </summary>
    public interface IFileExporter
    {
        int Order { get; }
        FileExportResult Export(string filePath);
    }
}
