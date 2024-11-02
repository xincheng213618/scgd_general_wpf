namespace ColorVision.UI
{
    public interface IFileProcessor
    {
        int Order { get; }
        bool CanProcess(string filePath);
        void Process(string filePath);
        bool CanExport (string filePath);

        void Export(string filePath);
    }
}
