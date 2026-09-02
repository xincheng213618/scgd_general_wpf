using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexMentionsV2FeatureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnifiedCatalogFlagControlsTemplateAndMenuReferencesButAlwaysAllowsFiles(bool includeUnifiedReferences)
    {
        Assert.Equal(includeUnifiedReferences, CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
            CopilotComposerReferenceKind.Template, includeUnifiedReferences));
        Assert.Equal(includeUnifiedReferences, CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
            CopilotComposerReferenceKind.Menu, includeUnifiedReferences));
        Assert.True(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
            CopilotComposerReferenceKind.File, includeUnifiedReferences));
    }

    [Fact]
    public void DisabledCatalogStillReturnsFileCandidates()
    {
        string root = CreateTemporaryDirectory();
        string filePath = Path.Combine(root, "CopilotMentionsLegacyFile.cs");
        try
        {
            File.WriteAllText(filePath, "// legacy @ file candidate");

            var candidates = CopilotComposerReferenceCatalog.SearchImmediate(
                "CopilotMentionsLegacyFile",
                filePath,
                includeUnifiedReferences: false);

            var file = Assert.Single(candidates);
            Assert.Equal(CopilotComposerReferenceKind.File, file.Kind);
            Assert.Equal(Path.GetFullPath(filePath), file.Value);
            Assert.DoesNotContain(candidates, candidate =>
                candidate.Kind is CopilotComposerReferenceKind.Template or CopilotComposerReferenceKind.Menu);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
