using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Project.RecentFile
{
    public interface IRecentFile
    {
        List<string> RecentFiles(int max);
        void InsertFile(string filepath, int max);
        void RemoveFile(string filepath, int max);
        void Clear();
    }
}
