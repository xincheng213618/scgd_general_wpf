using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWorkspaceChangeSetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-ChangeSet-" + Guid.NewGuid().ToString("N"));

    public CopilotWorkspaceChangeSetTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ApprovedChangeSetAppliesAndRollsBackExistingAndNewFilesTogether()
    {
        var existingPath = CreateFile("Existing.cs", "class Existing { int Value = 1; }\n");
        var newPath = Path.Combine(_root, "Generated", "NewFile.cs");
        var originalBytes = await File.ReadAllBytesAsync(existingPath);
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改 Existing.cs 并创建 NewFile.cs");
        var patchPreviewId = await PreviewPatchAsync(store, request, existingPath, "Value = 1", "Value = 2");
        var createPreviewId = await PreviewCreateAsync(store, request, newPath, "namespace Generated;\npublic sealed class NewFile;\n");
        var changeSetPreview = await new CopilotPreviewWorkspaceChangeSetTool(store).ExecuteAsync(
            request,
            PreviewListInput(patchPreviewId, createPreviewId),
            CancellationToken.None);
        var changeSetId = ExtractField(changeSetPreview.Content, "change_set_id");
        var changeSetInput = ChangeSetInput(changeSetId);
        var applyTool = new CopilotApplyWorkspaceChangeSetTool(store);

        Assert.True(changeSetPreview.Success);
        Assert.Equal(changeSetId, applyTool.GetConcurrencyKey(request, changeSetInput));
        Assert.Contains("file_count: 2", changeSetPreview.Content, StringComparison.Ordinal);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(existingPath));
        Assert.False(File.Exists(newPath));

        var unapproved = await applyTool.ExecuteAsync(request, changeSetInput, CancellationToken.None);
        Assert.False(unapproved.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, unapproved.FailureKind);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(existingPath));
        Assert.False(File.Exists(newPath));

        var approval = applyTool.CreateApprovalPresentation(changeSetInput);
        Assert.Contains(existingPath, approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(newPath, approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Every file is revalidated", approval.Description, StringComparison.Ordinal);

        var applied = await applyTool.ExecuteApprovedAsync(request, changeSetInput, CancellationToken.None);
        Assert.True(applied.Success);
        Assert.Contains("state: Applied", applied.Content, StringComparison.Ordinal);
        Assert.Equal("class Existing { int Value = 2; }\n", await File.ReadAllTextAsync(existingPath));
        Assert.Equal("namespace Generated;\npublic sealed class NewFile;\n", await File.ReadAllTextAsync(newPath));

        var rollbackTool = new CopilotRollbackWorkspaceChangeSetTool(store);
        var rolledBack = await rollbackTool.ExecuteApprovedAsync(
            Request("回滚刚才的多文件修改"),
            changeSetInput,
            CancellationToken.None);

        Assert.True(rolledBack.Success);
        Assert.Contains("state: RolledBack", rolledBack.Content, StringComparison.Ordinal);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(existingPath));
        Assert.False(File.Exists(newPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(newPath)));
    }

    [Fact]
    public async Task WholeSetConflictIsDetectedBeforeAnyFileIsWritten()
    {
        var firstPath = CreateFile("First.cs", "class First { int Value = 1; }\n");
        var secondPath = CreateFile("Second.cs", "class Second { int Value = 1; }\n");
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改两个文件");
        var firstPreviewId = await PreviewPatchAsync(store, request, firstPath, "Value = 1", "Value = 2");
        var secondPreviewId = await PreviewPatchAsync(store, request, secondPath, "Value = 1", "Value = 2");
        var changeSet = await new CopilotPreviewWorkspaceChangeSetTool(store).ExecuteAsync(
            request,
            PreviewListInput(firstPreviewId, secondPreviewId),
            CancellationToken.None);
        await File.WriteAllTextAsync(secondPath, "class Second { int Value = 99; }\n", new UTF8Encoding(false));

        var result = await new CopilotApplyWorkspaceChangeSetTool(store).ExecuteApprovedAsync(
            request,
            ChangeSetInput(ExtractField(changeSet.Content, "change_set_id")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, result.FailureKind);
        Assert.Equal("class First { int Value = 1; }\n", await File.ReadAllTextAsync(firstPath));
        Assert.Equal("class Second { int Value = 99; }\n", await File.ReadAllTextAsync(secondPath));

        var firstOnly = await new CopilotApplyWorkspacePatchTool(store).ExecuteApprovedAsync(
            request,
            PreviewInput(firstPreviewId),
            CancellationToken.None);
        Assert.True(firstOnly.Success);
        Assert.Equal("class First { int Value = 2; }\n", await File.ReadAllTextAsync(firstPath));
    }

    [Fact]
    public async Task ReservedChildPreviewCannotBypassChangeSetApproval()
    {
        var firstPath = CreateFile("One.cs", "one\n");
        var secondPath = CreateFile("Two.cs", "two\n");
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改多个文件");
        var firstPreviewId = await PreviewPatchAsync(store, request, firstPath, "one", "ONE");
        var secondPreviewId = await PreviewPatchAsync(store, request, secondPath, "two", "TWO");
        var changeSet = await new CopilotPreviewWorkspaceChangeSetTool(store).ExecuteAsync(
            request,
            PreviewListInput(firstPreviewId, secondPreviewId),
            CancellationToken.None);

        var bypass = await new CopilotApplyWorkspacePatchTool(store).ExecuteApprovedAsync(
            request,
            PreviewInput(firstPreviewId),
            CancellationToken.None);

        Assert.True(changeSet.Success);
        Assert.False(bypass.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, bypass.FailureKind);
        Assert.Contains("reserved by a multi-file change set", bypass.Summary, StringComparison.Ordinal);
        Assert.Equal("one\n", await File.ReadAllTextAsync(firstPath));
        Assert.Equal("two\n", await File.ReadAllTextAsync(secondPath));
    }

    [Fact]
    public async Task CancellationDuringWholeSetValidationKeepsPreviewReusable()
    {
        var firstPath = CreateFile("CancelOne.cs", "one\n");
        var secondPath = CreateFile("CancelTwo.cs", "two\n");
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改两个文件");
        var firstPreviewId = await PreviewPatchAsync(store, request, firstPath, "one", "ONE");
        var secondPreviewId = await PreviewPatchAsync(store, request, secondPath, "two", "TWO");
        var changeSet = await new CopilotPreviewWorkspaceChangeSetTool(store).ExecuteAsync(
            request,
            PreviewListInput(firstPreviewId, secondPreviewId),
            CancellationToken.None);
        var input = ChangeSetInput(ExtractField(changeSet.Content, "change_set_id"));
        var applyTool = new CopilotApplyWorkspaceChangeSetTool(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            applyTool.ExecuteApprovedAsync(request, input, cancellation.Token));

        var applied = await applyTool.ExecuteApprovedAsync(request, input, CancellationToken.None);
        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.Equal("ONE\n", await File.ReadAllTextAsync(firstPath));
        Assert.Equal("TWO\n", await File.ReadAllTextAsync(secondPath));
    }

    [Fact]
    public void RegistryExposesChangeSetToolsOnlyForMatchingWriteIntent()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        var ordinaryTools = registry.FindTools(Request("解释这些文件"));
        var editTools = registry.FindTools(Request("请修改多个文件"));
        var createTools = registry.FindTools(Request("请创建文件 A.cs 和 B.cs"));
        var rollbackTools = registry.FindTools(Request("回滚刚才的多文件修改"));

        Assert.DoesNotContain(ordinaryTools, IsChangeSetTool);
        Assert.Contains(editTools, tool => tool.Name == "PreviewWorkspaceChangeSet");
        Assert.Contains(editTools, tool => tool.Name == "ApplyWorkspaceChangeSet");
        Assert.Contains(createTools, tool => tool.Name == "PreviewWorkspaceChangeSet");
        Assert.Contains(createTools, tool => tool.Name == "ApplyWorkspaceChangeSet");
        Assert.Contains(rollbackTools, tool => tool.Name == "RollbackWorkspaceChangeSet");
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

    private CopilotAgentRequest Request(string userText)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            WritableLocalRootPaths = [_root],
        };
    }

    private static async Task<string> PreviewPatchAsync(
        CopilotWorkspacePatchStore store,
        CopilotAgentRequest request,
        string path,
        string oldText,
        string newText)
    {
        var result = await store.PreviewAsync(request, new CopilotAgentToolInput
        {
            Path = path,
            Arguments = new Dictionary<string, object?>
            {
                ["oldText"] = oldText,
                ["newText"] = newText,
            },
        }, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        return ExtractField(result.Content, "preview_id");
    }

    private static async Task<string> PreviewCreateAsync(
        CopilotWorkspacePatchStore store,
        CopilotAgentRequest request,
        string path,
        string content)
    {
        var result = await store.PreviewCreateAsync(request, new CopilotAgentToolInput
        {
            Path = path,
            Arguments = new Dictionary<string, object?> { ["content"] = content },
        }, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        return ExtractField(result.Content, "preview_id");
    }

    private static CopilotAgentToolInput PreviewListInput(params string[] previewIds)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["previewIds"] = previewIds },
        };
    }

    private static CopilotAgentToolInput PreviewInput(string previewId)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["previewId"] = previewId },
        };
    }

    private static CopilotAgentToolInput ChangeSetInput(string changeSetId)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["changeSetId"] = changeSetId },
        };
    }

    private static string ExtractField(string text, string fieldName)
    {
        var line = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .First(item => item.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase));
        return line[(line.IndexOf(':') + 1)..].Trim();
    }

    private static bool IsChangeSetTool(ICopilotTool tool)
    {
        return tool.Name is "PreviewWorkspaceChangeSet" or "ApplyWorkspaceChangeSet" or "RollbackWorkspaceChangeSet";
    }
}
