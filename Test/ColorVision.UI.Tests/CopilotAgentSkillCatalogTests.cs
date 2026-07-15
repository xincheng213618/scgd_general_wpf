#pragma warning disable CA1707
using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSkillCatalogTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-SkillCatalog-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Discover_UsesTrustedRootsDeduplicatesAndFiltersManualOffSkills()
    {
        var projectRoot = Path.Combine(_tempRoot, "project");
        var appRoot = Path.Combine(_tempRoot, "app");
        var projectSkills = Path.Combine(projectRoot, ".agents", "skills");
        var builtInSkills = Path.Combine(appRoot, "Copilot", "Skills");
        WriteSkill(projectSkills, "shared-workflow", "Project workflow wins.");
        WriteSkill(builtInSkills, "shared-workflow", "Built-in duplicate.");
        WriteSkill(builtInSkills, "hidden-workflow", "Hidden manually.");
        WriteSkill(Path.Combine(projectSkills, "plugin-group"), "nested-workflow", "Nested plugin workflow.");
        WriteSkill(Path.Combine(projectSkills, "shared-workflow", "references"), "reference-only", "Not a top-level Skill.");

        var catalog = CopilotAgentSkillCatalog.Discover(
            [projectRoot],
            new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                ["hidden-workflow"] = CopilotAgentSkillOverrideState.Off,
            },
            appRoot);

        Assert.Equal(["nested-workflow", "shared-workflow"], catalog.Select(item => item.Name));
        Assert.Equal("Project workflow wins.", Assert.Single(catalog, item => item.Name == "shared-workflow").Description);
        Assert.DoesNotContain(catalog, item => item.Name == "reference-only");
    }

    [Fact]
    public void Discover_IgnoresInvalidAndOversizedFilesAndBoundsTheCatalog()
    {
        var projectRoot = Path.Combine(_tempRoot, "bounded-project");
        var skillsRoot = Path.Combine(projectRoot, ".agents", "skills");
        for (var index = 0; index < 70; index++)
            WriteSkill(skillsRoot, $"skill-{index:00}", "A valid bounded workflow.");

        var invalidDirectory = Path.Combine(skillsRoot, "invalid-workflow");
        Directory.CreateDirectory(invalidDirectory);
        File.WriteAllText(Path.Combine(invalidDirectory, "SKILL.md"), "name: invalid-workflow");
        var oversizedDirectory = Path.Combine(skillsRoot, "oversized-workflow");
        Directory.CreateDirectory(oversizedDirectory);
        File.WriteAllText(Path.Combine(oversizedDirectory, "SKILL.md"), new string('x', 262_145));

        var catalog = CopilotAgentSkillCatalog.Discover([projectRoot], null, Path.Combine(_tempRoot, "empty-app"));

        Assert.Equal(CopilotAgentSkillCatalog.MaxCatalogEntries, catalog.Count);
        Assert.DoesNotContain(catalog, item => item.Name is "invalid-workflow" or "oversized-workflow");
    }

    [Fact]
    public void Discover_NormalizesQuotedFrontmatterAndShortensLongDescriptions()
    {
        var projectRoot = Path.Combine(_tempRoot, "quoted-project");
        var skillDirectory = Path.Combine(projectRoot, ".agents", "skills", "quoted-workflow");
        Directory.CreateDirectory(skillDirectory);
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), $$"""
            ---
            name: "quoted-workflow"
            description: '{{new string('a', 220)}}'
            ---

            Instructions.
            """);

        var item = Assert.Single(CopilotAgentSkillCatalog.Discover([projectRoot], null, Path.Combine(_tempRoot, "empty-app")));

        Assert.Equal("quoted-workflow", item.Name);
        Assert.Equal(180, item.Description.Length);
        Assert.EndsWith("…", item.Description, StringComparison.Ordinal);
    }

    private static void WriteSkill(string root, string name, string description)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), $$"""
            ---
            name: {{name}}
            description: {{description}}
            ---

            Instructions.
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
