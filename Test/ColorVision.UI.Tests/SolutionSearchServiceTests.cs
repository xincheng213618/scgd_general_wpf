using ColorVision.Solution.Explorer;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SolutionSearchServiceTests
{
    [Fact]
    public async Task LoadedFileUsesItsPhysicalDisplayPathUntilItIsMissing()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"colorvision-solution-search-{Guid.NewGuid():N}"));
        string path = Path.Combine(directory.FullName, "measure.txt");
        try
        {
            File.WriteAllText(path, "fixture");
            SolutionExplorer root = CreateExplorer(directory);
            var folder = new SolutionNode { Name = "Organized", Parent = root };
            var file = new SolutionNode { Name = "measure.txt", FullPath = path, Parent = folder };
            root.VisualChildren.Add(folder);
            folder.VisualChildren.Add(file);

            SolutionSearchHit present = Assert.Single((await SolutionSearchService.SearchAsync([root], "measure.txt")).Hits);
            Assert.Equal("measure.txt", present.DisplayPath);
            Assert.Same(file, present.ExistingNode);
            Assert.Empty((await SolutionSearchService.SearchAsync([root], "organized measure")).Hits);

            File.Delete(path);

            SolutionSearchHit missing = Assert.Single((await SolutionSearchService.SearchAsync([root], "organized measure")).Hits);
            Assert.Equal(Path.Combine("Organized", "measure.txt"), missing.DisplayPath);
            Assert.Same(file, missing.ExistingNode);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory.FullName);
        }
    }

    [Fact]
    public async Task LiteralKeywordsReachTheResultLimitOnlyAfterMatchingAllTerms()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"colorvision-solution-search-{Guid.NewGuid():N}"));
        const string expectedName = "run_!%report'-target.txt";
        try
        {
            for (int index = 0; index < 8; index++)
                File.WriteAllText(Path.Combine(directory.FullName, $"run-!anythingreport'-target{index}.txt"), "fixture");
            File.WriteAllText(Path.Combine(directory.FullName, "run_!%report'-other.txt"), "fixture");
            File.WriteAllText(Path.Combine(directory.FullName, expectedName), "fixture");
            SolutionExplorer root = CreateExplorer(directory);
            using var cache = new SolutionCache(root.ConfigFileInfo.FullName);
            cache.RebuildCache(directory.FullName);
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Cache))!.SetValue(root, cache);

            SolutionSearchResult result = await SolutionSearchService.SearchAsync([root], "\"run_!%report'\" target", maxResults: 1);

            Assert.Equal(expectedName, Assert.Single(result.Hits).Name);
            Assert.False(result.IsTruncated);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            Directory.Delete(directory.FullName);
        }
    }

    [Fact]
    public async Task CachedSearchRanksNameMatchesBeforeTheCandidateLimit()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"colorvision-search-needle-{Guid.NewGuid():N}"));
        const string exactName = "target_%.txt";
        const string prefixName = "target_%.txt.previous";
        const string allTermsInName = "needle-target.log";
        try
        {
            for (int index = 0; index < 12; index++)
                File.WriteAllText(Path.Combine(directory.FullName, $"00-{exactName}-{index:D2}"), "fixture");
            foreach (string name in new[] { exactName, prefixName, allTermsInName })
                File.WriteAllText(Path.Combine(directory.FullName, name), "fixture");
            SolutionExplorer root = CreateExplorer(directory);
            using var cache = new SolutionCache(root.ConfigFileInfo.FullName);
            cache.RebuildCache(directory.FullName);
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Cache))!.SetValue(root, cache);

            SolutionSearchResult filename = await SolutionSearchService.SearchAsync([root], "TARGET_%.TXT", maxResults: 2);
            SolutionSearchResult words = await SolutionSearchService.SearchAsync([root], "target needle", maxResults: 1);

            Assert.Equal(new[] { exactName, prefixName }, filename.Hits.Select(hit => hit.Name));
            Assert.Equal(allTermsInName, Assert.Single(words.Hits).Name);
            Assert.True(filename.IsTruncated);
            Assert.True(words.IsTruncated);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            Directory.Delete(directory.FullName);
        }
    }

    [Fact]
    public async Task FileSystemSearchFindsHigherRankedMatchesInUnvisitedDirectories()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"colorvision-solution-search-{Guid.NewGuid():N}"));
        DirectoryInfo nested = directory.CreateSubdirectory("unexpanded");
        const string exactName = "target.txt";
        const string prefixName = "target.txt.previous";
        try
        {
            for (int index = 0; index < 12; index++)
                File.WriteAllText(Path.Combine(directory.FullName, $"00-{exactName}-{index:D2}"), "fixture");
            File.WriteAllText(Path.Combine(nested.FullName, exactName), "fixture");
            File.WriteAllText(Path.Combine(nested.FullName, prefixName), "fixture");

            SolutionSearchResult result = await WpfTestHost.Invoke(() => SolutionSearchService.SearchFileSystemAsync(
                CreateExplorer(directory), new FileSystemFolderNode(directory, isWorkspaceRoot: true),
                "TARGET.TXT", maxResults: 2, CancellationToken.None));

            Assert.Equal(new[] { exactName, prefixName }, result.Hits.Select(hit => hit.Name));
            Assert.True(result.IsTruncated);
        }
        finally
        {
            foreach (FileInfo file in nested.EnumerateFiles()) file.Delete();
            nested.Delete();
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Fact]
    public async Task CachedProjectSearchCountsOnlyVisibleMatchesTowardTheCandidateLimit()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"colorvision-solution-search-{Guid.NewGuid():N}"));
        const string included = "target.txt-keep.txt";
        const string stale = "target.txt-deleted.txt";
        string stalePath = Path.Combine(directory.FullName, stale);
        try
        {
            for (int index = 0; index < 12; index++)
                File.WriteAllText(Path.Combine(directory.FullName, $"target.txt-exclude-{index:D2}.txt"), "fixture");
            File.WriteAllText(Path.Combine(directory.FullName, included), "fixture");
            File.WriteAllText(stalePath, "fixture");
            SolutionExplorer root = WpfTestHost.Invoke(() =>
            {
                SolutionExplorer explorer = CreateExplorer(directory);
                explorer.Config.ProjectMode = SolutionProjectMode.Explicit;
                var definition = new ProjectDefinition("test", "Project", "1.0",
                    new FileInfo(Path.Combine(directory.FullName, "Project.cvproj")),
                    ItemRules: new ProjectItemRules(["target.txt-*"], [included, stale]));
                explorer.VisualChildren.Add(new ProjectNode(definition) { Parent = explorer });
                return explorer;
            });
            using var cache = new SolutionCache(root.ConfigFileInfo.FullName);
            cache.RebuildCache(directory.FullName);
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Cache))!.SetValue(root, cache);
            File.Delete(stalePath);

            SolutionSearchResult result = await WpfTestHost.Invoke(() => SolutionSearchService.SearchAsync([root], "target.txt", maxResults: 1));

            Assert.Equal(included, Assert.Single(result.Hits).Name);
            Assert.False(result.IsTruncated);
            Assert.DoesNotContain(cache.GetChildren(directory.FullName), entry => entry.Name == stale);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static SolutionExplorer CreateExplorer(DirectoryInfo directory)
    {
        var root = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
        root.Name = "Workspace";
        root.FullPath = directory.FullName;
        root.VisualChildren = new ObservableCollection<SolutionNode>();
        typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Config))!.SetValue(root, new SolutionConfig());
        typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(root, directory);
        typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.ConfigFileInfo))!
            .SetValue(root, new FileInfo(Path.Combine(directory.FullName, "Workspace.cvsln")));
        return root;
    }
}
