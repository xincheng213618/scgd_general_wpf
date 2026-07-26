using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotLocalPathAuthorizationTests
{
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
}
