using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentSkillCatalogMonitor : IDisposable
    {
        private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(180);
        private readonly Action _onChanged;
        private readonly TimeSpan _debounceDelay;
        private readonly object _sync = new();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _debounceTimer;
        private bool _disposed;

        public CopilotAgentSkillCatalogMonitor(Action onChanged, TimeSpan? debounceDelay = null)
        {
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
            if (_debounceDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(debounceDelay));

            _debounceTimer = new Timer(_ => PublishChanged(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void UpdateRoots(IEnumerable<string>? skillRootPaths)
        {
            var desiredRoots = (skillRootPaths ?? Array.Empty<string>())
                .Select(TryNormalizePath)
                .Where(path => path != null)
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock (_sync)
            {
                if (_disposed)
                    return;

                foreach (var obsoleteRoot in _watchers.Keys.Except(desiredRoots, StringComparer.OrdinalIgnoreCase).ToArray())
                {
                    _watchers.Remove(obsoleteRoot, out var watcher);
                    watcher?.Dispose();
                }

                foreach (var root in desiredRoots)
                {
                    if (_watchers.ContainsKey(root))
                        continue;

                    var watcher = TryCreateWatcher(root);
                    if (watcher != null)
                        _watchers.Add(root, watcher);
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                foreach (var watcher in _watchers.Values)
                    watcher.Dispose();
                _watchers.Clear();
            }
            _debounceTimer.Dispose();
        }

        private FileSystemWatcher? TryCreateWatcher(string skillRootPath)
        {
            try
            {
                var watchDirectory = FindNearestExistingParent(skillRootPath);
                if (watchDirectory == null)
                    return null;

                var watcher = new FileSystemWatcher(watchDirectory)
                {
                    Filter = "*",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                };
                watcher.Changed += (_, args) => OnPathChanged(skillRootPath, args.FullPath);
                watcher.Created += (_, args) => OnPathChanged(skillRootPath, args.FullPath);
                watcher.Deleted += (_, args) => OnPathChanged(skillRootPath, args.FullPath);
                watcher.Renamed += (_, args) =>
                {
                    OnPathChanged(skillRootPath, args.OldFullPath);
                    OnPathChanged(skillRootPath, args.FullPath);
                };
                watcher.Error += (_, _) => ScheduleChanged();
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"Copilot Agent Skill monitoring could not watch '{skillRootPath}': {exception.Message}");
                return null;
            }
        }

        private void OnPathChanged(string skillRootPath, string changedPath)
        {
            if (IsSkillMetadataFile(changedPath) && IsPathWithinRoot(changedPath, skillRootPath))
                ScheduleChanged();
        }

        private static bool IsSkillMetadataFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.Equals(fileName, "SKILL.md", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "SKILL.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "openai.yaml", StringComparison.OrdinalIgnoreCase);
        }

        private void ScheduleChanged()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _debounceTimer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        private void PublishChanged()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
            }

            try
            {
                _onChanged();
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Copilot Agent Skill change notification failed: {exception}");
            }
        }

        private static string? FindNearestExistingParent(string skillRootPath)
        {
            var current = Path.GetDirectoryName(skillRootPath);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(current)
                    && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(current))
                {
                    return current;
                }

                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent)
                    || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                current = parent;
            }
            return null;
        }

        private static bool IsPathWithinRoot(string path, string root)
        {
            var normalizedPath = TryNormalizePath(path);
            var normalizedRoot = TryNormalizePath(root);
            if (normalizedPath == null || normalizedRoot == null)
                return false;
            if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            var rootPrefix = Path.EndsInDirectorySeparator(normalizedRoot)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryNormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
