using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalPathAuthorizationTests
{
    [Fact]
    public void AgentRequestFreezesPathAuthorizationAndEvidenceRequirements()
    {
        var searchRoots = new List<string> { @"C:\search" };
        var trustedRoots = new List<string> { @"C:\trusted" };
        var readableFiles = new List<string> { @"C:\read\file.txt" };
        var readableDirectories = new List<string> { @"C:\read" };
        var writableRoots = new List<string> { @"C:\write" };
        var writableFiles = new List<string> { @"C:\write\file.txt" };
        var requiredTools = new List<string> { "DelegateExplore" };
        var request = new CopilotAgentRequest
        {
            SearchRootPaths = searchRoots,
            TrustedProjectRootPaths = trustedRoots,
            ReadableLocalFilePaths = readableFiles,
            ReadableLocalDirectoryPaths = readableDirectories,
            WritableLocalRootPaths = writableRoots,
            WritableLocalFilePaths = writableFiles,
            RequiredSuccessfulToolNames = requiredTools,
        };

        searchRoots.Clear();
        trustedRoots.Clear();
        readableFiles.Clear();
        readableDirectories.Clear();
        writableRoots.Clear();
        writableFiles.Clear();
        requiredTools.Clear();

        Assert.Equal(@"C:\search", Assert.Single(request.SearchRootPaths));
        Assert.Equal(@"C:\trusted", Assert.Single(request.TrustedProjectRootPaths));
        Assert.Equal(@"C:\read\file.txt", Assert.Single(request.ReadableLocalFilePaths));
        Assert.Equal(@"C:\read", Assert.Single(request.ReadableLocalDirectoryPaths));
        Assert.Equal(@"C:\write", Assert.Single(request.WritableLocalRootPaths));
        Assert.Equal(@"C:\write\file.txt", Assert.Single(request.WritableLocalFilePaths));
        Assert.Equal("DelegateExplore", Assert.Single(request.RequiredSuccessfulToolNames));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)request.WritableLocalRootPaths).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)request.RequiredSuccessfulToolNames).Clear());
    }

    [Fact]
    public void ExplicitAllowListRequiresAnExactFullyQualifiedPath()
    {
        var allowedPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "copilot-explicit-path", "sample.cs"));

        Assert.True(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(
            allowedPath.ToUpperInvariant(),
            [allowedPath]));
        Assert.False(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath("sample.cs", [allowedPath]));
        Assert.False(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(
            Path.Combine(Path.GetDirectoryName(allowedPath)!, "sibling.cs"),
            [allowedPath]));
    }

    [Fact]
    public async Task BareDirectoryBeforeChinesePunctuationRemainsAuthorizedAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-local-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "sample.cs"), "namespace Sample;");
        try
        {
            var userText = $"只读审计 {root}，列出至少 30 条可验证的问题；不要修改任何文件。";
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(userText, CopilotAgentMode.Auto, hostContext);

            Assert.Equal([root], plan.ReadableLocalDirectoryPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([root], plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);

            var request = new CopilotAgentRequest
            {
                UserText = userText,
                Mode = CopilotAgentMode.Auto,
                ReadableLocalDirectoryPaths = plan.ReadableLocalDirectoryPaths,
                SearchRootPaths = plan.SearchRootPaths,
            };
            var result = await new CopilotListDirectoryTool().ExecuteAsync(
                request,
                new CopilotAgentToolInput { Path = root },
                CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains("sample.cs", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QuotedPathWithSpacesAndPunctuationDoesNotProduceAPartialBarePath()
    {
        var expected = Path.GetFullPath(@"C:\workspace folder\sample,one.cs");

        var paths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(
            "检查 “C:\\workspace folder\\sample,one.cs”，然后说明结果。");

        Assert.Equal([expected], paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkdownLinkWithForwardSlashesProducesAWindowsPath()
    {
        var expected = Path.GetFullPath(@"C:\workspace\sample.cs");

        var paths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(
            "See [sample.cs](<C:/workspace/sample.cs:42>) for the verified branch.");

        Assert.Equal([expected], paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingAbsoluteFileKeepsItsExistingParentSearchable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-missing-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var missingFile = Path.Combine(root, "GeneratedLater.cs");
        try
        {
            var plan = CopilotAgentRequestFactory.Prepare(
                $"检查文件 {missingFile}",
                CopilotAgentMode.Auto,
                new CopilotAgentHostContextSnapshot(
                    activeDocumentPath: null,
                    solutionDirectoryPath: null,
                    attachments: null));

            Assert.Equal([missingFile], plan.ReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([root], plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
