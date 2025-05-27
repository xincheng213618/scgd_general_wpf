using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI
{
    public class PluginManifest : ViewModelBase
    {
        [JsonProperty("id")]
        public string Id { get; set; } // 新增，插件唯一ID

        [JsonProperty("manifest_version")]
        public int ManifestVersion { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("dll_name")]
        public string DllName { get; set; }

        [JsonProperty("requires")]
        public Version Requires { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("entry_point")]
        public string EntryPoint { get; set; }

        [JsonProperty("dependencies")]
        public string[] Dependencies { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }

    public class PluginInfo : ViewModelBase
    {
        public PluginManifest Manifest { get; set; }
        public bool Enabled { get; set; } = true;
        public string? Name { get; set; }

        public string Description { get; set; }

        public Version? AssemblyVersion { get; set; }
        public DateTime? AssemblyBuildDate { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyPath { get; set; }
        public string? AssemblyCulture { get; set; }
        public string? AssemblyPublicKeyToken { get; set; }

        public string README { get; set; } = string.Empty;

        public string ChangeLog { get; set; } = string.Empty;

        [JsonIgnore]
        public ImageSource? Icon { get; set; }


        [JsonIgnore]
        public Assembly Assembly { get; set; }
        [JsonIgnore]
        public IPlugin IPlugin { get; set; }
    }

    public class PluginManagerConfig : IConfig
    {
        public static PluginManagerConfig Instance =>  ConfigService.Instance.GetRequiredService<PluginManagerConfig>();

        public string PluginPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

        // 用插件Id作为Key，保证唯一性
        public Dictionary<string, PluginInfo> Plugins { get; set; } = new Dictionary<string, PluginInfo>();
    }

    public static class PluginManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginManager));
        public static PluginManagerConfig Config => PluginManagerConfig.Instance;

        public static void LoadPlugins(string path)
        {
            if (!Directory.Exists(path))
                return;

            var hostVersion = Assembly.GetEntryAssembly().GetName().Version;
            var plugins = PluginManagerConfig.Instance.Plugins;
            var discoveredIds = new HashSet<string>();


            path = Path.GetFullPath(path); // 保证path是绝对路径
            foreach (var directory in Directory.GetDirectories(path))
            {
                string manifestPath = Path.Combine(directory, "manifest.json");
                PluginManifest manifest = null;
                string dllPath = null;

                try
                {
                    if (File.Exists(manifestPath))
                    {
                        string manifestContent = File.ReadAllText(manifestPath);
                        manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestContent);
                        if (string.IsNullOrWhiteSpace(manifest.Id))
                        {
                            log.Warn($"插件 {directory} 缺少唯一Id，已跳过");
                            continue;
                        }
                        discoveredIds.Add(manifest.Id);

                        dllPath = !string.IsNullOrEmpty(manifest.DllName)
                            ? Path.Combine(directory, manifest.DllName)
                            : Path.Combine(directory, Path.GetFileName(directory) + ".dll");

                        if (hostVersion < manifest.Requires)
                        {
                            MessageBox.Show($"插件 {manifest.Name} {manifest.Version} 支持的最小版本为 {manifest.Requires}，请更新软件以启用插件");
                            continue;
                        }

                        // 加载插件
                        if (!plugins.TryGetValue(manifest.Id, out var pluginInfo))
                        {
                            pluginInfo = new PluginInfo { Manifest = manifest, Enabled = true };
                            plugins[manifest.Id] = pluginInfo;
                        }
                        else
                        {
                            pluginInfo.Manifest = manifest; // 更新manifest
                        }
                        string readmePath = Path.Combine(directory, "readme.md");
                        if (File.Exists(readmePath))
                            pluginInfo.README = File.ReadAllText(readmePath); ;

                        string changelogPath = Path.Combine(directory, "changelog.md");
                        if (File.Exists(changelogPath))
                            pluginInfo.ChangeLog = File.ReadAllText(changelogPath); ;

                        string PackageIconPath = Path.Combine(directory, "PackageIcon.png");
                        if (File.Exists(PackageIconPath))
                            pluginInfo.Icon = new BitmapImage(new Uri(PackageIconPath));
                       
                        if (!pluginInfo.Enabled)
                            continue;

                        if (File.Exists(dllPath))
                        {
                            pluginInfo.Assembly = Assembly.LoadFrom(dllPath);
                            var assembly = pluginInfo.Assembly;

                            pluginInfo.AssemblyName = assembly.GetName().Name;
                            pluginInfo.AssemblyVersion = assembly.GetName().Version;
                            pluginInfo.AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);
                            pluginInfo.AssemblyPath = assembly.Location;
                            pluginInfo.AssemblyCulture = assembly.GetName().CultureInfo?.Name ?? "neutral";
                            pluginInfo.AssemblyPublicKeyToken = BitConverter.ToString(assembly.GetName().GetPublicKeyToken() ?? Array.Empty<byte>());
                            pluginInfo.Name = manifest.Name;
                            pluginInfo.Description = manifest.Description;
                        }
                        else
                        {
                            log.Warn($"插件DLL不存在: {dllPath}");
                        }
                    }
                    else
                    {
                        // 没有manifest，按目录名加载DLL（不加入 Plugins 字典）
                        string dirName = Path.GetFileName(directory);
                        dllPath = Path.Combine(directory, dirName + ".dll");
                        if (File.Exists(dllPath))
                        {
                            Assembly.LoadFrom(dllPath);
                            log.Info($"加载了没有manifest的插件: {dllPath}");
                        }
                        else
                        {
                            log.Warn($"插件DLL不存在: {dllPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载插件或manifest错误：{ex.Message}", "ColorVision");
                    log.Error(ex);
                }
            }

            // 移除磁盘上已不存在的插件
            var removedIds = plugins.Keys.Except(discoveredIds).ToList();
            foreach (var removedId in removedIds)
            {
                plugins.Remove(removedId);
                log.Info($"移除已不存在的插件: {removedId}");
            }
        }
    }
}