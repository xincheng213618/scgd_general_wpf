using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWorkspacePatchAgentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-Patch-" + Guid.NewGuid().ToString("N"));

    public CopilotWorkspacePatchAgentTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ApprovedPatchAndRollbackPreserveOriginalBytesAndLineEndings()
    {
        var path = CreateFile("Sample.cs", "class Sample\n{\n    int Value = 1;\n}\n");
        var originalBytes = await File.ReadAllBytesAsync(path);
        var store = new CopilotWorkspacePatchStore();
        var request = CreateEditRequest("请修改 Sample.cs", _root);
        var preview = await new CopilotPreviewWorkspacePatchTool(store).ExecuteAsync(request, new CopilotAgentToolInput
        {
            Path = path,
            Arguments = new Dictionary<string, object?>
            {
                ["oldText"] = "    int Value = 1;\r\n",
                ["newText"] = "    int Value = 2;\r\n",
            },
        }, CancellationToken.None);
        var previewId = ExtractField(preview.Content, "preview_id");
        var applyTool = new CopilotApplyWorkspacePatchTool(store);
        var applyInput = PreviewInput(previewId);

        var unapproved = await applyTool.ExecuteAsync(request, applyInput, CancellationToken.None);
        Assert.False(unapproved.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, unapproved.FailureKind);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));

        var approval = applyTool.CreateApprovalPresentation(applyInput);
        Assert.Contains(path, approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA-256", approval.Description, StringComparison.Ordinal);

        var applied = await applyTool.ExecuteApprovedAsync(request, applyInput, CancellationToken.None);
        Assert.True(applied.Success);
        var appliedBytes = await File.ReadAllBytesAsync(path);
        Assert.False(appliedBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal("class Sample\n{\n    int Value = 2;\n}\n", Encoding.UTF8.GetString(appliedBytes));

        var rollbackRequest = CreateEditRequest("回滚刚才的工作区补丁", _root);
        var rolledBack = await new CopilotRollbackWorkspacePatchTool(store).ExecuteApprovedAsync(
            rollbackRequest,
            applyInput,
            CancellationToken.None);

        Assert.True(rolledBack.Success);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ApplyRejectsFileChangedAfterPreviewWithoutOverwritingIt()
    {
        var path = CreateFile("Conflict.cs", "class Conflict { int Value = 1; }\n");
        var store = new CopilotWorkspacePatchStore();
        var request = CreateEditRequest("请修改 Conflict.cs", _root);
        var preview = await PreviewAsync(store, request, path, "Value = 1", "Value = 2");
        var previewId = ExtractField(preview.Content, "preview_id");
        const string externalChange = "class Conflict { int Value = 99; }\n";
        await File.WriteAllTextAsync(path, externalChange, new UTF8Encoding(false));

        var result = await new CopilotApplyWorkspacePatchTool(store).ExecuteApprovedAsync(
            request,
            PreviewInput(previewId),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, result.FailureKind);
        Assert.Equal(externalChange, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task PreviewRejectsOutOfScopeAndAmbiguousReplacement()
    {
        var allowedRoot = Path.Combine(_root, "allowed");
        var otherRoot = Path.Combine(_root, "other");
        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(otherRoot);
        var outOfScopePath = CreateFile(Path.Combine("other", "Outside.cs"), "class Outside { }\n");
        var ambiguousPath = CreateFile(Path.Combine("allowed", "Repeated.cs"), "token token\n");
        var store = new CopilotWorkspacePatchStore();
        var request = CreateEditRequest("请修改文件", allowedRoot);

        var outOfScope = await PreviewAsync(store, request, outOfScopePath, "Outside", "Inside");
        var ambiguous = await PreviewAsync(store, request, ambiguousPath, "token", "value");

        Assert.False(outOfScope.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, outOfScope.FailureKind);
        Assert.False(ambiguous.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, ambiguous.FailureKind);
        Assert.Contains("2 locations", ambiguous.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryExposesWorkspaceWritesOnlyForExplicitEditOrRollbackIntent()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var ordinary = CreateEditRequest("解释一下这个类", _root);
        var edit = CreateEditRequest("请修改这个类", _root);
        var editOptOut = CreateEditRequest("只解释，不要修改这个类", _root);
        var rollback = CreateEditRequest("回滚刚才的工作区补丁", _root);

        Assert.DoesNotContain(registry.FindTools(ordinary), IsWorkspacePatchTool);
        Assert.Contains(registry.FindTools(edit), tool => tool.Name == "PreviewWorkspacePatch");
        Assert.Contains(registry.FindTools(edit), tool => tool.Name == "ApplyWorkspacePatch");
        Assert.DoesNotContain(registry.FindTools(edit), tool => tool.Name == "RollbackWorkspacePatch");
        Assert.DoesNotContain(registry.FindTools(editOptOut), IsWorkspacePatchTool);
        Assert.Contains(registry.FindTools(rollback), tool => tool.Name == "RollbackWorkspacePatch");
    }

    [Fact]
    public async Task PreviewRequiresExactlyOneExplicitlyWritableExistingTextFile()
    {
        var exactPath = CreateFile(Path.Combine("external", "Notes.md"), "before\n");
        var store = new CopilotWorkspacePatchStore();
        var request = new CopilotAgentRequest
        {
            UserText = "请修改 Notes.md",
            Mode = CopilotAgentMode.Auto,
            WritableLocalFilePaths = [exactPath],
        };

        var result = await PreviewAsync(store, request, exactPath, "before", "after");

        Assert.True(result.Success);
        Assert.Contains("replacement_count: 1", result.Content, StringComparison.Ordinal);
        Assert.Equal("before\n", await File.ReadAllTextAsync(exactPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CreateFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private static CopilotAgentRequest CreateEditRequest(string text, string writableRoot)
    {
        return new CopilotAgentRequest
        {
            UserText = text,
            Mode = CopilotAgentMode.Auto,
            WritableLocalRootPaths = [writableRoot],
        };
    }

    private static Task<CopilotToolResult> PreviewAsync(
        CopilotWorkspacePatchStore store,
        CopilotAgentRequest request,
        string path,
        string oldText,
        string newText)
    {
        return store.PreviewAsync(request, new CopilotAgentToolInput
        {
            Path = path,
            Arguments = new Dictionary<string, object?>
            {
                ["oldText"] = oldText,
                ["newText"] = newText,
            },
        }, CancellationToken.None);
    }

    private static CopilotAgentToolInput PreviewInput(string previewId)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["previewId"] = previewId },
        };
    }

    private static string ExtractField(string text, string fieldName)
    {
        var line = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .First(item => item.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase));
        return line[(line.IndexOf(':') + 1)..].Trim();
    }

    private static bool IsWorkspacePatchTool(ICopilotTool tool)
    {
        return tool.Name is "PreviewWorkspacePatch" or "ApplyWorkspacePatch" or "RollbackWorkspacePatch";
    }
}
