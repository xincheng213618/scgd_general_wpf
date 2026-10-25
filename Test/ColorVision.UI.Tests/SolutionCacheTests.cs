using ColorVision.Solution.Explorer;
using Microsoft.Data.Sqlite;
using System.IO;
using Xunit;

namespace ColorVision.UI.Tests;

public sealed class SolutionCacheTests
{
    [Theory]
    [InlineData("Workspace.cvsln")]
    [InlineData("Workspace.cvsln.bak")]
    [InlineData("Workspace.cvsln.save.tmp")]
    public void IncrementalWritesKeepInternalFilesOutOfTheTreeCache(string internalName)
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("visible.txt");
        string internalFile = fixture.WriteFile(internalName);
        using SolutionCache cache = new(fixture.SolutionPath);

        cache.AddFile(visible, fixture.Root);
        cache.AddFile(internalFile, fixture.Root);

        Assert.Equal("visible.txt", Assert.Single(cache.GetChildren(fixture.Root)).Name);
        Assert.True(cache.ValidateDirectory(fixture.Root));
        Assert.Equal("fixture", File.ReadAllText(internalFile));
    }

    [Fact]
    public void LegacyInternalEntriesAreRepairedWithoutChangingWorkspaceFiles()
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("visible.txt");
        string backup = fixture.WriteFile("Workspace.cvsln.bak");
        using (SolutionCache cache = new(fixture.SolutionPath))
        {
            cache.AddFile(visible, fixture.Root);
            fixture.InsertLegacyFile(fixture.SolutionPath);
            fixture.InsertLegacyFile(backup);
            Assert.Equal(3, cache.GetChildren(fixture.Root).Count);

            Assert.True(cache.ValidateDirectory(fixture.Root));
            Assert.Equal("visible.txt", Assert.Single(cache.GetChildren(fixture.Root)).Name);
        }

        using SolutionCache reopened = new(fixture.SolutionPath);
        Assert.True(reopened.ValidateDirectory(fixture.Root));
        FileTreeCacheEntry entry = Assert.Single(reopened.GetChildren(fixture.Root));
        Assert.Equal("visible.txt", entry.Name);
        Assert.Equal(visible, entry.FullPath);
        Assert.Equal(fixture.Root, entry.ParentPath);
        Assert.False(entry.IsDirectory);
        Assert.Equal(".txt", entry.Extension);
        Assert.Equal(new FileInfo(visible).Length, entry.FileSize);
        Assert.Equal(File.GetLastWriteTimeUtc(visible).Ticks, entry.LastWriteTicks);
        Assert.Equal("fixture", File.ReadAllText(fixture.SolutionPath));
        Assert.Equal("fixture", File.ReadAllText(backup));
    }

    [Fact]
    public void RepairStillRejectsAStaleVisibleEntry()
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("old.txt");
        using SolutionCache cache = new(fixture.SolutionPath);
        cache.AddFile(visible, fixture.Root);
        fixture.InsertLegacyFile(fixture.SolutionPath);
        File.Move(visible, Path.Combine(fixture.Root, "renamed.txt"));

        Assert.False(cache.ValidateDirectory(fixture.Root));
        Assert.Equal("old.txt", Assert.Single(cache.GetChildren(fixture.Root)).Name);
    }

    [Fact]
    public void AnInternalFileSuffixDoesNotHideARealDirectory()
    {
        using CacheFixture fixture = new();
        string directory = Directory.CreateDirectory(Path.Combine(fixture.Root, "assets.cvsln.bak")).FullName;
        using SolutionCache cache = new(fixture.SolutionPath);
        cache.AddDirectory(directory, fixture.Root);

        Assert.True(cache.ValidateDirectory(fixture.Root));
        Assert.True(Assert.Single(cache.GetChildren(fixture.Root)).IsDirectory);
    }

    [Fact]
    public void AnOlderCacheSchemaIsUpgradedWithoutLosingItsVisibleRows()
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("visible.txt");
        using (SqliteConnection connection = new($"Data Source={fixture.SolutionPath}.cache.db"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE file_tree_cache (FullPath TEXT PRIMARY KEY, ParentPath TEXT, Name TEXT, IsDirectory INTEGER, Extension TEXT); INSERT INTO file_tree_cache VALUES ($path, $parent, 'visible.txt', 0, '.txt')";
            command.Parameters.AddWithValue("$path", visible);
            command.Parameters.AddWithValue("$parent", fixture.Root);
            command.ExecuteNonQuery();
        }

        using SolutionCache cache = new(fixture.SolutionPath);
        Assert.True(cache.HasCache());
        Assert.True(cache.ValidateDirectory(fixture.Root));
        FileTreeCacheEntry entry = Assert.Single(cache.GetChildren(fixture.Root));
        Assert.Equal(visible, entry.FullPath);
        Assert.Equal(0, entry.FileSize);
        Assert.Equal(0, entry.LastWriteTicks);
    }

    private sealed class CacheFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ColorVisionSolutionCacheTests", Guid.NewGuid().ToString("N"));
        public string SolutionPath => Path.Combine(Root, "Workspace.cvsln");

        public CacheFixture()
        {
            Directory.CreateDirectory(Root);
            WriteFile("Workspace.cvsln");
        }

        public string WriteFile(string name)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, "fixture");
            return path;
        }

        public void InsertLegacyFile(string path)
        {
            using SqliteConnection connection = new($"Data Source={SolutionPath}.cache.db");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO file_tree_cache (FullPath, ParentPath, Name, IsDirectory, Extension, FileSize, LastWriteTicks) VALUES ($path, $parent, $name, 0, $extension, 7, 0)";
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$parent", Root);
            command.Parameters.AddWithValue("$name", Path.GetFileName(path));
            command.Parameters.AddWithValue("$extension", Path.GetExtension(path));
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            string path = Path.GetFullPath(Root);
            string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionSolutionCacheTests")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected test workspace path.");
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }
}
