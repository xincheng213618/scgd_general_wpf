using ColorVision.UI;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System.IO;
using System.Reflection;

namespace ColorVision.Solution.Explorer
{
    public sealed record SolutionFileProject(
        string Path,
        string? SolutionFolderPath,
        IReadOnlyDictionary<string, string> Configurations);

    public sealed record SolutionFileFolder(
        string Path,
        string Name,
        string? ParentPath,
        IReadOnlyList<string> Files);

    /// <summary>
    /// Provider-neutral representation imported from an external solution file.
    /// The source remains read-only; SolutionManager projects this definition to
    /// a private .cvsln workspace containing ColorVision-specific UI state.
    /// </summary>
    public sealed record SolutionFileDefinition(
        string ProviderId,
        string Name,
        FileInfo SourceFile,
        DirectoryInfo RootDirectory,
        IReadOnlyList<SolutionFileProject> Projects,
        IReadOnlyList<SolutionFileFolder> Folders,
        IReadOnlyList<string> Configurations);

    public interface ISolutionFileProvider
    {
        string Id { get; }

        IReadOnlyList<string> SolutionFilePatterns { get; }

        bool CanLoad(FileInfo solutionFile);

        SolutionFileDefinition Load(FileInfo solutionFile);

        Task<SolutionFileDefinition> LoadAsync(
            FileInfo solutionFile,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => Load(solutionFile), cancellationToken);
        }
    }

    public sealed record SolutionFileLoadResult(
        bool Succeeded,
        SolutionFileDefinition? Definition,
        string ErrorMessage = "");

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SolutionFileProviderAttribute : Attribute
    {
        public int Priority { get; }

        public SolutionFileProviderAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Uses Microsoft's shared solution persistence model for both legacy .sln
    /// and modern .slnx instead of maintaining a second parser in ColorVision.
    /// </summary>
    [SolutionFileProvider(100)]
    public sealed class VisualStudioSolutionFileProvider : ISolutionFileProvider
    {
        public const string ProviderId = "visualstudio.solution";

        public string Id => ProviderId;
        public IReadOnlyList<string> SolutionFilePatterns { get; } = ["*.sln", "*.slnx"];

        public bool CanLoad(FileInfo solutionFile)
        {
            if (!solutionFile.Exists)
                return false;

            string extension = solutionFile.Extension;
            return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase);
        }

        public SolutionFileDefinition Load(FileInfo solutionFile)
        {
            return LoadAsync(solutionFile, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<SolutionFileDefinition> LoadAsync(
            FileInfo solutionFile,
            CancellationToken cancellationToken)
        {
            ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(solutionFile.FullName);
            if (serializer == null)
                throw new NotSupportedException($"没有可读取“{solutionFile.Name}”的 Visual Studio 解决方案序列化器。");

            SolutionModel model = await serializer.OpenAsync(solutionFile.FullName, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo rootDirectory = solutionFile.Directory
                ?? throw new InvalidDataException("解决方案文件没有有效的父目录。");
            string platform = model.Platforms.Count > 0 ? model.Platforms[0] : string.Empty;
            IReadOnlyList<string> configurations = model.BuildTypes.Count > 0
                ? model.BuildTypes.ToList()
                : ["Debug", "Release"];

            List<SolutionFileProject> projects = model.SolutionProjects
                .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
                .Select(project => new SolutionFileProject(
                    project.FilePath,
                    project.Parent?.Path,
                    configurations.ToDictionary(
                        configuration => configuration,
                        configuration => GetProjectConfiguration(project, configuration, platform),
                        StringComparer.OrdinalIgnoreCase)))
                .ToList();
            List<SolutionFileFolder> folders = model.SolutionFolders
                .Select(folder => new SolutionFileFolder(
                    folder.Path,
                    folder.ActualDisplayName,
                    folder.Parent?.Path,
                    (folder.Files ?? Array.Empty<string>()).ToList()))
                .ToList();

            return new SolutionFileDefinition(
                Id,
                Path.GetFileNameWithoutExtension(solutionFile.Name),
                solutionFile,
                rootDirectory,
                projects,
                folders,
                configurations);
        }

        private static string GetProjectConfiguration(
            SolutionProjectModel project,
            string solutionConfiguration,
            string solutionPlatform)
        {
            var projectConfiguration = project.GetProjectConfiguration(
                solutionConfiguration,
                solutionPlatform);
            string? buildType = projectConfiguration.Item1;
            return string.IsNullOrWhiteSpace(buildType) ? solutionConfiguration : buildType;
        }
    }

    public static class SolutionFileProviderRegistry
    {
        private sealed record Registration(ISolutionFileProvider Provider, int Priority);

        private static readonly List<Registration> _providers = new();
        private static readonly HashSet<Assembly> _registeredAssemblies = new();
        private static readonly object _syncRoot = new();
        private static string[] _solutionFilePatterns = [];
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

        public static void Register(ISolutionFileProvider provider, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.Id))
                throw new ArgumentException("解决方案文件 Provider Id 不允许为空。", nameof(provider));
            lock (_syncRoot)
                RegisterCore(provider, priority);
            ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        public static bool IsSupportedSolutionFilePath(string? path)
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

            return GetSolutionFilePatternSnapshot().Any(pattern =>
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                    pattern,
                    fileName,
                    ignoreCase: true));
        }

        public static IReadOnlyList<string> GetSolutionFilePatterns()
        {
            return GetSolutionFilePatternSnapshot().ToArray();
        }

        public static string GetSolutionFileDialogPattern()
        {
            IReadOnlyList<string> patterns = GetSolutionFilePatterns();
            return patterns.Count == 0 ? "*.*" : string.Join(';', patterns);
        }

        public static bool TryLoadSolution(
            FileInfo solutionFile,
            out SolutionFileDefinition? definition,
            out string errorMessage)
        {
            SolutionFileLoadResult result = LoadSolutionAsync(solutionFile, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            definition = result.Definition;
            errorMessage = result.ErrorMessage;
            return result.Succeeded;
        }

        public static async Task<SolutionFileLoadResult> LoadSolutionAsync(
            FileInfo solutionFile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solutionFile);
            Initialize();
            cancellationToken.ThrowIfCancellationRequested();
            solutionFile.Refresh();
            if (!solutionFile.Exists)
            {
                return new SolutionFileLoadResult(
                    false,
                    null,
                    $"解决方案文件不存在：{solutionFile.FullName}");
            }

            var providerErrors = new List<string>();
            Registration[] providers;
            lock (_syncRoot)
                providers = _providers.ToArray();

            foreach (Registration registration in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool canLoad;
                try
                {
                    canLoad = registration.Provider.CanLoad(solutionFile);
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
                    Task<SolutionFileDefinition> loadTask = registration.Provider
                        .LoadAsync(solutionFile, cancellationToken);
                    SolutionFileDefinition? definition = await loadTask
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (definition != null)
                        return new SolutionFileLoadResult(true, definition);
                    providerErrors.Add($"Provider“{registration.Provider.Id}”没有返回解决方案定义。");
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

            string errorMessage = providerErrors.Count > 0
                ? string.Join(Environment.NewLine, providerErrors)
                : IsSupportedSolutionFilePath(solutionFile.FullName)
                    ? $"没有已安装的解决方案文件 Provider 能加载“{solutionFile.Name}”。"
                    : $"没有解决方案文件 Provider 声明支持“{solutionFile.Name}”。";
            return new SolutionFileLoadResult(false, null, errorMessage);
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
                var attribute = type.GetCustomAttribute<SolutionFileProviderAttribute>();
                if (attribute == null
                    || !typeof(ISolutionFileProvider).IsAssignableFrom(type)
                    || type.IsAbstract
                    || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    RegisterCore((ISolutionFileProvider)Activator.CreateInstance(type)!, attribute.Priority);
                    changed = true;
                }
                catch
                {
                }
            }
            return changed;
        }

        private static void RegisterCore(ISolutionFileProvider provider, int priority)
        {
            _providers.RemoveAll(item => string.Equals(
                item.Provider.Id,
                provider.Id,
                StringComparison.OrdinalIgnoreCase));
            _providers.Add(new Registration(provider, priority));
            _providers.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            _solutionFilePatterns = CreateSolutionFilePatterns();
        }

        private static string[] GetSolutionFilePatternSnapshot()
        {
            Initialize();
            return Volatile.Read(ref _solutionFilePatterns);
        }

        private static string[] CreateSolutionFilePatterns()
        {
            return _providers
                .SelectMany(registration => registration.Provider.SolutionFilePatterns ?? Array.Empty<string>())
                .Select(NormalizeSolutionFilePattern)
                .Where(pattern => pattern != null)
                .Select(pattern => pattern!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string? NormalizeSolutionFilePattern(string? pattern)
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
