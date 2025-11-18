using log4net;
using log4net.Util;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;


namespace ColorVision.UI.Plugins
{
    public static class PluginLoader
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginLoader));
        public static PluginLoaderrConfig Config => PluginLoaderrConfig.Instance;

        public static void LoadPlugins()
        {
            LoadPlugins("Plugins");
            AssemblyHandler.GetInstance().RefreshAssemblies();
        }

        public static void LoadPlugins(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var plugins = PluginLoaderrConfig.Instance.Plugins;
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
                            log.Warn(string.Format(Properties.Resources.PluginMissingId, directory));
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
                                                log.Warn(string.Format(Properties.Resources.DependencyDllNotFound, dep.Key, expectedDll));
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
                                                    log.ErrorExt(string.Format(Properties.Resources.DependencyVersionInsufficient, dep.Key, requiredVersion, actualVersion));
                                                    MessageBox.Show(string.Format(Properties.Resources.DependencyVersionInsufficient, dep.Key, requiredVersion, actualVersion));
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                depsOk = false;
                                                log.Warn(string.Format(Properties.Resources.DependencyCheckException, dep.Key, ex.Message));
                                                MessageBox.Show(string.Format(Properties.Resources.DependencyCheckException, dep.Key, ex.Message));
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

                        // README, ChangeLog, and Icon are now loaded on-demand to reduce startup time
                       
                        if (!pluginInfo.Enabled)
                            continue;

                        if (File.Exists(dllPath))
                        {
                            log.Info(string.Format(Properties.Resources.LoadingPlugin, manifest.Name));

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
                            log.Warn(string.Format(Properties.Resources.PluginDllNotFound, dllPath));
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
                            log.Info(string.Format(Properties.Resources.LoadedPluginWithoutManifest, dllPath));
                        }
                        else
                        {
                            log.Warn(string.Format(Properties.Resources.PluginDllNotFound, dllPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Properties.Resources.PluginLoadError, ex.Message), "ColorVision");
                    log.Error(ex);
                }
            }

        }
    }
}