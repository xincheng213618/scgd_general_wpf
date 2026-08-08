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
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached([], overrides: null, applicationBaseDirectory));

            var skillDirectory = Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "live-skill");
            Directory.CreateDirectory(skillDirectory);
            var skillFilePath = Path.Combine(skillDirectory, "SKILL.md");
            File.WriteAllText(skillFilePath, CreateSkill("live-skill", "version one"));

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var added = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached([], overrides: null, applicationBaseDirectory));
            Assert.Equal("version one", added.Description);
            Drain(changed);

            File.WriteAllText(skillFilePath, CreateSkill("live-skill", "version two"));

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var updated = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached([], overrides: null, applicationBaseDirectory));
            Assert.Equal("version two", updated.Description);
            Drain(changed);

            Directory.Delete(skillDirectory, recursive: true);

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached([], overrides: null, applicationBaseDirectory));
        }
        finally
        {
            CopilotAgentSkillCatalog.CatalogChanged -= handler;
            CopilotAgentSkillCatalog.Invalidate();
            DeleteTemporaryDirectory(applicationBaseDirectory);
        }
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
