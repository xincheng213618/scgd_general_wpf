using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWorkspacePatchEnvelopeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-Envelope-" + Guid.NewGuid().ToString("N"));

    public CopilotWorkspacePatchEnvelopeTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task MixedEnvelopeAppliesAndRollsBackAddUpdateDeleteAsOneUnit()
    {
        var updatePath = CreateFile("Update.cs", "class Update { int Value = 1; }\n");
        var deletePath = CreateFile("Delete.cs", "class Delete;\n");
        var addPath = Path.Combine(_root, "Generated", "Added.cs");
        var originalUpdate = await File.ReadAllBytesAsync(updatePath);
        var originalDelete = await File.ReadAllBytesAsync(deletePath);
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改 Update.cs、删除 Delete.cs 并添加 Added.cs");
        var previewTool = new CopilotPreviewWorkspacePatchEnvelopeTool(store);

        var preview = await previewTool.ExecuteAsync(request, EnvelopeInput(
            Update(updatePath, "Value = 1", "Value = 2"),
            Delete(deletePath),
            Add(addPath, "namespace Generated;\npublic sealed class Added;\n")), CancellationToken.None);

        Assert.True(preview.Success, preview.ErrorMessage);
        Assert.Equal("PreviewWorkspacePatchEnvelope", preview.ToolName);
        Assert.Contains("file_count: 3", preview.Content, StringComparison.Ordinal);
        Assert.Contains("file_1_operation: Replace", preview.Content, StringComparison.Ordinal);
        Assert.Contains("file_2_operation: Delete", preview.Content, StringComparison.Ordinal);
        Assert.Contains("file_3_operation: Create", preview.Content, StringComparison.Ordinal);
        Assert.Equal(originalUpdate, await File.ReadAllBytesAsync(updatePath));
        Assert.Equal(originalDelete, await File.ReadAllBytesAsync(deletePath));
        Assert.False(File.Exists(addPath));

        var changeSetInput = ChangeSetInput(ExtractField(preview.Content, "change_set_id"));
        var applyTool = new CopilotApplyWorkspacePatchEnvelopeTool(store);
        var unapproved = await applyTool.ExecuteAsync(request, changeSetInput, CancellationToken.None);
        Assert.False(unapproved.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, unapproved.FailureKind);

        var approval = applyTool.CreateApprovalPresentation(changeSetInput);
        Assert.Contains(updatePath, approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(deletePath, approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(addPath, approval.Description, StringComparison.OrdinalIgnoreCase);

        var applied = await applyTool.ExecuteApprovedAsync(request, changeSetInput, CancellationToken.None);
        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.Equal("ApplyWorkspacePatchEnvelope", applied.ToolName);
        Assert.Equal("class Update { int Value = 2; }\n", await File.ReadAllTextAsync(updatePath));
        Assert.False(File.Exists(deletePath));
        Assert.Equal("namespace Generated;\npublic sealed class Added;\n", await File.ReadAllTextAsync(addPath));

        var rollback = await new CopilotRollbackWorkspacePatchEnvelopeTool(store).ExecuteApprovedAsync(
            Request("回滚刚才的多个文件修改"),
            changeSetInput,
            CancellationToken.None);

        Assert.True(rollback.Success, rollback.ErrorMessage);
        Assert.Equal("RollbackWorkspacePatchEnvelope", rollback.ToolName);
        Assert.Equal(originalUpdate, await File.ReadAllBytesAsync(updatePath));
        Assert.Equal(originalDelete, await File.ReadAllBytesAsync(deletePath));
        Assert.False(File.Exists(addPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(addPath)));
    }

    [Fact]
    public async Task WholeEnvelopeConflictPreventsEarlierWritesWhenDeleteTargetChanges()
    {
        var updatePath = CreateFile("Stable.cs", "stable = false;\n");
        var deletePath = CreateFile("Changed.cs", "delete me\n");
        var store = new CopilotWorkspacePatchStore();
        var request = Request("修改并删除多个文件");
        var preview = await new CopilotPreviewWorkspacePatchEnvelopeTool(store).ExecuteAsync(
            request,
            EnvelopeInput(Update(updatePath, "false", "true"), Delete(deletePath)),
            CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        await File.WriteAllTextAsync(deletePath, "changed after preview\n", new UTF8Encoding(false));

        var applied = await new CopilotApplyWorkspacePatchEnvelopeTool(store).ExecuteApprovedAsync(
            request,
            ChangeSetInput(ExtractField(preview.Content, "change_set_id")),
            CancellationToken.None);

        Assert.False(applied.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, applied.FailureKind);
        Assert.Equal("stable = false;\n", await File.ReadAllTextAsync(updatePath));
        Assert.Equal("changed after preview\n", await File.ReadAllTextAsync(deletePath));
    }

    [Fact]
    public async Task SingleDeleteEnvelopeRollbackNeverOverwritesRecreatedPath()
    {
        var deletePath = CreateFile("Recreated.cs", "original\n");
        var store = new CopilotWorkspacePatchStore();
        var request = Request("删除 Recreated.cs");
        var preview = await new CopilotPreviewWorkspacePatchEnvelopeTool(store).ExecuteAsync(
            request,
            EnvelopeInput(Delete(deletePath)),
            CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        Assert.Contains("file_count: 1", preview.Content, StringComparison.Ordinal);
        var input = ChangeSetInput(ExtractField(preview.Content, "change_set_id"));

        var applied = await new CopilotApplyWorkspacePatchEnvelopeTool(store).ExecuteApprovedAsync(
            request, input, CancellationToken.None);
        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.False(File.Exists(deletePath));
        await File.WriteAllTextAsync(deletePath, "external replacement\n", new UTF8Encoding(false));

        var rolledBack = await new CopilotRollbackWorkspacePatchEnvelopeTool(store).ExecuteApprovedAsync(
            Request("回滚刚才的文件删除"), input, CancellationToken.None);

        Assert.False(rolledBack.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, rolledBack.FailureKind);
        Assert.Equal("external replacement\n", await File.ReadAllTextAsync(deletePath));
    }

    [Fact]
    public async Task DeleteRejectsStandaloneWritableFileWithoutRollbackRoot()
    {
        var deletePath = CreateFile("Standalone.cs", "keep me\n");
        var request = new CopilotAgentRequest
        {
            UserText = "删除文件 Standalone.cs",
            Mode = CopilotAgentMode.Auto,
            WritableLocalFilePaths = [deletePath],
        };

        var preview = await new CopilotPreviewWorkspacePatchEnvelopeTool(new CopilotWorkspacePatchStore()).ExecuteAsync(
            request,
            EnvelopeInput(Delete(deletePath)),
            CancellationToken.None);

        Assert.False(preview.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, preview.FailureKind);
        Assert.Contains("rollback path", preview.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep me\n", await File.ReadAllTextAsync(deletePath));
    }

    [Fact]
    public void RegistryPrefersUnifiedEnvelopeWithoutExposingItToOrdinaryQuestions()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        var ordinary = registry.FindTools(Request("解释一下文件补丁是什么"));
        var edit = registry.FindTools(Request("请修改多个文件并删除旧文件"));
        var delete = registry.FindTools(Request("删除文件 Old.cs"));
        var rollback = registry.FindTools(Request("回滚刚才的多个文件修改"));

        Assert.DoesNotContain(ordinary, tool => tool.Name.Contains("PatchEnvelope", StringComparison.Ordinal));
        Assert.Contains(edit, tool => tool.Name == "PreviewWorkspacePatchEnvelope");
        Assert.Contains(edit, tool => tool.Name == "ApplyWorkspacePatchEnvelope");
        Assert.Contains(delete, tool => tool.Name == "PreviewWorkspacePatchEnvelope");
        Assert.Contains(delete, tool => tool.Name == "ApplyWorkspacePatchEnvelope");
        Assert.Contains(rollback, tool => tool.Name == "RollbackWorkspacePatchEnvelope");
        Assert.True(edit.FindIndex(tool => tool.Name == "ApplyWorkspacePatchEnvelope")
            < edit.FindIndex(tool => tool.Name == "ApplyWorkspaceChangeSet"));
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

    private static Dictionary<string, object?> Add(string path, string content) => new()
    {
        ["operation"] = "add",
        ["path"] = path,
        ["content"] = content,
    };

    private static Dictionary<string, object?> Update(string path, string oldText, string newText) => new()
    {
        ["operation"] = "update",
        ["path"] = path,
        ["oldText"] = oldText,
        ["newText"] = newText,
    };

    private static Dictionary<string, object?> Delete(string path) => new()
    {
        ["operation"] = "delete",
        ["path"] = path,
    };

    private static CopilotAgentToolInput EnvelopeInput(params Dictionary<string, object?>[] operations)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["operations"] = operations },
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
}

internal static class CopilotToolListExtensions
{
    public static int FindIndex(this IReadOnlyList<ICopilotTool> tools, Func<ICopilotTool, bool> predicate)
    {
        for (var index = 0; index < tools.Count; index++)
        {
            if (predicate(tools[index]))
                return index;
        }
        return -1;
    }
}
