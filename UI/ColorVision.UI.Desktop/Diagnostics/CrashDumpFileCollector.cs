using System.IO;

namespace ColorVision.UI.Desktop.Diagnostics
{
    /// <summary>
    /// Adds recent WER dump files to the feedback diagnostics package.
    /// </summary>
    public sealed class CrashDumpFileCollector : IFeedbackLogCollector
    {
        private const long MaxDumpFileSize = 100L * 1024 * 1024;

        public string Name => "Crash Dumps";

        public string Description => "Recent Windows Error Reporting dump files";

        public int Order => 30;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();
            string dumpFolder = new CrashDumpConfiguration().DumpFolder;
            if (string.IsNullOrWhiteSpace(dumpFolder) || !Directory.Exists(dumpFolder)) return results;

            IEnumerable<FileInfo> dumpFiles;
            try
            {
                dumpFiles = Directory.EnumerateFiles(dumpFolder, "*.dmp", SearchOption.TopDirectoryOnly)
                    .Select(filePath => new FileInfo(filePath))
                    .Where(file => file.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-7))
                    .Where(file => file.Length <= MaxDumpFileSize)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(5)
                    .ToList();
            }
            catch
            {
                return results;
            }

            foreach (FileInfo dumpFile in dumpFiles)
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"ColorVision_Dump_{Guid.NewGuid():N}_{dumpFile.Name}");
                    File.Copy(dumpFile.FullName, tempCopy, overwrite: true);
                    results.Add(($"Dumps/{dumpFile.Name}", tempCopy));
                }
                catch
                {
                }
            }

            return results;
        }
    }
}
