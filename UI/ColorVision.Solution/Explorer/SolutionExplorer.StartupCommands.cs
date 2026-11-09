using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer;

public partial class SolutionExplorer
{
    private StartupDiscovery? _startupDiscovery;

    private sealed class StartupDiscovery
    {
        public readonly HashSet<string> RemovedPaths = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<ProjectDefinition>? Projects;
        public Task? PendingTask;
        public int Version;
    }

    private IReadOnlyList<ProjectDefinition>? GetStartupProjectSnapshot()
    {
        if (Volatile.Read(ref _disposed)) return null;
        StartupDiscovery discovery = LazyInitializer.EnsureInitialized(ref _startupDiscovery, static () => new StartupDiscovery());
        lock (discovery)
        {
            if (Volatile.Read(ref _disposed)) return null;
            if (discovery.Projects == null && discovery.PendingTask == null)
            {
                int version = discovery.Version;
                discovery.PendingTask = Task.Run(() => DiscoverStartupProjectsAsync(discovery, version));
            }
            return discovery.Projects;
        }
    }

    private async Task DiscoverStartupProjectsAsync(StartupDiscovery discovery, int version)
    {
        List<ProjectDefinition> projects;
        try
        {
            projects = await DiscoverProjectsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn("加载启动项目列表失败。", ex);
            projects = [];
        }

        lock (discovery)
        {
            if (!Volatile.Read(ref _disposed) && version == discovery.Version)
            {
                if (discovery.RemovedPaths.Count > 0)
                    projects.RemoveAll(project => WasRemovedDuringStartupDiscovery(project, discovery.RemovedPaths));
                discovery.Projects = projects;
            }
            discovery.RemovedPaths.Clear();
            discovery.PendingTask = null;
        }
        RequeryStartupCommands();
    }

    private void InvalidateStartupProjectDiscovery(string? removedPath = null)
    {
        StartupDiscovery? discovery = Volatile.Read(ref _startupDiscovery);
        if (discovery == null) return;
        lock (discovery)
        {
            if (Volatile.Read(ref _disposed) || (discovery.Projects == null && discovery.PendingTask == null)) return;
            if (removedPath != null)
            {
                if (discovery.PendingTask != null)
                    discovery.RemovedPaths.Add(Path.TrimEndingDirectorySeparator(removedPath));
                if (discovery.Projects?.Any(project => IsProjectAtOrBelow(project, removedPath)) != true) return;
            }
            discovery.Version++;
            discovery.Projects = null;
        }
        RequeryStartupCommands();
    }

    private static bool WasRemovedDuringStartupDiscovery(ProjectDefinition project, HashSet<string> removedPaths)
    {
        for (string? path = project.ProjectFile.FullName; path != null; path = Path.GetDirectoryName(path))
            if (removedPaths.Contains(path)) return true;
        return false;
    }

    private static bool IsProjectAtOrBelow(ProjectDefinition project, string path)
    {
        string prefix = Path.TrimEndingDirectorySeparator(path);
        string filePath = project.ProjectFile.FullName;
        return filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (filePath.Length == prefix.Length || Path.EndsInDirectorySeparator(prefix) || filePath[prefix.Length] is '\\' or '/');
    }

    private void RequeryStartupCommands()
    {
        if (Volatile.Read(ref _disposed) || Application.Current?.Dispatcher is not { } dispatcher) return;
        if (dispatcher.CheckAccess()) CommandManager.InvalidateRequerySuggested();
        else _ = dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) CommandManager.InvalidateRequerySuggested();
        });
    }
}
