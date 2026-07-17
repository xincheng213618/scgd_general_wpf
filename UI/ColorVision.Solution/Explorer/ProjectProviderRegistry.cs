using ColorVision.UI;
using ColorVision.Solution.Terminal;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Solution.Explorer
{
    public static class ProjectProviderRegistry
    {
        private sealed record Registration(IProjectProvider Provider, int Priority);

        private static readonly List<Registration> _providers = new();
        private static readonly HashSet<Assembly> _registeredAssemblies = new();
        private static readonly object _syncRoot = new();
        private static string[] _projectFilePatterns = [];
        private static bool _initialized;
        private static bool _assemblyLoadSubscribed;

        public static event EventHandler? ProvidersChanged;

        public static void Initialize()
        {
            if (_initialized)
                return;

            bool changed = false;
            lock (_syncRoot)
            {
                if (_initialized)
                    return;

                if (!_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
                    _assemblyLoadSubscribed = true;
                }

                Assembly[] assemblies = AssemblyService.Instance?.GetAssemblies()
                    ?? AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                    changed |= RegisterProvidersFromAssemblyCore(assembly);
                _initialized = true;
            }

            if (changed)
                ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(IProjectProvider provider, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.Id))
                throw new ArgumentException("项目 Provider Id 不允许为空。", nameof(provider));
            lock (_syncRoot)
            {
                RegisterCore(provider, priority);
            }
            ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            bool changed;
            lock (_syncRoot)
                changed = RegisterProvidersFromAssemblyCore(e.LoadedAssembly);
            if (changed)
                ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        private static bool RegisterProvidersFromAssemblyCore(Assembly assembly)
        {
            if (!_registeredAssemblies.Add(assembly))
                return false;

            bool changed = false;
            foreach (Type type in GetLoadableTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<ProjectProviderAttribute>();
                if (attribute == null
                    || !typeof(IProjectProvider).IsAssignableFrom(type)
                    || type.IsAbstract
                    || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    RegisterCore((IProjectProvider)Activator.CreateInstance(type)!, attribute.Priority);
                    changed = true;
                }
                catch
                {
                }
            }
            return changed;
        }

        private static void RegisterCore(IProjectProvider provider, int priority)
        {
            _providers.RemoveAll(item => string.Equals(item.Provider.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
            _providers.Add(new Registration(provider, priority));
            _providers.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            _projectFilePatterns = CreateProjectFilePatterns();
        }

        public static bool TryLoadProject(DirectoryInfo directory, out ProjectDefinition? project)
        {
            return TryLoadProject(directory, out project, out _);
        }

        public static bool TryLoadProject(
            DirectoryInfo directory,
            out ProjectDefinition? project,
            out string errorMessage)
        {
            ProjectLoadResult result = LoadProjectAsync(directory).GetAwaiter().GetResult();
            project = result.Project;
            errorMessage = result.ErrorMessage;
            return result.Succeeded;
        }

        public static async Task<ProjectLoadResult> LoadProjectAsync(
            DirectoryInfo directory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(directory);
            directory.Refresh();
            if (!directory.Exists)
                return new ProjectLoadResult(false, ErrorMessage: $"项目目录不存在：{directory.FullName}");

            Task<List<FileInfo>> enumerationTask = Task.Run(
                () => EnumerateProjectFiles(directory, SearchOption.TopDirectoryOnly).ToList(),
                CancellationToken.None);
            List<FileInfo> projectFiles = await enumerationTask
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            var errors = new List<string>();
            foreach (FileInfo projectFile in projectFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProjectLoadResult result = await LoadProjectAsync(projectFile, cancellationToken)
                    .ConfigureAwait(false);
                if (result.Succeeded)
                    return result;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    errors.Add(result.ErrorMessage);
            }
            return new ProjectLoadResult(
                false,
                ErrorMessage: errors.Count > 0
                    ? string.Join(Environment.NewLine, errors)
                    : $"目录“{directory.FullName}”中没有已注册 Provider 支持的项目文件。");
        }

        public static bool TryLoadProject(FileInfo projectFile, out ProjectDefinition? project)
        {
            return TryLoadProject(projectFile, out project, out _);
        }

        public static bool TryLoadProject(
            FileInfo projectFile,
            out ProjectDefinition? project,
            out string errorMessage)
        {
            ProjectLoadResult result = LoadProjectAsync(projectFile).GetAwaiter().GetResult();
            project = result.Project;
            errorMessage = result.ErrorMessage;
            return result.Succeeded;
        }

        public static async Task<ProjectLoadResult> LoadProjectAsync(
            FileInfo projectFile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(projectFile);
            Initialize();
            projectFile.Refresh();
            if (!projectFile.Exists)
                return new ProjectLoadResult(false, ErrorMessage: $"项目文件不存在：{projectFile.FullName}");

            Registration[] providers;
            lock (_syncRoot)
                providers = _providers.ToArray();

            var providerErrors = new List<string>();
            foreach (Registration registration in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool canLoad;
                try
                {
                    Task<bool> canLoadTask = Task.Run(
                        () => registration.Provider.CanLoad(projectFile),
                        CancellationToken.None);
                    canLoad = await canLoadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    providerErrors.Add($"Provider“{registration.Provider.Id}”识别失败：{ex.Message}");
                    continue;
                }
                if (!canLoad)
                    continue;

                try
                {
                    ProjectDefinition project = await registration.Provider
                        .LoadAsync(projectFile, cancellationToken)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (project != null)
                        return new ProjectLoadResult(true, project);
                    providerErrors.Add($"Provider“{registration.Provider.Id}”没有返回项目定义。");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    providerErrors.Add($"Provider“{registration.Provider.Id}”加载失败：{ex.Message}");
                }
                break;
            }

            string? declaredProviderId = GetDeclaredProviderId(projectFile);
            return new ProjectLoadResult(
                false,
                ErrorMessage: providerErrors.Count > 0
                    ? string.Join(Environment.NewLine, providerErrors)
                    : IsSupportedProjectFilePath(projectFile.FullName)
                        ? $"没有已安装的项目 Provider 能加载“{projectFile.Name}”。"
                            + (declaredProviderId == null ? string.Empty : $"项目声明需要 Provider“{declaredProviderId}”；")
                            + "可能缺少对应插件，或项目类型标识不受支持。"
                        : $"没有项目 Provider 声明支持文件“{projectFile.Name}”。");
        }

        /// <summary>
        /// Reads the provider identity declared by the built-in .cvproj
        /// envelope without attempting to load the project. This keeps missing
        /// plugin diagnostics actionable while leaving other formats opaque.
        /// </summary>
        public static string? GetDeclaredProviderId(FileInfo projectFile)
        {
            ArgumentNullException.ThrowIfNull(projectFile);
            projectFile.Refresh();
            if (!projectFile.Exists
                || !string.Equals(projectFile.Extension, ".cvproj", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                string? projectType = JObject.Parse(File.ReadAllText(projectFile.FullName))
                    .Value<string>("ProjectType")?
                    .Trim();
                if (string.IsNullOrWhiteSpace(projectType))
                    return FolderProjectProvider.ProviderId;
                return string.Equals(projectType, "folder", StringComparison.OrdinalIgnoreCase)
                    ? FolderProjectProvider.ProviderId
                    : projectType;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        public static bool IsSupportedProjectFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string fileName;
            try
            {
                fileName = Path.GetFileName(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return GetProjectFilePatternSnapshot().Any(pattern =>
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                    pattern,
                    fileName,
                    ignoreCase: true));
        }

        public static IReadOnlyList<string> GetProjectFilePatterns()
        {
            return GetProjectFilePatternSnapshot().ToArray();
        }

        public static IEnumerable<FileInfo> EnumerateProjectFiles(
            DirectoryInfo directory,
            SearchOption searchOption)
        {
            if (!directory.Exists)
                return Array.Empty<FileInfo>();

            var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (string pattern in GetProjectFilePatterns())
            {
                try
                {
                    foreach (FileInfo file in directory.EnumerateFiles(pattern, searchOption))
                        files.TryAdd(file.FullName, file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            return files.Values.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string GetProjectFileDialogPattern()
        {
            IReadOnlyList<string> patterns = GetProjectFilePatterns();
            return patterns.Count == 0 ? "*.*" : string.Join(';', patterns);
        }

        private static string? NormalizeProjectFilePattern(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;

            string normalized = pattern.Trim();
            if (normalized.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
                return null;
            if (!normalized.Contains('*') && !normalized.Contains('?'))
                normalized = normalized.StartsWith('.') ? $"*{normalized}" : $"*.{normalized.TrimStart('.')}";
            return normalized;
        }

        private static string[] GetProjectFilePatternSnapshot()
        {
            Initialize();
            return Volatile.Read(ref _projectFilePatterns);
        }

        private static string[] CreateProjectFilePatterns()
        {
            return _providers
                .Select(registration => registration.Provider)
                .OfType<IProjectFileFormatProvider>()
                .SelectMany(provider => provider.ProjectFilePatterns ?? Array.Empty<string>())
                .Select(NormalizeProjectFilePattern)
                .Where(pattern => pattern != null)
                .Select(pattern => pattern!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<ProjectCapabilityDescriptor> GetCapabilities(ProjectDefinition project)
        {
            return FindCapabilityProvider(project.ProviderId)?.GetCapabilities(project)
                ?? Array.Empty<ProjectCapabilityDescriptor>();
        }

        public static bool CanExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            return FindCapabilityProvider(project.ProviderId)?.CanExecuteCapability(project, capabilityId) == true;
        }

        public static bool ExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            return FindCapabilityProvider(project.ProviderId)?.ExecuteCapability(project, capabilityId) == true;
        }

        public static bool HasCapability(ProjectDefinition project, string capabilityId)
        {
            return GetCapabilities(project).Any(capability =>
                string.Equals(capability.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryCreateCapabilityInvocation(
            ProjectDefinition project,
            string capabilityId,
            out ProjectCommandInvocation? invocation)
        {
            Initialize();
            IProjectCommandProvider? provider;
            lock (_syncRoot)
            {
                provider = _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, project.ProviderId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectCommandProvider;
            }
            if (provider != null)
                return provider.TryCreateInvocation(project, capabilityId, out invocation);

            invocation = null;
            return false;
        }

        public static bool CanChangeProjectItemMembership(ProjectDefinition project, string fullPath)
        {
            return FindItemMutationProvider(project.ProviderId)?.CanChangeItemMembership(project, fullPath) == true;
        }

        public static bool TrySetProjectItemMembership(
            ProjectDefinition project,
            IReadOnlyList<string> fullPaths,
            bool included,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            IProjectItemMutationProvider? provider = FindItemMutationProvider(project.ProviderId);
            if (provider != null)
            {
                return provider.TrySetItemMembership(
                    project,
                    fullPaths,
                    included,
                    out updatedProject,
                    out errorMessage);
            }

            updatedProject = null;
            errorMessage = $"项目 Provider“{project.ProviderId}”不支持修改项目项。";
            return false;
        }

        public static bool CanChangeProjectDependencies(ProjectDefinition project)
        {
            return FindDependencyMutationProvider(project.ProviderId)?.CanChangeDependencies(project) == true;
        }

        public static bool TrySetProjectDependencies(
            ProjectDefinition project,
            IReadOnlyList<string> dependencies,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            IProjectDependencyMutationProvider? provider = FindDependencyMutationProvider(project.ProviderId);
            if (provider != null)
            {
                return provider.TrySetDependencies(
                    project,
                    dependencies,
                    out updatedProject,
                    out errorMessage);
            }

            updatedProject = null;
            errorMessage = $"项目 Provider“{project.ProviderId}”不支持修改项目依赖。";
            return false;
        }

        private static IProjectItemMutationProvider? FindItemMutationProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectItemMutationProvider;
            }
        }

        private static IProjectDependencyMutationProvider? FindDependencyMutationProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectDependencyMutationProvider;
            }
        }

        private static IProjectCapabilityProvider? FindCapabilityProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectCapabilityProvider;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
