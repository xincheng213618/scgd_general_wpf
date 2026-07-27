using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentProjectInstructionsTests
{
    [Fact]
    public void DiscoversClaudeInstructionsWhenAgentsInstructionsAreMissing()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string claudePath = Path.Combine(root, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude project instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(claudePath, document.Path, ignoreCase: true);
            Assert.Contains("Claude project instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiscoversClaudeInstructionsFromTheClaudeProjectDirectory()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string claudeDirectory = Path.Combine(root, ".claude");
            Directory.CreateDirectory(claudeDirectory);
            string claudePath = Path.Combine(claudeDirectory, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude directory instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(claudePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentsInstructionsWinOverClaudeFallbackInTheSameDirectory()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
            Assert.DoesNotContain("Claude instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentsOverrideWinsOverOtherInstructionFallbacks()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string overridePath = Path.Combine(root, "AGENTS.override.md");
            File.WriteAllText(overridePath, "# Override instructions");
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(overridePath, document.Path, ignoreCase: true);
            Assert.Contains("Override instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyAgentsOverrideFallsBackToAgentsInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AGENTS.override.md"), "  ");
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyAgentsInstructionsFallBackToClaudeInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AGENTS.override.md"), "<!-- empty after filtering -->");
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), string.Empty);
            string claudePath = Path.Combine(root, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(claudePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeLocalInstructionsFollowTheSelectedSharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            File.WriteAllText(agentsPath, "# Shared agents instructions");
            File.WriteAllText(localPath, "# Private local instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(2, documents.Count);
            Assert.Equal(agentsPath, documents[0].Path, ignoreCase: true);
            Assert.Equal(localPath, documents[1].Path, ignoreCase: true);
            Assert.Contains("Private local instructions", documents[1].Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeLocalInstructionsLoadWithoutASharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            File.WriteAllText(localPath, "# Private local instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(localPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyClaudeLocalInstructionsDoNotReplaceOrConsumeTheSharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Shared agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.local.md"), "<!-- private note only -->");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnconditionalClaudeRulesLoadBetweenSharedAndLocalInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            string rulePath = Path.Combine(rulesDirectory, "testing.md");
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(agentsPath, "# Shared instructions");
            File.WriteAllText(rulePath, "# Testing rule");
            File.WriteAllText(localPath, "# Local instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(
                new[] { agentsPath, rulePath, localPath },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MatchingPathScopedClaudeRuleLoadsAndRemovesFrontmatter()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "api");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "Handler.ts");
        File.WriteAllText(activeDocument, "export const handler = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "api.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths:
                  - "src/api/**/*.ts"
                ---

                # API rule
                - Validate every request.
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocument));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
            Assert.Contains("# API rule", document.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("paths:", document.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("---", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NonMatchingPathScopedClaudeRuleIsNotLoaded()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "ui");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "View.tsx");
        File.WriteAllText(activeDocument, "export const view = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "api.md"),
                """
                ---
                paths:
                  - "src/api/**/*.ts"
                ---
                # API only
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeRulePathsSupportInlineListsAndBraceExpansion()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "components");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "Button.tsx");
        File.WriteAllText(activeDocument, "export const button = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "frontend.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths: ["src/**/*.{ts,tsx}", "tests/**/*.test.ts"]
                ---
                # Frontend rule
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocument));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
            Assert.Equal("# Frontend rule", document.Content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PathScopedClaudeRuleDoesNotLoadWithoutAnActiveDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "source.md"),
                """
                ---
                paths:
                  - "src/**/*.cs"
                ---
                # Source rule
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PathScopedClaudeRuleLoadsForAnExplicitRequestTarget()
    {
        string root = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);
        string targetFile = Path.Combine(sourceDirectory, "Target.cs");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "source.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths:
                  - "src/**/*.cs"
                ---
                # Source rule
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover(
                    [root],
                    activeDocumentPath: null,
                    additionalTargetFilePaths: [targetFile]));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeRulesAreDiscoveredRecursivelyInStableRelativePathOrder()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            string nestedRulesDirectory = Path.Combine(rulesDirectory, "frontend");
            Directory.CreateDirectory(nestedRulesDirectory);
            string nestedRulePath = Path.Combine(nestedRulesDirectory, "a-style.md");
            string rootRulePath = Path.Combine(rulesDirectory, "z-testing.md");
            File.WriteAllText(rootRulePath, "# Root rule");
            File.WriteAllText(nestedRulePath, "# Nested rule");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(
                new[] { nestedRulePath, rootRulePath },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../secrets/**")]
    [InlineData("/absolute/**")]
    [InlineData("C:/outside/**")]
    [InlineData("!src/generated/**")]
    public void UnsafeOrUnsupportedClaudeRulePathPatternsAreRejected(string pattern)
    {
        string root = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);
        string activeDocument = Path.Combine(sourceDirectory, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "unsafe.md"),
                $"---{Environment.NewLine}paths:{Environment.NewLine}  - \"{pattern}\"{Environment.NewLine}---{Environment.NewLine}# Unsafe rule");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedClaudeRuleFrontmatterFailsClosed()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "malformed.md"),
                """
                ---
                paths:
                  - "**/*.cs"
                # Missing closing delimiter
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IndentedClaudeRulePathsFieldFailsClosedInsteadOfBecomingGlobal()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "indented.md"),
                """
                ---
                  paths:
                    - "**/*.cs"
                ---
                # Invalid scoped rule
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NestedClaudeInstructionsFollowBroaderAgentsInstructions()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src", "feature");
        Directory.CreateDirectory(nested);
        string activeDocument = Path.Combine(nested, "Feature.cs");
        File.WriteAllText(activeDocument, "namespace Feature;");
        try
        {
            string rootInstructions = Path.Combine(root, "AGENTS.md");
            string nestedInstructions = Path.Combine(nested, "CLAUDE.md");
            File.WriteAllText(rootInstructions, "# Root instructions");
            File.WriteAllText(nestedInstructions, "# Feature instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(2, documents.Count);
            Assert.Equal(rootInstructions, documents[0].Path, ignoreCase: true);
            Assert.Equal(nestedInstructions, documents[1].Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DocumentLimitRetainsTheMostSpecificInstructions()
    {
        string root = CreateTemporaryDirectory();
        string current = root;
        var instructionPaths = new string[6];
        try
        {
            for (int index = 0; index < instructionPaths.Length; index++)
            {
                if (index > 0)
                {
                    current = Path.Combine(current, $"level-{index}");
                    Directory.CreateDirectory(current);
                }

                instructionPaths[index] = Path.Combine(current, "AGENTS.md");
                File.WriteAllText(instructionPaths[index], $"# Scope {index}");
            }
            string activeDocument = Path.Combine(current, "Target.cs");
            File.WriteAllText(activeDocument, "namespace Target;");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(CopilotAgentProjectInstructions.MaxDocuments, documents.Count);
            Assert.Equal(instructionPaths[^4..], documents.Select(document => document.Path).ToArray(), StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(documents, document => string.Equals(document.Path, instructionPaths[0], StringComparison.OrdinalIgnoreCase));
            Assert.Equal(instructionPaths[^1], documents[^1].Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DocumentLimitRetainsSpecificLocalOverlaysWithTheirSharedDocuments()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src");
        string deepest = Path.Combine(nested, "feature");
        Directory.CreateDirectory(deepest);
        string activeDocument = Path.Combine(deepest, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            foreach (var directory in new[] { root, nested, deepest })
            {
                File.WriteAllText(Path.Combine(directory, "AGENTS.md"), "# Shared " + directory);
                File.WriteAllText(Path.Combine(directory, "CLAUDE.local.md"), "# Local " + directory);
            }

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(CopilotAgentProjectInstructions.MaxDocuments, documents.Count);
            Assert.Equal(
                new[]
                {
                    Path.Combine(nested, "AGENTS.md"),
                    Path.Combine(nested, "CLAUDE.local.md"),
                    Path.Combine(deepest, "AGENTS.md"),
                    Path.Combine(deepest, "CLAUDE.local.md"),
                },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(documents, document => document.Path.StartsWith(
                    root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetDirectoryName(document.Path), root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CharacterBudgetRetainsTheMostSpecificInstructions()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src");
        string deepest = Path.Combine(nested, "feature");
        Directory.CreateDirectory(deepest);
        string activeDocument = Path.Combine(deepest, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            string rootInstructions = Path.Combine(root, "AGENTS.md");
            string nestedInstructions = Path.Combine(nested, "AGENTS.md");
            string deepestInstructions = Path.Combine(deepest, "AGENTS.md");
            File.WriteAllText(rootInstructions, "# Root\n" + new string('R', CopilotAgentProjectInstructions.MaxDocumentCharacters));
            File.WriteAllText(nestedInstructions, "# Nested\n" + new string('N', CopilotAgentProjectInstructions.MaxDocumentCharacters));
            File.WriteAllText(deepestInstructions, "# Deepest\n" + new string('D', CopilotAgentProjectInstructions.MaxDocumentCharacters));

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(2, documents.Count);
            Assert.Equal(nestedInstructions, documents[0].Path, ignoreCase: true);
            Assert.Equal(deepestInstructions, documents[1].Path, ignoreCase: true);
            Assert.DoesNotContain(documents, document => string.Equals(document.Path, rootInstructions, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeImportsAreNotExpandedDuringDiscovery()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "private.md"), "PRIVATE_IMPORT_CONTENT");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "@private.md");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal("@private.md", document.Content);
            Assert.DoesNotContain("PRIVATE_IMPORT_CONTENT", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PromptBlockExplainsCompatibilityFallbackLocalOverlayAndSafetyBoundary()
    {
        string prompt = CopilotAgentProjectInstructions.BuildPromptBlock(
        [
            new CopilotProjectInstructionDocument
            {
                Path = @"C:\workspace\CLAUDE.md",
                Content = "# Build instructions",
            },
        ]);

        Assert.Contains("CLAUDE.md", prompt, StringComparison.Ordinal);
        Assert.Contains("compatibility fallback", prompt, StringComparison.Ordinal);
        Assert.Contains("CLAUDE.local.md", prompt, StringComparison.Ordinal);
        Assert.Contains("private project overlay", prompt, StringComparison.Ordinal);
        Assert.Contains(".claude/rules/**/*.md", prompt, StringComparison.Ordinal);
        Assert.Contains("paths frontmatter", prompt, StringComparison.Ordinal);
        Assert.Contains("never authorize a write", prompt, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
