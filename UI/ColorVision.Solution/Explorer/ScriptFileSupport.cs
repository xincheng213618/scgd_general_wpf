using System.IO;

namespace ColorVision.Solution.Explorer
{
    internal static class ScriptFileSupport
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bat",
            ".cmd",
            ".ps1",
            ".py",
            ".pyw",
        };

        public static bool CanRun(FileInfo fileInfo)
        {
            return fileInfo.Exists && Extensions.Contains(fileInfo.Extension);
        }
    }
}
