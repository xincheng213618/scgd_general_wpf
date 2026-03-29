using System.Security.Cryptography;

namespace ColorVision.PluginMarketplace.Services
{
    public class StorageService
    {
        private readonly string _basePath;
        private readonly string _baseUrl;

        public StorageService(IConfiguration configuration)
        {
            _basePath = configuration.GetValue<string>("Storage:BasePath")
                ?? Path.Combine(AppContext.BaseDirectory, "storage");
            _baseUrl = configuration.GetValue<string>("Storage:BaseUrl") ?? "/api/files";

            Directory.CreateDirectory(_basePath);
        }

        /// <summary>
        /// Generate the relative storage path for a plugin package
        /// </summary>
        public string GetPluginPackagePath(string pluginId, string version)
        {
            return Path.Combine("plugins", pluginId, $"{pluginId}-{version}.cvxp");
        }

        /// <summary>
        /// Generate the relative storage path for a plugin icon
        /// </summary>
        public string GetPluginIconPath(string pluginId)
        {
            return Path.Combine("plugins", pluginId, "icon.png");
        }

        /// <summary>
        /// Save a file to storage and return its SHA256 hash
        /// </summary>
        public async Task<string> SaveFileAsync(Stream stream, string relativePath)
        {
            var absolutePath = GetAbsolutePath(relativePath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (directory != null)
                Directory.CreateDirectory(directory);

            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
            using var hashStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);

            await stream.CopyToAsync(hashStream);
            await hashStream.FlushFinalBlockAsync();

            return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        /// <summary>
        /// Get the absolute file system path for a relative storage path
        /// </summary>
        public string GetAbsolutePath(string relativePath)
        {
            return Path.Combine(_basePath, relativePath);
        }

        /// <summary>
        /// Get the public URL for a stored file
        /// </summary>
        public string GetFileUrl(string relativePath)
        {
            return $"{_baseUrl}/{relativePath.Replace('\\', '/')}";
        }

        /// <summary>
        /// Check if a file exists in storage
        /// </summary>
        public bool FileExists(string relativePath)
        {
            return File.Exists(GetAbsolutePath(relativePath));
        }

        /// <summary>
        /// Delete a file from storage
        /// </summary>
        public void DeleteFile(string relativePath)
        {
            var path = GetAbsolutePath(relativePath);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
