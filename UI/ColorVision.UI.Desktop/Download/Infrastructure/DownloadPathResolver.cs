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
                            return dn;
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
                    return fileName;
            }
            catch
            {
            }

            return $"download_{DateTime.Now:yyyyMMddHHmmss}";
        }

        public static string GetUniqueFilePath(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            if (!File.Exists(filePath) && !File.Exists(filePath + ".aria2"))
                return filePath;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, $"{nameWithoutExt}({i}){ext}");
                if (!File.Exists(candidate) && !File.Exists(candidate + ".aria2"))
                    return candidate;
            }

            return Path.Combine(directory, $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
        }
    }
}
