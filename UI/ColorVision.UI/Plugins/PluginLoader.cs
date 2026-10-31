#pragma warning disable CA1310,CA1863,CS8601,CS8602
using log4net;
using log4net.Util;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;


namespace ColorVision.UI.Plugins
{
    public static class PluginLoader
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginLoader));
        private static PluginLoadReport _lastReport = PluginLoadReport.Empty;
        private static readonly HashSet<string> RetiredPluginIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "EventVWR"
        };

        public static PluginLoaderrConfig Config => PluginLoaderrConfig.Instance;

        /// <summary>The report of the most recent load pass; empty before the first pass.</summary>
        public static PluginLoadReport LastReport => Volatile.Read(ref _lastReport);

        public static bool LastLoadCompletedWithoutFailures => !LastReport.HasFailures;

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

        internal static bool IsPluginAssemblyAvailable([NotNullWhen(true)] string? dllPath)
        {
            return !string.IsNullOrWhiteSpace(dllPath) && File.Exists(dllPath);
        }

        public static PluginLoadReport LoadPlugins()
        {
            return LoadPlugins("Plugins");
        }

        public static PluginLoadReport LoadPlugins(ModuleCatalog moduleCatalog)
        {
            ArgumentNullException.ThrowIfNull(moduleCatalog);
            return LoadPlugins("Plugins", moduleCatalog);
        }

        public static PluginLoadReport LoadPlugins(ModuleCatalog moduleCatalog, IEnumerable<string>? skipOncePluginIds)
        {
            return LoadPlugins(moduleCatalog, skipOncePluginIds, null);
        }

        public static PluginLoadReport LoadPlugins(ModuleCatalog moduleCatalog, IEnumerable<string>? skipOncePluginIds, Action<string>? onPluginLoading)
        {
            ArgumentNullException.ThrowIfNull(moduleCatalog);
            return LoadPlugins("Plugins", moduleCatalog, skipOncePluginIds, onPluginLoading);
        }

        public static PluginLoadReport LoadPlugins(string path)
        {
            return LoadPlugins(path, null);
        }

        private static PluginLoadReport LoadPlugins(string path, ModuleCatalog? moduleCatalog)
        {
            return LoadPlugins(path, moduleCatalog, null, null);
        }

        public static Task<PluginLoadReport> LoadPluginsAsync(ModuleCatalog moduleCatalog, IEnumerable<string>? skipOncePluginIds,
            Action<string>? onPluginLoading, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return LoadPluginsCoreAsync("Plugins", moduleCatalog, skipOncePluginIds, onPluginLoading,
                async () =>
                {
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    cancellationToken.ThrowIfCancellationRequested();
                });
        }

        private static PluginLoadReport LoadPlugins(
            string path,
            ModuleCatalog? moduleCatalog,
            IEnumerable<string>? skipOncePluginIds,
            Action<string>? onPluginLoading)
        {
            return LoadPluginsCoreAsync(path, moduleCatalog, skipOncePluginIds, onPluginLoading, null).GetAwaiter().GetResult();
        }

        private static async Task<PluginLoadReport> LoadPluginsCoreAsync(string path, ModuleCatalog? moduleCatalog,
            IEnumerable<string>? skipOncePluginIds, Action<string>? onPluginLoading, Func<Task>? yieldToUi)
        {
            var results = new List<PluginLoadResult>();
            Volatile.Write(ref _lastReport, PluginLoadReport.Empty);
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
            string[] directories = Directory.GetDirectories(path);
            var dependencyVersions = new Dictionary<string, Version?>(StringComparer.OrdinalIgnoreCase);
            var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
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


            foreach (var directory in directories)
            {
                if (yieldToUi != null)
                    await yieldToUi();

                string manifestPath = Path.Combine(directory, "manifest.json");
                PluginManifest manifest = null;
                string dllPath = null;
                string dirName = Path.GetFileName(directory);
                void Record(PluginLoadStatus status, string? detail = null, string? userMessage = null, Exception? error = null)
                    => results.Add(new PluginLoadResult(manifest?.Id ?? dirName, directory, status, detail, userMessage, error));
                try
                {
                    PluginInfo pluginInfo = null;

                    // Directory-level recovery decisions must run before manifest parsing so a
                    // malformed manifest can still be skipped or disabled safely.
                    if (IsRetiredPlugin(dirName))
                    {
                        log.Info($"Skipped retired plugin directory '{directory}'.");
                        Record(PluginLoadStatus.Retired);
                        continue;
                    }

                    if (plugins.TryGetValue(dirName, out pluginInfo) && !pluginInfo.Enabled)
                    {
                        log.Info($"Skipped disabled plugin directory '{directory}'.");
                        Record(PluginLoadStatus.Disabled);
                        continue;
                    }

                    if (ShouldSkipPlugin(skipOncePluginIdSet, null, dirName))
                    {
                        log.Info($"Skipped plugin directory '{directory}' for this startup.");
                        Record(PluginLoadStatus.Skipped);
                        continue;
                    }

                    if (File.Exists(manifestPath))
                    {
                        string manifestContent = File.ReadAllText(manifestPath);
                        manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestContent);
                        if (string.IsNullOrWhiteSpace(manifest.Id))
                        {
                            string detail = $"Plugin manifest is missing an Id: '{manifestPath}'.";
                            log.Warn(detail);
                            Record(PluginLoadStatus.Failed, detail);
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
                            Record(PluginLoadStatus.Retired);
                            continue;
                        }

                        if (!pluginInfo.Enabled)
                        {
                            Record(PluginLoadStatus.Disabled);
                            continue;
                        }

                        if (ShouldSkipPlugin(skipOncePluginIdSet, manifest.Id, dirName))
                        {
                            log.Info($"Skipped plugin '{manifest.Id}' for this startup.");
                            Record(PluginLoadStatus.Skipped);
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
                        string? depsDetail = null;
                        string? depsUserMessage = null;
                        Exception? depsError = null;

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
                                                depsDetail = $"Plugin dependency '{dep.Key}' was not found at '{expectedDll}'.";
                                                log.Warn(depsDetail);
                                                break;
                                            }

                                            // 获取dll实际版本
                                            try
                                            {
                                                if (!dependencyVersions.TryGetValue(expectedDll, out Version? actualVersion))
                                                {
                                                    actualVersion = AssemblyName.GetAssemblyName(expectedDll).Version;
                                                    dependencyVersions.Add(expectedDll, actualVersion);
                                                }
                                                var requiredVersion = new Version(dep.Value);

                                                if (actualVersion == null || actualVersion < requiredVersion)
                                                {
                                                    depsOk = false;
                                                    depsDetail = $"Plugin dependency '{dep.Key}' requires version {requiredVersion}, but version {actualVersion} is installed.";
                                                    depsUserMessage = string.Format(Properties.Resources.DependencyVersionInsufficient, dep.Key, requiredVersion, actualVersion);
                                                    log.ErrorExt(depsDetail);
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                depsOk = false;
                                                depsDetail = $"Plugin dependency '{dep.Key}' could not be inspected: {ex.Message}";
                                                depsUserMessage = string.Format(Properties.Resources.DependencyCheckException, dep.Key, ex.Message);
                                                depsError = ex;
                                                log.Warn(depsDetail);
                                                break;
                                            }
                                        }
                                    }
                                }

                            }
                            if (!depsOk)
                            {
                                Record(PluginLoadStatus.Failed, depsDetail ?? "Plugin dependency metadata could not be validated.", depsUserMessage, depsError);
                                continue;
                            }
                        }

                        if (IsPluginAssemblyAvailable(dllPath))
                        {
                            log.Info($"Loading plugin: {manifest.Name}");

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
                            Record(PluginLoadStatus.Loaded);
                        }
                        else
                        {
                            string detail = $"Plugin assembly was not found: '{dllPath}'.";
                            log.Warn(detail);
                            Record(PluginLoadStatus.Failed, detail);
                        }
                    }
                    else
                    {
                        dllPath = Path.Combine(directory, dirName + ".dll");
                        if (IsPluginAssemblyAvailable(dllPath))
                        {
                            Assembly assembly = Assembly.LoadFrom(dllPath);
                            moduleCatalog?.AddPlugin(dirName, assembly);
                            log.Info($"Loaded plugin without a manifest: {dllPath}");
                            Record(PluginLoadStatus.Loaded);
                        }
                        else
                        {
                            string detail = $"Plugin assembly was not found: '{dllPath}'.";
                            log.Warn(detail);
                            Record(PluginLoadStatus.Failed, detail);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    Record(PluginLoadStatus.Failed, ex.Message, string.Format(Properties.Resources.PluginLoadError, ex.Message), ex);
                }
            }

            pluginConfig.Save();
            AssemblyHandler.GetInstance().RefreshAssemblies();
            var report = new PluginLoadReport(results.ToArray());
            Volatile.Write(ref _lastReport, report);
            return report;
        }
    }
}
