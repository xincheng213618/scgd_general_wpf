using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Plugins
{
    public class PluginLoaderrConfig : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginLoaderrConfig));
        private static readonly object locker = new();
        private static PluginLoaderrConfig? _instance;
        private static bool _processExitRegistered;

        public static PluginLoaderrConfig Instance
        {
            get
            {
                lock (locker)
                {
                    _instance ??= Load();
                    RegisterProcessExitSave();
                    return _instance;
                }
            }
        }

        [JsonIgnore]
        public string ConfigFilePath { get; private set; } = GetCurrentConfigFilePath();

        // 用插件Id作为Key，保证唯一性
        public Dictionary<string, PluginInfo> Plugins { get; set; } = new Dictionary<string, PluginInfo>();

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                log.Warn($"Save plugin config failed: {ConfigFilePath}", ex);
            }
        }

        private static PluginLoaderrConfig Load()
        {
            string configFilePath = GetCurrentConfigFilePath();
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    PluginLoaderrConfig? config = JsonConvert.DeserializeObject<PluginLoaderrConfig>(json);
                    if (config != null)
                    {
                        config.ConfigFilePath = configFilePath;
                        config.Plugins ??= new Dictionary<string, PluginInfo>();
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Load plugin config failed: {configFilePath}", ex);
            }

            return new PluginLoaderrConfig { ConfigFilePath = configFilePath };
        }

        private static void RegisterProcessExitSave()
        {
            if (_processExitRegistered)
                return;

            AppDomain.CurrentDomain.ProcessExit += (s, e) => _instance?.Save();
            _processExitRegistered = true;
        }

        private static string GetCurrentConfigFilePath()
        {
            string configDirectory = GetConfigDirectory();
            string installHash = GetInstallHash(AppDomain.CurrentDomain.BaseDirectory);
            return Path.Combine(configDirectory, "Plugins", $"{installHash}.json");
        }

        private static string GetConfigDirectory()
        {
            if (Directory.Exists("Config"))
            {
                return Path.GetFullPath("Config");
            }

            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            string company = entryAssembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
                ?? entryAssembly?.GetName().Name
                ?? "ColorVision";

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), company, "Config");
        }

        [SuppressMessage("Security", "CA5351:不要使用损坏的加密算法", Justification = "MD5 is only used as a stable cache file name for the install path.")]
        private static string GetInstallHash(string baseDirectory)
        {
            string normalizedPath = Path.GetFullPath(baseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();

            byte[] hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedPath));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
