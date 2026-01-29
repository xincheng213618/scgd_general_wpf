using ColorVision.UI.Extension;
using log4net;
using System.IO;

namespace Pattern
{
    /// <summary>
    /// Manages user-defined default configurations for patterns
    /// </summary>
    public class PatternUserDefaultManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PatternUserDefaultManager));
        private static readonly Lazy<string> _userDefaultPath = new Lazy<string>(() =>
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ColorVision",
                "Pattern",
                "UserDefaults");
            
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
                
            return path;
        });

        private static string UserDefaultPath => _userDefaultPath.Value;

        /// <summary>
        /// Save current configuration as user default for the pattern type
        /// </summary>
        public static void SaveUserDefault(IPattern pattern)
        {
            try
            {
                string typeName = pattern.GetType().FullName;
                string filePath = GetUserDefaultPath(typeName);
                string configJson = pattern.GetConfig().ToJsonN();
                File.WriteAllText(filePath, configJson);
                log.Info($"User default saved for {typeName}");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to save user default: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Load user default configuration if exists
        /// </summary>
        public static string LoadUserDefault(Type patternType)
        {
            try
            {
                string typeName = patternType.FullName;
                string filePath = GetUserDefaultPath(typeName);
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to load user default: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if user default exists for pattern type
        /// </summary>
        public static bool HasUserDefault(Type patternType)
        {
            string typeName = patternType.FullName;
            string filePath = GetUserDefaultPath(typeName);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Delete user default for pattern type
        /// </summary>
        public static void DeleteUserDefault(Type patternType)
        {
            try
            {
                string typeName = patternType.FullName;
                string filePath = GetUserDefaultPath(typeName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    log.Info($"User default deleted for {typeName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Failed to delete user default: {ex.Message}", ex);
            }
        }

        private static string GetUserDefaultPath(string typeName)
        {
            // Sanitize the type name to create a safe filename
            // Remove all invalid filename characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safeFileName = string.Join("_", typeName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Ensure filename isn't too long (leave room for .json extension)
            if (safeFileName.Length > 200)
            {
                safeFileName = safeFileName.Substring(0, 200);
            }
            
            return Path.Combine(UserDefaultPath, $"{safeFileName}.json");
        }
    }
}
