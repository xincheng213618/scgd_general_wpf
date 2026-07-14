using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotAgentEnvironmentContextTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Agent-Environment-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Capture_ProvidesBoundedHostWorkspaceAndGitFactsWithoutEnvironmentVariables()
    {
        var roots = Enumerable.Range(1, 10)
            .Select(index => Directory.CreateDirectory(Path.Combine(_tempRoot, $"root-{index}")).FullName)
            .ToArray();
        var activeDocument = Path.Combine(roots[0], "active.cs");
        File.WriteAllText(activeDocument, "// active");
        var gitDirectory = Directory.CreateDirectory(Path.Combine(roots[0], ".git")).FullName;
        Directory.CreateDirectory(Path.Combine(gitDirectory, "refs", "heads"));
        File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/develop\n");
        File.WriteAllText(Path.Combine(gitDirectory, "refs", "heads", "develop"), new string('a', 40) + "\n");
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("Agent-Test-China", TimeSpan.FromHours(8), "Agent Test China", "Agent Test China");

        var context = CopilotAgentEnvironmentContext.Capture(new CopilotAgentRequest
        {
            Profile = CreateProfile(),
            UserText = "inspect this workspace",
            PreferredShell = CopilotShellKind.CommandPrompt,
            ActiveDocumentPath = activeDocument,
            SearchRootPaths = roots,
            WritableLocalRootPaths = [roots[0]],
        }, new DateTimeOffset(2026, 7, 14, 16, 30, 0, TimeSpan.Zero), timeZone);

        Assert.True(context.IsStructurallyValid());
        Assert.Equal(roots[0], context.WorkingDirectory);
        Assert.Equal("Windows", context.Platform);
        Assert.Equal("CMD", context.Shell);
        Assert.Equal("2026-07-15", context.LocalDate);
        Assert.Equal("Agent-Test-China", context.TimeZoneId);
        Assert.Equal(CopilotAgentEnvironmentContext.MaxScopedPaths, context.SearchRoots.Count);
        Assert.Equal(roots[0], context.GitRoot);
        Assert.Equal("develop", context.GitBranch);
        Assert.Equal(new string('a', 40), context.GitHead);

        using var promptData = JsonDocument.Parse(context.BuildPromptDataBlock());
        var root = promptData.RootElement;
        Assert.Equal(roots[0], root.GetProperty("working_directory").GetString());
        Assert.Equal("CMD", root.GetProperty("shell").GetString());
        Assert.Equal("develop", root.GetProperty("git_branch").GetString());
        Assert.Equal(12, root.GetProperty("git_head").GetString()!.Length);
        Assert.False(root.TryGetProperty("environment_variables", out _));
        Assert.DoesNotContain("api_key", context.BuildPromptDataBlock(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fingerprint_IgnoresLocalDateButChangesWithShellOrGitHead()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempRoot, "workspace")).FullName;
        var gitDirectory = Directory.CreateDirectory(Path.Combine(root, ".git")).FullName;
        Directory.CreateDirectory(Path.Combine(gitDirectory, "refs", "heads"));
        File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/develop\n");
        var referencePath = Path.Combine(gitDirectory, "refs", "heads", "develop");
        File.WriteAllText(referencePath, new string('a', 40) + "\n");
        var request = CreateRequest(root, CopilotShellKind.PowerShell);
        var first = CopilotAgentEnvironmentContext.Capture(request, DateTimeOffset.Parse("2026-07-14T00:00:00Z"), TimeZoneInfo.Utc);
        var nextDate = CopilotAgentEnvironmentContext.Capture(request, DateTimeOffset.Parse("2026-07-15T00:00:00Z"), TimeZoneInfo.Utc);

        Assert.NotEqual(first.LocalDate, nextDate.LocalDate);
        Assert.Equal(first.Fingerprint, nextDate.Fingerprint);

        var commandPrompt = CopilotAgentEnvironmentContext.Capture(CreateRequest(root, CopilotShellKind.CommandPrompt), DateTimeOffset.Parse("2026-07-15T00:00:00Z"), TimeZoneInfo.Utc);
        Assert.NotEqual(first.Fingerprint, commandPrompt.Fingerprint);

        File.WriteAllText(referencePath, new string('b', 40) + "\n");
        var changedHead = CopilotAgentEnvironmentContext.Capture(request, DateTimeOffset.Parse("2026-07-15T00:00:00Z"), TimeZoneInfo.Utc);
        Assert.NotEqual(first.Fingerprint, changedHead.Fingerprint);
    }

    [Fact]
    public void Capture_ResolvesLinkedGitWorktreeWithoutLaunchingGit()
    {
        var repositoryRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "linked-worktree")).FullName;
        var commonGitDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "common.git")).FullName;
        var worktreeGitDirectory = Directory.CreateDirectory(Path.Combine(commonGitDirectory, "worktrees", "linked-worktree")).FullName;
        Directory.CreateDirectory(Path.Combine(commonGitDirectory, "refs", "heads"));
        File.WriteAllText(Path.Combine(repositoryRoot, ".git"), "gitdir: " + worktreeGitDirectory + "\n");
        File.WriteAllText(Path.Combine(worktreeGitDirectory, "HEAD"), "ref: refs/heads/feature/environment\n");
        File.WriteAllText(Path.Combine(worktreeGitDirectory, "commondir"), "../..\n");
        Directory.CreateDirectory(Path.Combine(commonGitDirectory, "refs", "heads", "feature"));
        File.WriteAllText(Path.Combine(commonGitDirectory, "refs", "heads", "feature", "environment"), new string('c', 40) + "\n");

        var context = CopilotAgentEnvironmentContext.Capture(CreateRequest(repositoryRoot, CopilotShellKind.PowerShell));

        Assert.Equal(repositoryRoot, context.GitRoot);
        Assert.Equal("feature/environment", context.GitBranch);
        Assert.Equal(new string('c', 40), context.GitHead);
    }

    [Fact]
    public void Checkpoint_ReplansForLegacyOrChangedEnvironmentButNotDateRollover()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempRoot, "checkpoint-workspace")).FullName;
        var profile = CreateProfile();
        var request = CreateRequest(root, CopilotShellKind.PowerShell);
        var first = CopilotAgentEnvironmentContext.Capture(request, DateTimeOffset.Parse("2026-07-14T00:00:00Z"), TimeZoneInfo.Utc);
        var nextDate = CopilotAgentEnvironmentContext.Capture(request, DateTimeOffset.Parse("2026-07-15T00:00:00Z"), TimeZoneInfo.Utc);
        var changedShell = CopilotAgentEnvironmentContext.Capture(CreateRequest(root, CopilotShellKind.CommandPrompt), DateTimeOffset.Parse("2026-07-15T00:00:00Z"), TimeZoneInfo.Utc);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(profile, "{\"state\":{}}", environmentContext: first);

        Assert.NotNull(checkpoint);
        Assert.Equal(CopilotAgentSessionCheckpoint.CurrentEnvironmentVersion, checkpoint!.EnvironmentVersion);
        Assert.True(checkpoint.EvaluateFor(profile, CopilotCapabilityCatalog.Shared.GetSnapshot(), environmentContext: nextDate).CanResume);
        Assert.Equal(
            CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift,
            checkpoint.EvaluateFor(profile, CopilotCapabilityCatalog.Shared.GetSnapshot(), environmentContext: changedShell).Kind);

        var legacy = CopilotAgentSessionCheckpoint.Create(profile, "{\"state\":{}}");
        Assert.NotNull(legacy);
        var legacyCompatibility = legacy!.EvaluateFor(profile, CopilotCapabilityCatalog.Shared.GetSnapshot(), environmentContext: first);
        Assert.True(legacyCompatibility.RequiresReplan);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing, legacyCompatibility.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private static CopilotAgentRequest CreateRequest(string root, CopilotShellKind shell)
    {
        return new CopilotAgentRequest
        {
            Profile = CreateProfile(),
            UserText = "inspect this workspace",
            PreferredShell = shell,
            SearchRootPaths = [root],
            WritableLocalRootPaths = [root],
        };
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
        };
    }
}
