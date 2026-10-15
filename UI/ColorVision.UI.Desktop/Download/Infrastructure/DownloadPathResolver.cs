using System.IO;

namespace ColorVision.UI.Desktop.Download
{
    internal static class DownloadPathResolver
    {
        public static string GetFileNameFromUrl(string url)
        {
            if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int dnIndex = url.IndexOf("dn=", StringComparison.OrdinalIgnoreCase);
                    if (dnIndex >= 0)
                    {
                        string dn = url.Substring(dnIndex + 3);
                        int endIndex = dn.IndexOf('&');
                        if (endIndex >= 0)
                            dn = dn.Substring(0, endIndex);

                        dn = Uri.UnescapeDataString(dn).Trim();
                        if (!string.IsNullOrWhiteSpace(dn))
                            return NormalizeFileName(dn);
                    }
                }
                catch
                {
                }

                return $"magnet_{DateTime.Now:yyyyMMddHHmmss}";
            }

            try
            {
                var uri = new Uri(url);
                string fileName = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName) && fileName != "/")
                    return NormalizeFileName(fileName);
            }
            catch
            {
            }

            return $"download_{DateTime.Now:yyyyMMddHHmmss}";
        }

        public static string NormalizeFileName(string fileName)
        {
            // A URL display name is data, never a path supplied to the filesystem.
            string name = Path.GetFileName(fileName.Replace('/', '\\')).Trim().TrimEnd('.');
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
                name = "download";
            string stem = Path.GetFileNameWithoutExtension(name);
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
                name = "_" + name;
            return name;
        }

        public static string GetUniqueFilePath(string directory, string fileName, Func<string, bool>? isReserved = null)
        {
            directory = Path.GetFullPath(directory);
            fileName = NormalizeFileName(fileName);
            string filePath = Path.Combine(directory, fileName);
            bool Available(string path) => !File.Exists(path) && !Directory.Exists(path) && !File.Exists(path + ".aria2") && isReserved?.Invoke(path) != true;
            if (Available(filePath))
                return filePath;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, $"{nameWithoutExt}({i}){ext}");
                if (Available(candidate))
                    return candidate;
            }

            return Path.Combine(directory, $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}");
        }
    }
}
