using System.Collections.Generic;

namespace ColorVision.RecentFile
{
    public interface IRecentFile
    {
        List<string> RecentFiles(int max);
        void InsertFile(string filepath, int max);
        void RemoveFile(string filepath, int max);
        void Clear();
    }
}
