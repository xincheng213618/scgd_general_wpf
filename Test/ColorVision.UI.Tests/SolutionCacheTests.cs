using ColorVision.Solution.Explorer;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Reflection;
using Xunit;

namespace ColorVision.UI.Tests;

public sealed class SolutionCacheTests
{
    [Theory]
    [InlineData("Workspace.cvsln")]
    [InlineData("Workspace.cvsln.bak")]
    [InlineData("Workspace.cvsln.save.tmp")]
    [InlineData("Other.cvsln.cache.db-journal")]
    [InlineData("Other.cvsln.cache.db-wal")]
    [InlineData("Other.cvsln.cache.db-shm")]
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

    [Theory]
    [InlineData("Workspace.cvsln.bak", true)]
    [InlineData("Workspace.cvsln.cache.db-journal", false)]
    [InlineData("Workspace.cvsln.cache.db-wal", false)]
    [InlineData("Workspace.cvsln.cache.db-shm", false)]
    public void LegacyInternalEntriesAreRepairedWithoutChangingWorkspaceFiles(string internalName, bool physicalFileExists)
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("visible.txt");
        string internalFile = Path.Combine(fixture.Root, internalName);
        if (physicalFileExists) fixture.WriteFile(internalName);
        using (SolutionCache cache = new(fixture.SolutionPath))
        {
            cache.AddFile(visible, fixture.Root);
            fixture.InsertLegacyFile(fixture.SolutionPath);
            fixture.InsertLegacyFile(internalFile);
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
        if (physicalFileExists) Assert.Equal("fixture", File.ReadAllText(internalFile));
        else Assert.False(File.Exists(internalFile));
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

    [Theory]
    [InlineData("assets.cvsln.bak")]
    [InlineData("assets.cvsln.cache.db-journal")]
    [InlineData("assets.cvsln.cache.db-wal")]
    [InlineData("assets.cvsln.cache.db-shm")]
    [InlineData("Workspace.cvsln.cache.db-wal")]
    [InlineData("Workspace.cvsln.cache.db-shm")]
    public void AnInternalFileSuffixDoesNotHideARealDirectory(string directoryName)
    {
        using CacheFixture fixture = new();
        string directory = Directory.CreateDirectory(Path.Combine(fixture.Root, directoryName)).FullName;
        using SolutionCache cache = new(fixture.SolutionPath);
        cache.AddDirectory(directory, fixture.Root);

        Assert.True(cache.ValidateDirectory(fixture.Root));
        Assert.True(Assert.Single(cache.GetChildren(fixture.Root)).IsDirectory);

        Directory.Delete(directory);
        cache.Remove(directory);
        Assert.Empty(cache.GetChildren(fixture.Root));
        Assert.True(cache.ValidateDirectory(fixture.Root));
    }

    [Fact]
    public void RebuildingBesideTheDatabaseLeavesRootAndChildCachesValid()
    {
        using CacheFixture fixture = new();
        string child = Directory.CreateDirectory(Path.Combine(fixture.Root, "assets.cvsln.cache.db-journal")).FullName;
        fixture.WriteFile(Path.Combine("assets.cvsln.cache.db-journal", "child.txt"));
        fixture.WriteFile("visible.txt");
        fixture.WriteFile("notes.db-journal");
        using (SolutionCache cache = new(fixture.SolutionPath))
        {
            cache.RebuildCache(fixture.Root);

            Assert.True(cache.ValidateDirectory(fixture.Root));
            Assert.True(cache.ValidateDirectory(child));
            Assert.Equal(new[] { "assets.cvsln.cache.db-journal", "notes.db-journal", "visible.txt" }, cache.GetChildren(fixture.Root).Select(entry => entry.Name));
            Assert.Equal("child.txt", Assert.Single(cache.GetChildren(child)).Name);
        }

        using SolutionCache reopened = new(fixture.SolutionPath);
        Assert.True(reopened.ValidateDirectory(fixture.Root));
        Assert.True(reopened.ValidateDirectory(child));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-journal")]
    [InlineData("-wal")]
    [InlineData("-shm")]
    public void DeletingTheOwnedDatabaseOrSidecarDoesNotWriteSql(string suffix)
    {
        using CacheFixture fixture = new();
        string visible = fixture.WriteFile("visible.txt");
        using SolutionCache cache = new(fixture.SolutionPath);
        cache.AddFile(visible, fixture.Root);
        var database = (SqlSugarClient)typeof(SolutionCache).GetField("_db", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        int writes = 0;
        database.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                || sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                || sql.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
                writes++;
        };

        cache.Remove((fixture.SolutionPath + ".cache.db" + suffix).ToUpperInvariant());

        Assert.Equal(0, writes);
        Assert.Equal("visible.txt", Assert.Single(cache.GetChildren(fixture.Root)).Name);
        cache.Remove(visible);
        Assert.True(writes > 0);
        Assert.Empty(cache.GetChildren(fixture.Root));
        Assert.Equal("fixture", File.ReadAllText(visible));
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
