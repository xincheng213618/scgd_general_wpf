using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSkillCatalogTests
{
    [Fact]
    public async Task CachedCatalogReloadsWhenSkillRootAppearsChangesAndIsDeleted()
    {
        var applicationBaseDirectory = CreateTemporaryDirectory();
        using var changed = new SemaphoreSlim(0);
        EventHandler handler = (_, _) => changed.Release();
        CopilotAgentSkillCatalog.CatalogChanged += handler;
        try
        {
            CopilotAgentSkillCatalog.Invalidate();
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory: applicationBaseDirectory));

            var skillDirectory = Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "live-skill");
            Directory.CreateDirectory(skillDirectory);
            var skillFilePath = Path.Combine(skillDirectory, "SKILL.md");
            File.WriteAllText(skillFilePath, CreateSkill("live-skill", "version one"));

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var added = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory: applicationBaseDirectory));
            Assert.Equal("version one", added.Description);
            Assert.True(added.IsBuiltIn);
            Assert.Equal(skillFilePath, added.SkillFilePath);
            Assert.Equal(Path.Combine(applicationBaseDirectory, "Copilot", "Skills"), added.SearchRootPath);
            Drain(changed);

            File.WriteAllText(skillFilePath, CreateSkill("live-skill", "version two"));

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var updated = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory: applicationBaseDirectory));
            Assert.Equal("version two", updated.Description);
            Drain(changed);

            Directory.Delete(skillDirectory, recursive: true);

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory: applicationBaseDirectory));
        }
        finally
        {
            CopilotAgentSkillCatalog.CatalogChanged -= handler;
            CopilotAgentSkillCatalog.Invalidate();
            DeleteTemporaryDirectory(applicationBaseDirectory);
        }
    }

    [Fact]
    public async Task CachedCatalogReloadsWhenPersonalSkillRootAppears()
    {
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        using var changed = new SemaphoreSlim(0);
        EventHandler handler = (_, _) => changed.Release();
        CopilotAgentSkillCatalog.CatalogChanged += handler;
        try
        {
            CopilotAgentSkillCatalog.Invalidate();
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory));

            WriteSkill(
                Path.Combine(userProfileDirectory, ".agents", "skills", "personal-skill"),
                "personal-skill",
                "personal description");

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var skill = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached(
                [],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory));
            Assert.Equal(CopilotAgentSkillSourceKind.User, skill.SourceKind);
            Assert.Equal(Path.Combine(userProfileDirectory, ".agents", "skills"), skill.SearchRootPath);
        }
        finally
        {
            CopilotAgentSkillCatalog.CatalogChanged -= handler;
            CopilotAgentSkillCatalog.Invalidate();
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void DiscoveryTracksProjectUserAndBuiltInSkillSources()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            WriteSkill(Path.Combine(projectRoot, ".agents", "skills", "project-skill"), "project-skill", "project description");
            WriteSkill(Path.Combine(userProfileDirectory, ".agents", "skills", "user-skill"), "user-skill", "user description");
            WriteSkill(Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "built-in-skill"), "built-in-skill", "built-in description");

            var skills = CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory);

            Assert.Collection(
                skills,
                builtIn =>
                {
                    Assert.Equal("built-in-skill", builtIn.Name);
                    Assert.True(builtIn.IsBuiltIn);
                    Assert.StartsWith(applicationBaseDirectory, builtIn.SkillFilePath, StringComparison.OrdinalIgnoreCase);
                },
                project =>
                {
                    Assert.Equal("project-skill", project.Name);
                    Assert.False(project.IsBuiltIn);
                    Assert.Equal(CopilotAgentSkillSourceKind.Project, project.SourceKind);
                    Assert.StartsWith(projectRoot, project.SkillFilePath, StringComparison.OrdinalIgnoreCase);
                },
                user =>
                {
                    Assert.Equal("user-skill", user.Name);
                    Assert.Equal(CopilotAgentSkillSourceKind.User, user.SourceKind);
                    Assert.StartsWith(userProfileDirectory, user.SkillFilePath, StringComparison.OrdinalIgnoreCase);
                });
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void ProjectSkillOverridesBuiltInSkillWithTheSameName()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            WriteSkill(Path.Combine(projectRoot, ".agents", "skills", "shared-skill"), "shared-skill", "project description");
            WriteSkill(Path.Combine(userProfileDirectory, ".agents", "skills", "shared-skill"), "shared-skill", "user description");
            WriteSkill(Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "shared-skill"), "shared-skill", "built-in description");

            var skill = Assert.Single(CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory));

            Assert.Equal("project description", skill.Description);
            Assert.Equal(CopilotAgentSkillSourceKind.Project, skill.SourceKind);
            Assert.StartsWith(projectRoot, skill.SkillFilePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void RuntimeSearchPathsUseProjectUserAndBuiltInPrecedence()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            var projectSkillRoot = Path.Combine(projectRoot, ".agents", "skills");
            var userSkillRoot = Path.Combine(userProfileDirectory, ".agents", "skills");
            var builtInSkillRoot = Path.Combine(applicationBaseDirectory, "Copilot", "Skills");
            Directory.CreateDirectory(projectSkillRoot);
            Directory.CreateDirectory(userSkillRoot);
            Directory.CreateDirectory(builtInSkillRoot);

            var paths = CopilotAgentSkills.ResolveSearchPaths(
                new CopilotAgentRequest { TrustedProjectRootPaths = [projectRoot] },
                applicationBaseDirectory,
                userProfileDirectory);

            Assert.Equal([projectSkillRoot, userSkillRoot, builtInSkillRoot], paths);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void DiagnosticsListAvailableSkillsAndForcedReloadState()
    {
        var report = CopilotAgentSkillDiagnostics.FormatReport(
            new CopilotAgentSkillUsageSnapshot(),
            metadataCharacterBudget: 1_024,
            overrides: null,
            availableSkills:
            [
                new CopilotAgentSkillCatalogItem("project-skill", "Project description"),
                new CopilotAgentSkillCatalogItem("user-skill", "User description") { SourceKind = CopilotAgentSkillSourceKind.User },
                new CopilotAgentSkillCatalogItem("built-in-skill", "Built-in description") { SourceKind = CopilotAgentSkillSourceKind.BuiltIn },
            ],
            catalogReloaded: true);

        Assert.Contains("当前可调用：3 个 Skill；已强制从磁盘重扫目录。", report, StringComparison.Ordinal);
        Assert.Contains("$built-in-skill [内置] — Built-in description", report, StringComparison.Ordinal);
        Assert.Contains("$project-skill [项目] — Project description", report, StringComparison.Ordinal);
        Assert.Contains("$user-skill [用户] — User description", report, StringComparison.Ordinal);
        Assert.Contains("本地使用证据", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Show")]
    [InlineData("reload", "Reload")]
    [InlineData("refresh", "Reload")]
    [InlineData("unknown", "Invalid")]
    public void SkillsCommandResolvesCatalogActions(string arguments, string expected)
    {
        Assert.Equal(expected, CopilotAgentSkillCommand.Resolve(arguments).ToString());

        var command = CopilotLocalCommandCatalog.FindExact("/skills");
        Assert.NotNull(command);
        Assert.True(command.AcceptsArguments);
        Assert.Contains(command.Arguments!, argument => argument.Value == "reload");
    }

    [Fact]
    public async Task MonitorIgnoresChangesOutsideTheConfiguredSkillRoot()
    {
        var parentDirectory = CreateTemporaryDirectory();
        try
        {
            var skillRoot = Path.Combine(parentDirectory, ".agents", "skills");
            using var changed = new SemaphoreSlim(0);
            using var monitor = new CopilotAgentSkillCatalogMonitor(
                () => changed.Release(),
                TimeSpan.FromMilliseconds(50));
            monitor.UpdateRoots([skillRoot]);

            var outsideDirectory = Path.Combine(parentDirectory, "outside");
            Directory.CreateDirectory(outsideDirectory);
            File.WriteAllText(Path.Combine(outsideDirectory, "SKILL.md"), CreateSkill("outside", "outside root"));

            Assert.False(await changed.WaitAsync(TimeSpan.FromMilliseconds(350)));

            var insideDirectory = Path.Combine(skillRoot, "inside");
            Directory.CreateDirectory(insideDirectory);
            File.WriteAllText(Path.Combine(insideDirectory, "SKILL.md"), CreateSkill("inside", "inside root"));

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            DeleteTemporaryDirectory(parentDirectory);
        }
    }

    private static string CreateSkill(string name, string description) =>
        $"---{Environment.NewLine}name: {name}{Environment.NewLine}description: {description}{Environment.NewLine}---{Environment.NewLine}";

    private static void WriteSkill(string directoryPath, string name, string description)
    {
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Combine(directoryPath, "SKILL.md"), CreateSkill(name, description));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ColorVision.UI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void Drain(SemaphoreSlim semaphore)
    {
        while (semaphore.Wait(0))
        {
        }
    }
}
