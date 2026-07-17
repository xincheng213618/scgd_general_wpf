using log4net;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Solution.Mru
{
    public sealed class MruPathService
    {
        private const int DefaultCapacity = 50;
        private static readonly ILog Log = LogManager.GetLogger(typeof(MruPathService));
        private readonly object _sync = new();
        private readonly IMruPathStore _store;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly int _capacity;
        private IReadOnlyList<MruPathEntry> _items = Array.Empty<MruPathEntry>();

        public IReadOnlyList<MruPathEntry> Items
        {
            get
            {
                lock (_sync)
                    return _items;
            }
        }

        public event EventHandler? Changed;

        internal MruPathService(
            IMruPathStore store,
            int capacity = DefaultCapacity,
            Func<DateTimeOffset>? utcNow = null)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            _store = store;
            _capacity = capacity;
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            try
            {
                _items = Normalize(store.Load());
            }
            catch (Exception ex) when (IsStorageException(ex))
            {
                Log.Warn("无法加载最近路径记录。", ex);
            }
        }

        public static MruPathService CreateLocal(
            string fileName,
            int capacity = DefaultCapacity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            string storagePath = Path.Combine(
                ColorVision.UI.Environments.DirLocalAppData,
                "Solution",
                fileName);
            return new MruPathService(
                new JsonMruPathStore(storagePath),
                capacity);
        }

        public bool Touch(string path, params string[] aliasesToRemove)
        {
            string? normalizedPath = NormalizePath(path);
            if (normalizedPath == null)
                return false;

            var aliases = (aliasesToRemove ?? Array.Empty<string>())
                .Select(NormalizePath)
                .Where(alias => alias != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            aliases.Remove(normalizedPath);

            return Update(entries =>
            {
                MruPathEntry? existing = entries.FirstOrDefault(
                    entry => PathsEqual(entry.Path, normalizedPath));
                entries.RemoveAll(entry => PathsEqual(entry.Path, normalizedPath)
                    || aliases.Contains(entry.Path));
                entries.Add(new MruPathEntry(
                    normalizedPath,
                    _utcNow(),
                    existing?.IsPinned ?? false));
                return true;
            });
        }

        public bool Remove(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return false;
            var normalizedPaths = paths
                .Select(NormalizePath)
                .Where(path => path != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (normalizedPaths.Count == 0)
                return false;
            return Update(entries => entries.RemoveAll(
                entry => normalizedPaths.Contains(entry.Path)) > 0);
        }

        public bool SetPinned(string path, bool isPinned)
        {
            string? normalizedPath = NormalizePath(path);
            if (normalizedPath == null)
                return false;
            return Update(entries =>
            {
                int index = entries.FindIndex(entry => PathsEqual(entry.Path, normalizedPath));
                if (index < 0 || entries[index].IsPinned == isPinned)
                    return false;
                entries[index] = entries[index] with { IsPinned = isPinned };
                return true;
            });
        }

        public bool Clear()
        {
            return Update(entries =>
            {
                if (entries.Count == 0)
                    return false;
                entries.Clear();
                return true;
            });
        }

        private bool Update(Func<List<MruPathEntry>, bool> update)
        {
            IReadOnlyList<MruPathEntry> snapshot;
            lock (_sync)
            {
                var entries = _items.ToList();
                if (!update(entries))
                    return false;
                snapshot = Normalize(entries);
                _items = snapshot;
                try
                {
                    _store.Save(snapshot);
                }
                catch (Exception ex) when (IsStorageException(ex))
                {
                    Log.Warn("无法保存最近路径记录。", ex);
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private ReadOnlyCollection<MruPathEntry> Normalize(IEnumerable<MruPathEntry> entries)
        {
            var normalizedEntries = entries
                .Select(entry => (Entry: entry, Path: NormalizePath(entry.Path)))
                .Where(item => item.Path != null)
                .GroupBy(item => item.Path!, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    MruPathEntry mostRecent = group
                        .OrderByDescending(item => item.Entry.LastUsedUtc)
                        .First()
                        .Entry;
                    return mostRecent with
                    {
                        Path = group.Key,
                        IsPinned = group.Any(item => item.Entry.IsPinned),
                    };
                })
                .ToList();

            List<MruPathEntry> pinned = normalizedEntries
                .Where(entry => entry.IsPinned)
                .OrderByDescending(entry => entry.LastUsedUtc)
                .ToList();
            IEnumerable<MruPathEntry> unpinned = normalizedEntries
                .Where(entry => !entry.IsPinned)
                .OrderByDescending(entry => entry.LastUsedUtc)
                .Take(Math.Max(0, _capacity - pinned.Count));
            return new ReadOnlyCollection<MruPathEntry>(pinned.Concat(unpinned).ToList());
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
            }
            catch (Exception ex) when (ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                return null;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
        }
    }
}
