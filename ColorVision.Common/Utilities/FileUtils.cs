using System.IO;
using System.Linq;

namespace ColorVision.Common.Utilities
{
    public static class FileUtils
    {
        public static bool HasDefaultProgram(this System.IO.FileInfo file) => Tool.HasDefaultProgram(file.FullName);

        public static void Empty(this System.IO.DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles())
                file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) 
                subDirectory.Delete(true);
        }

        public static long GetDirectorySize(this DirectoryInfo directoryInfo)
        {
            return directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
    }
}
