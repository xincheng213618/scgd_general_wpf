namespace ColorVision.UI
{
    public interface IFileProcessor
    {
        int Order { get; }
        void Process(string filePath);
        void Export(string filePath);
    }
}
