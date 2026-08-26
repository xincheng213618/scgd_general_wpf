#pragma warning disable CA1310,CA1863,CS8601,CS8602
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
        private static int _lastLoadFailureCount;
        private static readonly HashSet<string> RetiredPluginIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "EventVWR"
        };

        public static PluginLoaderrConfig Config => PluginLoaderrConfig.Instance;

        public static bool LastLoadCompletedWithoutFailures => Volatile.Read(ref _lastLoadFailureCount) == 0;

        internal static bool IsRetiredPlugin(string? pluginId)
        {
            return !string.IsNullOrWhiteSpace(pluginId) && RetiredPluginIds.Contains(pluginId);
        }

        internal static bool ShouldSkipPlugin(IEnumerable<string>? skipOncePluginIds, string? manifestId, string? directoryName)
        {
            if (skipOncePluginIds == null)
                return false;

            foreach (string pluginId in skipOncePluginIds)
            {
                if (string.IsNullOrWhiteSpace(pluginId))
                    continue;

                if (string.Equals(pluginId, manifestId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pluginId, directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsPluginAssemblyAvailable(string? dllPath)
        {
            return !string.IsNullOrWhiteSpace(dllPath) && File.Exists(dllPath);
        }

        public static void LoadPlugins()
        {
            LoadPlugins("Plugins");
        }

        public static void LoadPlugins(ModuleCatalog moduleCatalog)
        {
            ArgumentNullException.ThrowIfNull(moduleCatalog);
            LoadPlugins("Plugins", moduleCatalog);
        }

        public static void LoadPlugins(ModuleCatalog moduleCatalog, IEnumerable<string>? skipOncePluginIds)
        {
            LoadPlugins(moduleCatalog, skipOncePluginIds, null);
        }

        public static void LoadPlugins(ModuleCatalog moduleCatalog, IEnumerable<string>? skipOncePluginIds, Action<string>? onPluginLoading)
        {
            ArgumentNullException.ThrowIfNull(moduleCatalog);
            LoadPlugins("Plugins", moduleCatalog, skipOncePluginIds, onPluginLoading);
        }

        public static void LoadPlugins(string path)
        {
            LoadPlugins(path, null);
        }

        private static void LoadPlugins(string path, ModuleCatalog? moduleCatalog)
        {
            LoadPlugins(path, moduleCatalog, null, null);
        }

        private static void LoadPlugins(
            string path,
            ModuleCatalog? moduleCatalog,
            IEnumerable<string>? skipOncePluginIds,
            Action<string>? onPluginLoading)
        {
            Volatile.Write(ref _lastLoadFailureCount, 0);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var skipOncePluginIdSet = new HashSet<string>(
                skipOncePluginIds?.Where(id => !string.IsNullOrWhiteSpace(id)) ?? [],
                StringComparer.OrdinalIgnoreCase);
            PluginLoaderrConfig pluginConfig = PluginLoaderrConfig.Instance;
            var plugins = pluginConfig.Plugins;
            path = Path.GetFullPath(path); // 保证path是绝对路径
                                           // 先收集当前所有的插件目录名（通常以插件Id为key）
            var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in Directory.GetDirectories(path))
            {
                string directoryName = Path.GetFileName(directory);
                string manifestPath = Path.Combine(directory, "manifest.json");
                bool validManifestIdFound = false;
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string manifestContent = File.ReadAllText(manifestPath);
                        var manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestContent);
                        if (!string.IsNullOrWhiteSpace(manifest?.Id))
                        {
                            validIds.Add(manifest.Id);
                            validManifestIdFound = true;
                        }
                    }
                    catch { /* ignore invalid manifest */ }
                }

                if (!validManifestIdFound)
                    validIds.Add(directoryName);
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
                try
                {
                    string dirName = Path.GetFileName(directory);
                    PluginInfo pluginInfo = null;

                    // Directory-level recovery decisions must run before manifest parsing so a
                    // malformed manifest can still be skipped or disabled safely.
                    if (IsRetiredPlugin(dirName))
                    {
                        log.Info($"Skipped retired plugin directory '{directory}'.");
                        continue;
                    }

                    if (plugins.TryGetValue(dirName, out pluginInfo) && !pluginInfo.Enabled)
                    {
                        log.Info($"Skipped disabled plugin directory '{directory}'.");
                        continue;
                    }

                    if (ShouldSkipPlugin(skipOncePluginIdSet, null, dirName))
                    {
                        log.Info($"Skipped plugin directory '{directory}' for this startup.");
                        continue;
                    }

                    if (File.Exists(manifestPath))
                    {
                        string manifestContent = File.ReadAllText(manifestPath);
                        manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestContent);
                        if (string.IsNullOrWhiteSpace(manifest.Id))
                        {
                            RecordLoadFailure();
                            log.Warn(string.Format(Properties.Resources.PluginMissingId, directory));
                            continue;
                        }

                        dllPath = !string.IsNullOrEmpty(manifest.DllName)
                            ? Path.Combine(directory, manifest.DllName)
                            : Path.Combine(directory, Path.GetFileName(directory) + ".dll");

                        // 加载插件
                        if (!plugins.TryGetValue(manifest.Id, out pluginInfo))
                        {
                            pluginInfo = new PluginInfo { Manifest = manifest, Enabled = true };
                            plugins[manifest.Id] = pluginInfo;
                        }
                        else
                        {
                            pluginInfo.Manifest = manifest; // 更新manifest
                        }

                        if (IsRetiredPlugin(manifest.Id))
                        {
                            pluginInfo.Enabled = false;
                            pluginInfo.Name = manifest.Name;
                            pluginInfo.Description = manifest.Description;
                            log.Info($"Skipped retired plugin '{manifest.Id}'. Its functionality is built into ColorVision.");
                            continue;
                        }

                        if (!pluginInfo.Enabled)
                            continue;

                        if (ShouldSkipPlugin(skipOncePluginIdSet, manifest.Id, dirName))
                        {
                            log.Info($"Skipped plugin '{manifest.Id}' for this startup.");
                            continue;
                        }
                    }
                    onPluginLoading?.Invoke(manifest?.Id ?? dirName);

                    DepsJson depsObj = null;
                    string[] depsFiles = Directory.GetFiles(directory, "*.deps.json");
                    if (depsFiles.Length == 1)
                    {
                        string depsPath = depsFiles[0];
                        string json = File.ReadAllText(depsPath);

                        depsObj = JsonConvert.DeserializeObject<DepsJson>(json);
                    }

                    if (manifest != null)
                    {
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
                                                depsOk = false;
                                                log.Warn(string.Format(Properties.Resources.DependencyDllNotFound, dep.Key, expectedDll));
                                                break;
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
                                RecordLoadFailure();
                                continue;
                            }
                        }
                      
                        if (IsPluginAssemblyAvailable(dllPath))
                        {
                            log.Info(string.Format(Properties.Resources.LoadingPlugin, manifest.Name));

                            pluginInfo.Assembly = Assembly.LoadFrom(dllPath);
                            moduleCatalog?.AddPlugin(manifest.Id, pluginInfo.Assembly);

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
                            RecordLoadFailure();
                            log.Warn(string.Format(Properties.Resources.PluginDllNotFound, dllPath));
                        }
                    }
                    else
                    {
                        dllPath = Path.Combine(directory, dirName + ".dll");
                        if (IsPluginAssemblyAvailable(dllPath))
                        {
                            Assembly assembly = Assembly.LoadFrom(dllPath);
                            moduleCatalog?.AddPlugin(dirName, assembly);
                            log.Info(string.Format(Properties.Resources.LoadedPluginWithoutManifest, dllPath));
                        }
                        else
                        {
                            RecordLoadFailure();
                            log.Warn(string.Format(Properties.Resources.PluginDllNotFound, dllPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    RecordLoadFailure();
                    MessageBox.Show(string.Format(Properties.Resources.PluginLoadError, ex.Message), "ColorVision");
                    log.Error(ex);
                }
            }

            pluginConfig.Save();
            AssemblyHandler.GetInstance().RefreshAssemblies();
        }

        private static void RecordLoadFailure()
        {
            Interlocked.Increment(ref _lastLoadFailureCount);
        }
    }
}
