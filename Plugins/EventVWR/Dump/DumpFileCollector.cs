using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EventVWR.Dump
{
    /// <summary>
    /// Collects crash dump files from the WER (Windows Error Reporting) dump folder for the feedback system.
    /// </summary>
    public class DumpFileCollector : ColorVision.UI.IFeedbackLogCollector
    {
        public string Name => "Crash Dumps";
        public int Order => 30;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            // DumpConfig reads DumpFolder from WER registry on construction
            var config = new DumpConfig();

            string dumpFolder = config.DumpFolder;
            if (string.IsNullOrEmpty(dumpFolder) || !Directory.Exists(dumpFolder))
                return results;

            // Collect recent dump files (last 7 days, up to 5 files, max 100MB each)
            IEnumerable<FileInfo> dumpFiles;
            try
            {
                dumpFiles = Directory.GetFiles(dumpFolder, "*.dmp", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime > DateTime.Now.AddDays(-7))
                    .Where(f => f.Length < 100 * 1024 * 1024) // Skip dumps > 100MB
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(5);
            }
            catch
            {
                return results;
            }

            foreach (var dumpFile in dumpFiles)
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"dumpcopy_{dumpFile.Name}");
                    File.Copy(dumpFile.FullName, tempCopy, true);
                    results.Add(($"Dumps/{dumpFile.Name}", tempCopy));
                }
                catch
                {
                    // Skip files that can't be copied
                }
            }

            return results;
        }
    }
}
