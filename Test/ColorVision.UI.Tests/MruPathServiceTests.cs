using ColorVision.Solution.Mru;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class MruPathServiceTests
{
    [Fact]
    public void TouchMovesPathToFrontAndDeduplicatesCaseInsensitivePath()
    {
        string path = Path.Combine(Path.GetTempPath(), "ColorVision", "Workspace");
        DateTimeOffset now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryMruPathStore(
        [
            new MruPathEntry(path.ToUpperInvariant(), now.AddMinutes(-1)),
        ]);
        var service = new MruPathService(store, utcNow: () => now);

        bool changed = service.Touch(path.ToLowerInvariant());

        Assert.True(changed);
        Assert.Single(service.Items);
        Assert.Equal(Path.GetFullPath(path.ToLowerInvariant()), service.Items[0].Path);
        Assert.Equal(now, service.Items[0].LastUsedUtc);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void TouchRemovesAliasesInOneUpdate()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColorVision", "MruAliases");
        string projectPath = Path.Combine(root, "Project.cvproj");
        string workspacePath = Path.Combine(root, "Workspace");
        var store = new MemoryMruPathStore(
        [
            new MruPathEntry(projectPath, DateTimeOffset.UtcNow.AddMinutes(-1)),
        ]);
        var service = new MruPathService(store);
        int changeCount = 0;
        service.Changed += (_, _) => changeCount++;

        service.Touch(workspacePath, projectPath);

        Assert.Single(service.Items);
        Assert.Equal(Path.GetFullPath(workspacePath), service.Items[0].Path);
        Assert.Equal(1, changeCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void PinnedItemIsNotEvictedByCapacity()
    {
        DateTimeOffset now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        string root = Path.Combine(Path.GetTempPath(), "ColorVision", "MruCapacity");
        var store = new MemoryMruPathStore(
        [
            new MruPathEntry(Path.Combine(root, "Pinned"), now.AddHours(-2), true),
            new MruPathEntry(Path.Combine(root, "Recent"), now.AddHours(-1)),
            new MruPathEntry(Path.Combine(root, "Old"), now.AddHours(-3)),
        ]);

        var service = new MruPathService(store, capacity: 2, utcNow: () => now);
        service.Touch(Path.Combine(root, "Newest"));

        Assert.Equal(2, service.Items.Count);
        Assert.True(service.Items[0].IsPinned);
        Assert.EndsWith("Pinned", service.Items[0].Path);
        Assert.EndsWith("Newest", service.Items[1].Path);
    }

    [Fact]
    public void SetPinnedMovesItemIntoPinnedSection()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColorVision", "MruPin");
        var store = new MemoryMruPathStore(
        [
            new MruPathEntry(Path.Combine(root, "Recent"), DateTimeOffset.UtcNow),
            new MruPathEntry(Path.Combine(root, "Older"), DateTimeOffset.UtcNow.AddMinutes(-1)),
        ]);
        var service = new MruPathService(store);

        bool changed = service.SetPinned(Path.Combine(root, "Older"), true);

        Assert.True(changed);
        Assert.True(service.Items[0].IsPinned);
        Assert.EndsWith("Older", service.Items[0].Path);
    }

    [Fact]
    public void RemoveAcceptsMultiplePathsAndRaisesOneChange()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColorVision", "MruRemove");
        var store = new MemoryMruPathStore(
        [
            new MruPathEntry(Path.Combine(root, "One"), DateTimeOffset.UtcNow),
            new MruPathEntry(Path.Combine(root, "Two"), DateTimeOffset.UtcNow.AddMinutes(-1)),
        ]);
        var service = new MruPathService(store);
        int changeCount = 0;
        service.Changed += (_, _) => changeCount++;

        bool changed = service.Remove(
            Path.Combine(root, "One"),
            Path.Combine(root, "Two"));

        Assert.True(changed);
        Assert.Empty(service.Items);
        Assert.Equal(1, changeCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void JsonStoreRoundTripsEntriesAndIgnoresInvalidJson()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ColorVision-Mru-{Guid.NewGuid():N}");
        string filePath = Path.Combine(root, "mru.json");
        try
        {
            var store = new JsonMruPathStore(filePath);
            MruPathEntry expected = new(
                Path.Combine(root, "Workspace"),
                new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero),
                true);

            store.Save([expected]);
            MruPathEntry actual = Assert.Single(store.Load());

            Assert.Equal(expected, actual);
            File.WriteAllText(filePath, "not-json", Encoding.UTF8);
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class MemoryMruPathStore(IEnumerable<MruPathEntry> entries) : IMruPathStore
    {
        private readonly IReadOnlyList<MruPathEntry> _entries = entries.ToList();

        public int SaveCount { get; private set; }

        public IReadOnlyList<MruPathEntry> Load() => _entries;

        public void Save(IReadOnlyList<MruPathEntry> entries)
        {
            SaveCount++;
        }
    }
}
