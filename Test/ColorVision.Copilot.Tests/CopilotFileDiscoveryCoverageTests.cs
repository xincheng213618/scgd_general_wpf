using ColorVision.Copilot.Mcp;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFileDiscoveryCoverageTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotFileDiscoveryCoverage-");

    [Theory]
    [InlineData("capability", false)]
    [InlineData("capability", true)]
    [InlineData("agent", false)]
    [InlineData("agent", true)]
    [InlineData("mcp", false)]
    [InlineData("mcp", true)]
    public async Task InaccessibleDirectoryCannotSupplyCompleteFileSearchEvidence(string surface, bool validMatch)
    {
        using var denied = new DirectoryEnumerationDenial(Path.Combine(_directory.FullName, "unavailable"));
        File.WriteAllText(Path.Combine(_directory.FullName, validMatch ? "needle-visible.log" : "ordinary.log"), "visible");
        Assert.True(Directory.Exists(denied.DirectoryPath));
        Assert.Throws<UnauthorizedAccessException>(() => Directory.GetFileSystemEntries(denied.DirectoryPath));

        string content;
        if (surface == "capability")
        {
            var result = Search();
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(validMatch ? 1 : 0, result.Matches.Count);
            Assert.False(result.ScanComplete);
            Assert.False(result.ResultsComplete);
            Assert.Empty(result.NextCursor);
            Assert.NotEmpty(result.ToCapabilityResult().PartialResultMessage);
            content = result.Content;
        }
        else if (surface == "agent")
        {
            var result = await new CopilotSearchFilesTool().ExecuteAsync(new()
            {
                Mode = CopilotAgentMode.Auto, UserText = "查找 needle 文件", SearchRootPaths = [_directory.FullName],
            }, new() { Query = "needle" }, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotEmpty(result.PartialResultMessage);
            Assert.DoesNotContain("达到上限", result.PartialResultMessage);
            content = result.Content;
        }
        else
        {
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new()
                {
                    SolutionDirectoryPath = _directory.FullName, SearchRootPaths = [_directory.FullName],
                },
            });
            var result = await dispatcher.CallAsync("search_files", new Dictionary<string, JsonElement>
            {
                ["query"] = JsonSerializer.SerializeToElement("needle"),
            }, CancellationToken.None);
            Assert.True(result.Success, result.Text);
            content = result.Text;
        }

        Assert.Contains("[Scan Complete] false", content);
        Assert.Contains("[Results Complete] false", content);
        Assert.Contains("[Search Warning]", content);
        Assert.Contains(denied.DirectoryPath, content);
        Assert.DoesNotContain("completed search scope", content);
        Assert.DoesNotContain("file scan limit", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("next_cursor:", content);
        Assert.Equal(validMatch, content.Contains("needle-visible.log", StringComparison.Ordinal));
        Assert.DoesNotContain("needle-hidden.log", content);
    }

    [Fact]
    public async Task InaccessibleDirectoryListingFailsWithoutCompleteEmptyDirectoryEvidence()
    {
        using var denied = new DirectoryEnumerationDenial(Path.Combine(_directory.FullName, "unavailable"));

        var result = await new CopilotListDirectoryTool().ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto, UserText = "列出目录", SearchRootPaths = [_directory.FullName],
        }, new() { Path = denied.DirectoryPath }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.DoesNotContain("entries_complete: true", result.Content);
        Assert.DoesNotContain("Listed the complete", result.Summary);
        Assert.Empty(result.SuggestedReadableLocalFilePaths);
    }

    [Fact]
    public void IgnoredDependenciesDoNotMakeTheDeclaredFileScopeIncomplete()
    {
        using var denied = new DirectoryEnumerationDenial(Path.Combine(_directory.FullName, "node_modules"));
        File.WriteAllText(Path.Combine(_directory.FullName, "ordinary.log"), "visible");

        var result = Search();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.ScanComplete);
        Assert.True(result.ResultsComplete);
        Assert.Empty(result.Matches);
        Assert.Empty(result.ToCapabilityResult().PartialResultMessage);
        Assert.DoesNotContain("[Search Warning]", result.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootDisappearingBeforeFileEnumerationCannotSupplyCompleteSearchEvidence(bool textSearch)
    {
        var available = Directory.CreateDirectory(Path.Combine(_directory.FullName, "available")).FullName;
        var disappearing = Directory.CreateDirectory(Path.Combine(_directory.FullName, "disappearing")).FullName;
        var visibleFile = Path.Combine(available, "needle-visible.log");
        var disappearingFile = Path.Combine(disappearing, "needle-disappearing.log");
        File.WriteAllText(visibleFile, "needle visible");
        File.WriteAllText(disappearingFile, "needle disappeared");
        IEnumerable<string> DisplayRootsAfterTheScopeChanges()
        {
            yield return available;
            yield return disappearing;
            // Change the real file system after the search accepted its roots and before
            // it starts file enumeration, without a scheduler or product-only test hook.
            var resolved = Path.GetFullPath(disappearing);
            Assert.Equal(_directory.FullName, Path.GetDirectoryName(resolved));
            File.Delete(disappearingFile);
            Directory.Delete(resolved);
        }

        string content;
        if (textSearch)
        {
            var result = CopilotGrepTextCapability.SearchWithinScope(
                [available, disappearing], DisplayRootsAfterTheScopeChanges(), "needle", null, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.False(result.ScanComplete);
            Assert.False(result.ResultsComplete);
            Assert.Equal(visibleFile, Assert.Single(result.Matches).FullPath);
            Assert.NotEmpty(result.PartialResultMessage);
            content = result.Content;
        }
        else
        {
            var result = CopilotSearchFilesCapability.SearchWithinScope(
                [available, disappearing], DisplayRootsAfterTheScopeChanges(), "needle", null, true, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.False(result.ScanComplete);
            Assert.False(result.ResultsComplete);
            Assert.Equal(visibleFile, Assert.Single(result.Matches).FullPath);
            Assert.NotEmpty(result.ToCapabilityResult().PartialResultMessage);
            content = result.Content;
        }

        Assert.False(Directory.Exists(disappearing));
        Assert.Contains("[Search Warning]", content);
        Assert.Contains(disappearing, content);
        Assert.DoesNotContain("completed search scope", content);
    }

    private CopilotFileSearchResult Search() => CopilotSearchFilesCapability.Search(
        [_directory.FullName], "needle", null, true, CancellationToken.None);

    private sealed class DirectoryEnumerationDenial : IDisposable
    {
        private readonly DirectoryInfo _directory;
        private readonly byte[] _originalAccessControl;

        public string DirectoryPath => _directory.FullName;

        public DirectoryEnumerationDenial(string path)
        {
            _directory = Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "needle-hidden.log"), "not observed");
            _originalAccessControl = FileSystemAclExtensions.GetAccessControl(_directory, AccessControlSections.Access).GetSecurityDescriptorBinaryForm();
            var deniedSecurity = new DirectorySecurity();
            deniedSecurity.SetSecurityDescriptorBinaryForm(_originalAccessControl, AccessControlSections.Access);
            using var identity = WindowsIdentity.GetCurrent();
            deniedSecurity.AddAccessRule(new FileSystemAccessRule(
                identity.User ?? throw new InvalidOperationException("The current Windows identity has no SID."),
                FileSystemRights.ListDirectory,
                AccessControlType.Deny));
            FileSystemAclExtensions.SetAccessControl(_directory, deniedSecurity);
        }

        public void Dispose()
        {
            var originalSecurity = new DirectorySecurity();
            originalSecurity.SetSecurityDescriptorBinaryForm(_originalAccessControl, AccessControlSections.Access);
            FileSystemAclExtensions.SetAccessControl(_directory, originalSecurity);
        }
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotFileDiscoveryCoverage-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
