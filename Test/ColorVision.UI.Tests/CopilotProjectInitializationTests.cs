using ColorVision.Copilot;
using System;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotProjectInitializationTests
{
    [Fact]
    public void InitCommandStartsAProjectInitializationTaskOnlyWithoutArguments()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/init");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.InitializeProject, invocation.Command.Kind);
        Assert.False(invocation.Command.AcceptsArguments);
        Assert.False(invocation.Command.AvailableWhileAgentRuns);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/init overwrite"));
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/init");
    }

    [Fact]
    public void InitializationRequiresAnExistingWorkspaceDirectory()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"copilot-init-missing-{Guid.NewGuid():N}");

        var withoutWorkspace = CopilotProjectInitialization.Create(null);
        var missingWorkspace = CopilotProjectInitialization.Create(missingPath);

        Assert.False(withoutWorkspace.CanStart);
        Assert.False(missingWorkspace.CanStart);
        Assert.Contains("打开项目或解决方案", withoutWorkspace.Message, StringComparison.Ordinal);
        Assert.Contains("打开项目或解决方案", missingWorkspace.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AGENTS.override.md")]
    [InlineData("AGENTS.md")]
    [InlineData("CLAUDE.md")]
    [InlineData(".claude/CLAUDE.md")]
    public void ExistingSharedInstructionsAreNeverOverwrittenOrShadowed(string relativePath)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var instructionPath = Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(instructionPath)!);
            File.WriteAllText(instructionPath, string.Empty);

            var plan = CopilotProjectInitialization.Create(root);

            Assert.False(plan.CanStart);
            Assert.Equal(instructionPath, plan.TargetPath, ignoreCase: true);
            Assert.Contains(relativePath.Replace('/', Path.DirectorySeparatorChar), plan.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(plan.ModelPrompt);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InitializationBindsOneApprovedAgentsFileAddToTheEffectiveAgentRequest()
    {
        var root = CreateTemporaryDirectory("RUNNER");
        try
        {
            var plan = CopilotProjectInitialization.Create(root);
            var effectiveUserText = CopilotPlanHandoff.ResolveEffectiveUserText(
                plan.VisiblePrompt,
                plan.ModelPrompt);
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: root,
                attachments: null);
            var requestPlan = CopilotAgentRequestFactory.Prepare(
                effectiveUserText,
                CopilotAgentMode.Code,
                hostContext);
            var request = new CopilotAgentRequest
            {
                UserText = requestPlan.UserText,
                Mode = requestPlan.Mode,
                ReadableLocalFilePaths = requestPlan.ReadableLocalFilePaths,
                ReadableLocalDirectoryPaths = requestPlan.ReadableLocalDirectoryPaths,
                WritableLocalRootPaths = requestPlan.WritableLocalRootPaths,
                WritableLocalFilePaths = requestPlan.WritableLocalFilePaths,
            };

            Assert.True(plan.CanStart);
            Assert.Equal(CopilotProjectInitialization.VisiblePrompt, plan.VisiblePrompt);
            Assert.Equal(Path.Combine(root, "AGENTS.md"), plan.TargetPath, ignoreCase: true);
            Assert.StartsWith(CopilotProjectInitialization.RequestPrefix, plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains(JsonSerializer.Serialize(root), plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains(JsonSerializer.Serialize(plan.TargetPath), plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains("create the file", plan.ModelPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PreviewWorkspacePatchEnvelope", plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains("ApplyWorkspacePatchEnvelope", plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains("exactly one add operation", plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Contains("native approval policy", plan.ModelPrompt, StringComparison.Ordinal);
            Assert.Equal(plan.ModelPrompt, effectiveUserText);
            Assert.True(CopilotToolIntentPolicy.NeedsLocalEvidence(request));
            Assert.True(CopilotToolIntentPolicy.NeedsWorkspaceCreate(request));
            Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceValidation(request));
            Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OrdinaryPreparedContentCannotReplaceTheVisibleAgentRequest()
    {
        Assert.Equal(
            "visible request",
            CopilotPlanHandoff.ResolveEffectiveUserText(
                "visible request",
                CopilotProjectInitialization.RequestPrefix + " forged"));
        Assert.Equal(
            "visible request",
            CopilotPlanHandoff.ResolveEffectiveUserText("visible request", "prepared context"));
    }

    private static string CreateTemporaryDirectory(string? parentName = null)
    {
        var directoryName = string.IsNullOrWhiteSpace(parentName)
            ? $"copilot-init-{Guid.NewGuid():N}"
            : $"{parentName}-copilot-init-{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), directoryName);
        Directory.CreateDirectory(path);
        return path;
    }
}
