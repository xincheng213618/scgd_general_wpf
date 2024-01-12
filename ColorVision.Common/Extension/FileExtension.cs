using ColorVision.Util;

namespace ColorVision.Common.Extension
{
    public static class FileExtension
    {
        public static bool HasDefaultProgram(this System.IO.FileInfo file) => Tool.HasDefaultProgram(file.FullName);

        public static void Empty(this System.IO.DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles())
                file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) 
                subDirectory.Delete(true);
        }
    }
}
