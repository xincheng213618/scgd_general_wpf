namespace ColorVision.UI
{
    public interface IFileProcessor
    {
        int Order { get; }
        bool Process(string filePath);
        void Export(string filePath);
    }

}
