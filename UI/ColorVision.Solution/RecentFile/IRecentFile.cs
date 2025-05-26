namespace ColorVision.Solution.RecentFile
{
    public interface IRecentFile
    {
        List<string> RecentFiles(int max);
        void InsertFile(string filepath, int max);
        void RemoveFile(string filepath, int max);
        void Clear();
    }
}
