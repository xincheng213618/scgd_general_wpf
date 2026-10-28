using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexMentionsV2FeatureTests
{
    [Fact]
    public void UnifiedCatalogCompletesSkillAsAnExplicitInvocation()
    {
        string root = CreateTemporaryDirectory();
        string skillDirectory = Path.Combine(root, "document-review");
        string skillFilePath = Path.Combine(skillDirectory, "SKILL.md");
        try
        {
            Directory.CreateDirectory(skillDirectory);
            File.WriteAllText(skillFilePath, "---\nname: document-review\ndescription: Review documents\n---");
            var skill = new CopilotAgentSkillCatalogItem("document-review", "Review documents")
            {
                DisplayName = "Document Review",
                ShortDescription = "Review the current document",
                SkillFilePath = Path.GetFullPath(skillFilePath),
            };

            var unified = CopilotComposerReferenceCatalog.SearchImmediate(
                "document",
                activeDocumentPath: null,
                includeUnifiedReferences: true,
                skills: [skill]);
            var legacy = CopilotComposerReferenceCatalog.SearchImmediate(
                "document",
                activeDocumentPath: null,
                includeUnifiedReferences: false,
                skills: [skill]);

            var reference = Assert.Single(unified.Where(candidate =>
                candidate.Kind == CopilotComposerReferenceKind.Skill));
            Assert.Equal(CopilotComposerReferenceKind.Skill, reference.Kind);
            Assert.Equal("document-review", reference.AgentSkillReference?.Name);
            Assert.Empty(legacy);
            Assert.True(CopilotComposerReferenceCatalog.TryParseMention(
                "Please use @document",
                out var mention));
            Assert.Equal(
                "Please use $document-review ",
                CopilotComposerReferenceCatalog.CompleteSkillMention(
                    "Please use @document",
                    mention,
                    reference.AgentSkillReference!.Name));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-codex-mentions-v2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
