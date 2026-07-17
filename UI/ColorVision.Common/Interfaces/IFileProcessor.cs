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

    public interface IFileProcessor
    {
        int Order { get; }
        bool Process(string filePath);
        void Export(string filePath);
    }

    /// <summary>
    /// Marks a legacy file processor whose primary open behavior is an action
    /// such as installing a package, importing a license, or opening a flow.
    /// Document editors must use IEditor instead.
    /// </summary>
    public interface IFileOpenActionProcessor : IFileProcessor
    {
        FileOpenRouteResult OpenFile(string filePath)
        {
            bool succeeded = Process(filePath);
            return new FileOpenRouteResult(true, succeeded);
        }
    }
}
