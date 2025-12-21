#pragma warning disable CS8604,CA1822
using ColorVision.UI;
using ColorVision.UI.Plugins;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Update
{
    [FileExtension(".cvx")]
    public class CVXFileProcess : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }
        public void Process(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string fileName = Path.GetFileName(filePath);

            AutoUpdater.RestartIsIncrementApplication(filePath);
        }
    }

    [FileExtension(".cvxp")]

    public class CVXPProcessUpdte : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public void Process(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string fileName = Path.GetFileName(filePath);
            PluginUpdater.UpdatePlugin(filePath);
        }
    }


    [FileExtension(".zip")]

    public class FileProcessUpdte : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public void Process(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string fileName = Path.GetFileName(filePath);

            if (Regex.IsMatch(fileName, @"^ColorVision-Update-\[.*\]\.zip$", RegexOptions.IgnoreCase))
            {
                AutoUpdater.RestartIsIncrementApplication(filePath);
                return; // Assuming we stop here if A is executed. Remove return if flow should continue.
            }
            if (Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                bool hasManifest = false;

                try
                {
                    // Open the zip to check contents without extracting everything
                    using (ZipArchive archive = ZipFile.OpenRead(filePath))
                    {
                        // Case-insensitive check for manifest.json at the root of the zip
                        hasManifest = archive.Entries.Any(e => e.FullName.Contains("manifest.json", StringComparison.OrdinalIgnoreCase));
                    }
                }
                catch (Exception ex)
                {
                    // Handle invalid zip files or permission errors
                    Console.WriteLine($"Error reading zip file: {ex.Message}");
                    return;
                }
                if (hasManifest)
                {
                    PluginUpdater.UpdatePlugin(filePath);
                    return;
                }
            }



        }
    }
}
