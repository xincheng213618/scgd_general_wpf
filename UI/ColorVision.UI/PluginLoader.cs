using ColorVision.Common.MVVM;
using log4net;
using log4net.Util;
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

        [JsonProperty("dllpath")]
        public string DllName { get; set; }

        [JsonProperty("requires")]
        public Version Requires { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("entry_point")]
        public string EntryPoint { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }



    public class PluginInfo : ViewModelBase
    {
        public PluginManifest Manifest { get; set; }

        public DepsJson DepsJson { get; set; }
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
    }

    public class DepsJson
    {
        [JsonProperty("runtimeTarget")]
        public RuntimeTarget RuntimeTarget { get; set; }

        [JsonProperty("targets")]
        public Dictionary<string, Dictionary<string, DepsTargetEntry>> Targets { get; set; }
    }

    public class RuntimeTarget
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class DepsTargetEntry
    {
        [JsonProperty("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }
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

            var plugins = PluginManagerConfig.Instance.Plugins;
            path = Path.GetFullPath(path); // 保证path是绝对路径
                                           // 先收集当前所有的插件目录名（通常以插件Id为key）
            var validIds = new HashSet<string>();
            foreach (var directory in Directory.GetDirectories(path))
            {
                string manifestPath = Path.Combine(directory, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string manifestContent = File.ReadAllText(manifestPath);
                        var manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestContent);
                        if (!string.IsNullOrWhiteSpace(manifest?.Id))
                        {
                            validIds.Add(manifest.Id);
                        }
                    }
                    catch { /* ignore invalid manifest */ }
                }
            }

            // 删除那些在记录中存在但物理上已不存在的插件
            var toRemove = plugins.Keys.Where(id => !validIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                plugins.Remove(id);
            }


            foreach (var directory in Directory.GetDirectories(path))
            {
                string manifestPath = Path.Combine(directory, "manifest.json");
                PluginManifest manifest = null;
                string dllPath = null;

                DepsJson depsObj = null;
                string[] depsFiles = Directory.GetFiles(directory, "*.deps.json");
                if (depsFiles.Length == 1)
                {
                    string depsPath = depsFiles[0];
                    string json = File.ReadAllText(depsPath);

                    depsObj = JsonConvert.DeserializeObject<DepsJson>(json);
                }
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

                        dllPath = !string.IsNullOrEmpty(manifest.DllName)
                            ? Path.Combine(directory, manifest.DllName)
                            : Path.Combine(directory, Path.GetFileName(directory) + ".dll");

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

                        pluginInfo.DepsJson = depsObj;
                        bool depsOk = false;

                        if (depsObj != null)
                        {
                            var mainTargetDict = depsObj.Targets?.Values.FirstOrDefault();
                            if (mainTargetDict != null)
                            {
                                var mainPackage = mainTargetDict.Values.FirstOrDefault();
                                var dependencies = mainPackage?.Dependencies;
                                if (dependencies != null && dependencies.Count > 0)
                                {
                                    depsOk = true;
                                    foreach (var dep in dependencies)
                                    {
                                        if (dep.Key.StartsWith("ColorVision"))
                                        {
                                            // 依赖的dll名规则 ColorVision.XXX.dll
                                            string expectedDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dep.Key + ".dll");
                                            if (!File.Exists(expectedDll))
                                            {
                                                log.Warn($"依赖 {dep.Key} 未找到对应的dll: {expectedDll}");
                                                continue;
                                            }

                                            // 获取dll实际版本
                                            try
                                            {
                                                var assemblyName = AssemblyName.GetAssemblyName(expectedDll);
                                                var actualVersion = assemblyName.Version;
                                                var requiredVersion = new Version(dep.Value);

                                                if (actualVersion == null || actualVersion < requiredVersion)
                                                {
                                                    depsOk = false;
                                                    log.ErrorExt($"依赖 {dep.Key} 版本不足，要求: {requiredVersion}，实际: {actualVersion}");
                                                    MessageBox.Show($"依赖 {dep.Key} 版本不足，要求: {requiredVersion}，实际: {actualVersion}");
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                depsOk = false;
                                                log.Warn($"检查依赖 {dep.Key} 版本时发生异常: {ex.Message}");
                                                MessageBox.Show(($"检查依赖 {dep.Key} 版本时发生异常: {ex.Message}"));
                                                break;
                                            }
                                        }
                                    }
                                }

                            }
                            if (!depsOk)
                            {
                                continue;
                            }
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
        }
    }
}