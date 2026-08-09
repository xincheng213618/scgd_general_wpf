using ColorVision.Copilot;
using Microsoft.Agents.AI;
using Newtonsoft.Json;
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
    public async Task CachedCatalogReloadsWhenNestedProjectSkillRootAppears()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        var moduleDirectory = Path.Combine(projectRoot, "src", "module");
        Directory.CreateDirectory(moduleDirectory);
        var activeDocumentPath = Path.Combine(moduleDirectory, "Widget.cs");
        File.WriteAllText(activeDocumentPath, string.Empty);
        using var changed = new SemaphoreSlim(0);
        EventHandler handler = (_, _) => changed.Release();
        CopilotAgentSkillCatalog.CatalogChanged += handler;
        try
        {
            CopilotAgentSkillCatalog.Invalidate();
            Assert.Empty(CopilotAgentSkillCatalog.DiscoverCached(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath));

            var moduleSkillRoot = Path.Combine(moduleDirectory, ".agents", "skills");
            WriteSkill(
                Path.Combine(moduleSkillRoot, "module-skill"),
                "module-skill",
                "module description");

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            var skill = Assert.Single(CopilotAgentSkillCatalog.DiscoverCached(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath));
            Assert.Equal(CopilotAgentSkillSourceKind.Project, skill.SourceKind);
            Assert.Equal(moduleSkillRoot, skill.SearchRootPath);
        }
        finally
        {
            CopilotAgentSkillCatalog.CatalogChanged -= handler;
            CopilotAgentSkillCatalog.Invalidate();
            DeleteTemporaryDirectory(projectRoot);
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
    public void CatalogRetainsProjectUserAndBuiltInSkillsWithTheSameNameInPrecedenceOrder()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            WriteSkill(Path.Combine(projectRoot, ".agents", "skills", "shared-skill"), "shared-skill", "project description");
            WriteSkill(Path.Combine(userProfileDirectory, ".agents", "skills", "shared-skill"), "shared-skill", "user description");
            WriteSkill(Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "shared-skill"), "shared-skill", "built-in description");

            var skills = CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory);

            Assert.Collection(
                skills,
                project =>
                {
                    Assert.Equal("project description", project.Description);
                    Assert.Equal(CopilotAgentSkillSourceKind.Project, project.SourceKind);
                },
                user => Assert.Equal(CopilotAgentSkillSourceKind.User, user.SourceKind),
                builtIn => Assert.Equal(CopilotAgentSkillSourceKind.BuiltIn, builtIn.SourceKind));

            var suggestions = CopilotLocalCommandCatalog.Suggest("$shared", skills);
            Assert.Equal(3, suggestions.Count);
            Assert.All(suggestions, suggestion =>
            {
                Assert.NotNull(suggestion.AgentSkillReference);
                Assert.Equal("$shared-skill", suggestion.Name);
                Assert.Contains("来源", suggestion.Description, StringComparison.Ordinal);
            });
            Assert.Equal(3, suggestions.Select(suggestion => suggestion.AgentSkillReference!.SkillFilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void ExactPathOverrideHidesOnlyTheMatchingDuplicateSkill()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            var projectDirectory = Path.Combine(projectRoot, ".agents", "skills", "shared-skill");
            var userDirectory = Path.Combine(userProfileDirectory, ".agents", "skills", "shared-skill");
            WriteSkill(projectDirectory, "shared-skill", "project description");
            WriteSkill(userDirectory, "shared-skill", "user description");
            WriteSkill(Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "shared-skill"), "shared-skill", "built-in description");
            var userSkillPath = Path.Combine(userDirectory, "SKILL.md");

            var skills = CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory,
                pathOverrides: new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase)
                {
                    [userSkillPath] = CopilotAgentSkillOverrideState.Off,
                });

            Assert.Equal(
                [CopilotAgentSkillSourceKind.Project, CopilotAgentSkillSourceKind.BuiltIn],
                skills.Select(skill => skill.SourceKind));
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void SkillOverrideNormalizationKeepsDistinctExactPathsAndBuildsSeparateSnapshots()
    {
        var firstDirectory = CreateTemporaryDirectory();
        var secondDirectory = CreateTemporaryDirectory();
        try
        {
            var firstPath = Path.Combine(firstDirectory, "SKILL.md");
            var secondPath = Path.Combine(secondDirectory, "SKILL.md");
            var defaults = new CopilotAgentDefaultsConfig
            {
                SkillOverrides =
                [
                    new CopilotAgentSkillOverrideConfig { Name = "shared-skill", State = CopilotAgentSkillOverrideState.UserInvocableOnly },
                    new CopilotAgentSkillOverrideConfig { Name = "shared-skill", SkillFilePath = firstPath, State = CopilotAgentSkillOverrideState.Off },
                    new CopilotAgentSkillOverrideConfig { Name = "shared-skill", SkillFilePath = secondPath, State = CopilotAgentSkillOverrideState.On },
                    new CopilotAgentSkillOverrideConfig { Name = "shared-skill", SkillFilePath = "relative\\SKILL.md", State = CopilotAgentSkillOverrideState.Off },
                ],
            };

            Assert.True(defaults.EnsureValid());

            Assert.Equal(CopilotAgentSkillOverrideState.UserInvocableOnly, defaults.CreateSkillOverrideSnapshot()["shared-skill"]);
            var pathOverrides = defaults.CreateSkillPathOverrideSnapshot();
            Assert.Equal(2, pathOverrides.Count);
            Assert.Equal(CopilotAgentSkillOverrideState.Off, pathOverrides[firstPath]);
            Assert.Equal(CopilotAgentSkillOverrideState.On, pathOverrides[secondPath]);

            var request = CopilotAgentRequestFactory.Create(
                new CopilotAgentRequestPlan(),
                new CopilotAgentRequestBuildInput
                {
                    Profile = new CopilotProfileConfig(),
                    AgentDefaults = defaults,
                });
            defaults.SkillOverrides.Clear();
            Assert.Equal(2, request.SkillPathOverrides.Count);
            Assert.Equal(CopilotAgentSkillOverrideState.Off, request.SkillPathOverrides[firstPath]);
        }
        finally
        {
            DeleteTemporaryDirectory(firstDirectory);
            DeleteTemporaryDirectory(secondDirectory);
        }
    }

    [Fact]
    public void CatalogRetainsNestedProjectRootUserAndBuiltInSkillsWithTheSameName()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        var moduleDirectory = Path.Combine(projectRoot, "src", "module");
        Directory.CreateDirectory(moduleDirectory);
        var activeDocumentPath = Path.Combine(moduleDirectory, "Widget.cs");
        File.WriteAllText(activeDocumentPath, string.Empty);
        try
        {
            var moduleSkillRoot = Path.Combine(moduleDirectory, ".agents", "skills");
            WriteSkill(Path.Combine(moduleSkillRoot, "shared-skill"), "shared-skill", "module description");
            WriteSkill(Path.Combine(projectRoot, ".agents", "skills", "shared-skill"), "shared-skill", "project description");
            WriteSkill(Path.Combine(userProfileDirectory, ".agents", "skills", "shared-skill"), "shared-skill", "user description");
            WriteSkill(Path.Combine(applicationBaseDirectory, "Copilot", "Skills", "shared-skill"), "shared-skill", "built-in description");

            var skills = CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath);

            Assert.Equal(
                ["module description", "project description", "user description", "built-in description"],
                skills.Select(skill => skill.Description));
            Assert.Equal(moduleSkillRoot, skills[0].SearchRootPath);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void RuntimeUsesExactDiscoveredPathForASelectedDuplicateSkill()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var userDirectory = CreateTemporaryDirectory();
        try
        {
            var projectSkill = new TestAgentSkill("shared-skill", "project");
            var userSkill = new TestAgentSkill("shared-skill", "user");
            var unrelated = new TestAgentSkill("other-skill", "other");
            var projectSkillPath = Path.Combine(projectDirectory, "SKILL.md");
            var userSkillPath = Path.Combine(userDirectory, "SKILL.md");
            var reference = new CopilotAgentSkillReference
            {
                Name = "shared-skill",
                SkillFilePath = userSkillPath,
            };

            var selected = CopilotAgentSkills.SelectPreferredSkills(
                [projectSkill, userSkill, unrelated],
                reference,
                skill => ReferenceEquals(skill, userSkill) ? userSkillPath : projectSkillPath);

            Assert.Collection(
                selected,
                shared => Assert.Same(userSkill, shared),
                other => Assert.Same(unrelated, other));

            reference.SkillFilePath = Path.Combine(projectDirectory, "missing", "SKILL.md");
            selected = CopilotAgentSkills.SelectPreferredSkills(
                [projectSkill, userSkill, unrelated],
                reference,
                skill => ReferenceEquals(skill, userSkill) ? userSkillPath : projectSkillPath);
            Assert.Same(projectSkill, selected[0]);
        }
        finally
        {
            DeleteTemporaryDirectory(projectDirectory);
            DeleteTemporaryDirectory(userDirectory);
        }
    }

    [Fact]
    public void RuntimeSkillFilePathsMustRemainWithinTrustedNonReparseRoots()
    {
        var trustedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
        var linkedSkillDirectory = Path.Combine(trustedRoot, "linked-skill");
        try
        {
            var trustedSkillDirectory = Path.Combine(trustedRoot, "trusted-skill");
            var outsideSkillDirectory = Path.Combine(outsideRoot, "outside-skill");
            WriteSkill(trustedSkillDirectory, "trusted-skill", "trusted");
            WriteSkill(outsideSkillDirectory, "outside-skill", "outside");
            Directory.CreateSymbolicLink(linkedSkillDirectory, outsideSkillDirectory);

            Assert.True(CopilotAgentSkills.IsTrustedSkillFilePath(
                Path.Combine(trustedSkillDirectory, "SKILL.md"),
                [trustedRoot]));
            Assert.False(CopilotAgentSkills.IsTrustedSkillFilePath(
                Path.Combine(outsideSkillDirectory, "SKILL.md"),
                [trustedRoot]));
            Assert.False(CopilotAgentSkills.IsTrustedSkillFilePath(
                Path.Combine(linkedSkillDirectory, "SKILL.md"),
                [trustedRoot]));
        }
        finally
        {
            if (Directory.Exists(linkedSkillDirectory))
                Directory.Delete(linkedSkillDirectory);
            DeleteTemporaryDirectory(trustedRoot);
            DeleteTemporaryDirectory(outsideRoot);
        }
    }

    [Fact]
    public void RuntimePathPolicyOverridesTheBroaderNamePolicy()
    {
        var skillDirectory = CreateTemporaryDirectory();
        try
        {
            var skill = new TestAgentSkill("shared-skill", "shared");
            var skillPath = Path.Combine(skillDirectory, "SKILL.md");
            var selection = CopilotAgentSkillSelectionPolicy.Select(
                [skill],
                "$shared-skill run",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase)
                {
                    ["shared-skill"] = CopilotAgentSkillOverrideState.Off,
                },
                maximumCount: 4,
                maximumMetadataCharacters: 1_024,
                skillPathOverrides: new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase)
                {
                    [skillPath] = CopilotAgentSkillOverrideState.On,
                },
                skillFilePathResolver: _ => skillPath);

            Assert.Same(skill, Assert.Single(selection.SelectedSkills));
        }
        finally
        {
            DeleteTemporaryDirectory(skillDirectory);
        }
    }

    [Fact]
    public void DisabledAutomaticInstructionsStillAllowAnExplicitSkillInvocation()
    {
        var documentSkill = new TestAgentSkill("document-review", "Review documents");
        var unrelatedSkill = new TestAgentSkill("workspace-audit", "Audit workspaces");
        var historicalExplicitOnlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase);

        var implicitSelection = CopilotAgentSkillSelectionPolicy.Select(
            [documentSkill, unrelatedSkill],
            "Review this document.",
            historicalExplicitOnlyNames,
            overrides,
            maximumCount: 4,
            maximumMetadataCharacters: 1_024,
            allowImplicitSelection: false);
        var explicitSelection = CopilotAgentSkillSelectionPolicy.Select(
            [documentSkill, unrelatedSkill],
            "$document-review review this document.",
            historicalExplicitOnlyNames,
            overrides,
            maximumCount: 4,
            maximumMetadataCharacters: 1_024,
            allowImplicitSelection: false);

        Assert.Empty(implicitSelection.SelectedSkills);
        Assert.Equal(
            ["document-review", "workspace-audit"],
            implicitSelection.AutomaticInstructionsDisabledNames);
        Assert.Same(documentSkill, Assert.Single(explicitSelection.SelectedSkills));
        Assert.Equal(
            ["workspace-audit"],
            explicitSelection.AutomaticInstructionsDisabledNames);
    }

    [Fact]
    public void UnavailableMcpDependencyRequiresExplicitSkillInvocation()
    {
        var skillDirectory = CreateTemporaryDirectory();
        try
        {
            WriteSkill(skillDirectory, "document-review", "Review documents using the docs service");
            var agentsDirectory = Path.Combine(skillDirectory, "agents");
            Directory.CreateDirectory(agentsDirectory);
            File.WriteAllText(Path.Combine(agentsDirectory, "openai.yaml"), """
                dependencies:
                  tools:
                    - type: "mcp"
                      value: "docs"
                      description: "Docs MCP server"
                      transport: "streamable_http"
                      url: "https://example.test/docs-mcp"
                """);
            var skill = new TestAgentSkill("document-review", "Review documents using the docs service");
            var historicalExplicitOnlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var overrides = new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase);
            var configuredServer = new CopilotMcpClientServerConfig
            {
                Name = "docs-configured",
                Endpoint = "https://example.test/docs-mcp",
                Enabled = true,
            };

            var unavailable = CopilotAgentSkillSelectionPolicy.Select(
                [skill],
                "Review this document using the docs service.",
                historicalExplicitOnlyNames,
                overrides,
                maximumCount: 4,
                maximumMetadataCharacters: 1_024,
                implicitAvailabilityResolver: _ =>
                    CopilotAgentSkillSelectionPolicy.ResolveImplicitAvailability(skillDirectory));
            var explicitSelection = CopilotAgentSkillSelectionPolicy.Select(
                [skill],
                "$document-review review this document.",
                historicalExplicitOnlyNames,
                overrides,
                maximumCount: 4,
                maximumMetadataCharacters: 1_024,
                implicitAvailabilityResolver: _ =>
                    CopilotAgentSkillSelectionPolicy.ResolveImplicitAvailability(skillDirectory));
            var available = CopilotAgentSkillSelectionPolicy.Select(
                [skill],
                "Review this document using the docs service.",
                historicalExplicitOnlyNames,
                overrides,
                maximumCount: 4,
                maximumMetadataCharacters: 1_024,
                implicitAvailabilityResolver: _ =>
                    CopilotAgentSkillSelectionPolicy.ResolveImplicitAvailability(
                        skillDirectory,
                        [configuredServer]));

            Assert.Empty(unavailable.SelectedSkills);
            Assert.Equal(["document-review"], unavailable.UnavailableDependencyNames);
            Assert.Same(skill, Assert.Single(explicitSelection.SelectedSkills));
            Assert.Empty(explicitSelection.UnavailableDependencyNames);
            Assert.Same(skill, Assert.Single(available.SelectedSkills));
            Assert.Empty(available.UnavailableDependencyNames);
            Assert.Equal(
                CopilotAgentSkillImplicitAvailability.DependencyUnavailable,
                CopilotAgentSkillSelectionPolicy.ResolveImplicitAvailability(skillDirectory));
            Assert.Equal(
                CopilotAgentSkillImplicitAvailability.Available,
                CopilotAgentSkillSelectionPolicy.ResolveImplicitAvailability(
                    skillDirectory,
                    [configuredServer]));
        }
        finally
        {
            DeleteTemporaryDirectory(skillDirectory);
        }
    }

    [Fact]
    public void SkillReferencePersistsOnUserMessagesAndRejectsMismatchedPrompts()
    {
        var skillDirectory = CreateTemporaryDirectory();
        try
        {
            var message = new CopilotChatMessage(CopilotChatRole.User, "$shared-skill inspect this")
            {
                AgentSkillReference = new CopilotAgentSkillReference
                {
                    Name = "shared-skill",
                    SkillFilePath = Path.Combine(skillDirectory, "SKILL.md"),
                },
            };

            var json = JsonConvert.SerializeObject(message);
            var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(json)!;
            restored.EnsureValid();
            Assert.NotNull(restored.AgentSkillReference);

            restored.Content = "$another-skill inspect this";
            Assert.True(restored.EnsureValid());
            Assert.Null(restored.AgentSkillReference);
        }
        finally
        {
            DeleteTemporaryDirectory(skillDirectory);
        }
    }

    [Fact]
    public void RequestFactorySnapshotsOnlyAnExplicitlyInvokedSkillReference()
    {
        var skillDirectory = CreateTemporaryDirectory();
        try
        {
            var source = new CopilotAgentSkillReference
            {
                Name = "shared-skill",
                SkillFilePath = Path.Combine(skillDirectory, "SKILL.md"),
            };
            var input = new CopilotAgentRequestBuildInput
            {
                Profile = new CopilotProfileConfig(),
                AgentDefaults = new CopilotAgentDefaultsConfig(),
                AgentSkillReference = source,
            };

            var request = CopilotAgentRequestFactory.Create(
                new CopilotAgentRequestPlan { UserText = "Use `$shared-skill` for this review." },
                input);

            Assert.NotSame(source, request.AgentSkillReference);
            Assert.Equal(source.SkillFilePath, request.AgentSkillReference?.SkillFilePath);

            source.SkillFilePath = Path.Combine(skillDirectory, "changed", "SKILL.md");
            Assert.NotEqual(source.SkillFilePath, request.AgentSkillReference?.SkillFilePath);

            request = CopilotAgentRequestFactory.Create(
                new CopilotAgentRequestPlan { UserText = "Do not invoke a skill." },
                input);
            Assert.Null(request.AgentSkillReference);
        }
        finally
        {
            DeleteTemporaryDirectory(skillDirectory);
        }
    }

    [Fact]
    public void QueuedFollowUpRecoveryRestoresTheSelectedSkillReferenceToTheDraft()
    {
        var skillDirectory = CreateTemporaryDirectory();
        try
        {
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            var reference = new CopilotAgentSkillReference
            {
                Name = "shared-skill",
                SkillFilePath = Path.Combine(skillDirectory, "SKILL.md"),
            };
            var state = new CopilotChatState
            {
                Conversations = [conversation],
                QueuedFollowUpRecoveries =
                [
                    new CopilotQueuedFollowUpRecoveryRecord
                    {
                        RunId = "run-1",
                        ConversationId = conversation.Id,
                        ComposerState = CopilotComposerStash.Capture(
                            "$shared-skill continue",
                            0,
                            CopilotAgentMode.Auto,
                            Array.Empty<CopilotAttachmentItem>(),
                            agentSkillReference: reference),
                    },
                ],
            };

            Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));

            Assert.Equal("$shared-skill continue", conversation.DraftText);
            Assert.Equal(reference.SkillFilePath, conversation.DraftAgentSkillReference?.SkillFilePath);
            Assert.Empty(state.QueuedFollowUpRecoveries);
        }
        finally
        {
            DeleteTemporaryDirectory(skillDirectory);
        }
    }

    [Fact]
    public void RuntimeSearchPathsUseNestedProjectUserAndBuiltInPrecedence()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            var moduleDirectory = Path.Combine(projectRoot, "src", "module");
            Directory.CreateDirectory(moduleDirectory);
            var activeDocumentPath = Path.Combine(moduleDirectory, "Widget.cs");
            File.WriteAllText(activeDocumentPath, string.Empty);
            var moduleSkillRoot = Path.Combine(moduleDirectory, ".agents", "skills");
            var projectSkillRoot = Path.Combine(projectRoot, ".agents", "skills");
            var userSkillRoot = Path.Combine(userProfileDirectory, ".agents", "skills");
            var builtInSkillRoot = Path.Combine(applicationBaseDirectory, "Copilot", "Skills");
            Directory.CreateDirectory(moduleSkillRoot);
            Directory.CreateDirectory(projectSkillRoot);
            Directory.CreateDirectory(userSkillRoot);
            Directory.CreateDirectory(builtInSkillRoot);

            var paths = CopilotAgentSkills.ResolveSearchPaths(
                new CopilotAgentRequest
                {
                    TrustedProjectRootPaths = [projectRoot],
                    ActiveDocumentPath = activeDocumentPath,
                },
                applicationBaseDirectory,
                userProfileDirectory);

            Assert.Equal([moduleSkillRoot, projectSkillRoot, userSkillRoot, builtInSkillRoot], paths);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void ActiveDocumentOutsideTrustedProjectCannotContributeSkillRoots()
    {
        var projectRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            WriteSkill(Path.Combine(projectRoot, ".agents", "skills", "project-skill"), "project-skill", "project description");
            WriteSkill(Path.Combine(outsideRoot, ".agents", "skills", "outside-skill"), "outside-skill", "outside description");
            var activeDocumentPath = Path.Combine(outsideRoot, "Outside.cs");
            File.WriteAllText(activeDocumentPath, string.Empty);

            var skill = Assert.Single(CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath));

            Assert.Equal("project-skill", skill.Name);
            Assert.StartsWith(projectRoot, skill.SkillFilePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(outsideRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void DiagnosticsListAvailableSkillsAndForcedReloadState()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "project-skill", "SKILL.md");
        var userPath = Path.Combine(Path.GetTempPath(), "user-skill", "SKILL.md");
        var builtInPath = Path.Combine(Path.GetTempPath(), "built-in-skill", "SKILL.md");
        var report = CopilotAgentSkillDiagnostics.FormatReport(
            new CopilotAgentSkillUsageSnapshot(),
            metadataCharacterBudget: 1_024,
            overrides: null,
            availableSkills:
            [
                new CopilotAgentSkillCatalogItem("project-skill", "Project description") { SkillFilePath = projectPath },
                new CopilotAgentSkillCatalogItem("user-skill", "User description") { SourceKind = CopilotAgentSkillSourceKind.User, SkillFilePath = userPath },
                new CopilotAgentSkillCatalogItem("built-in-skill", "Built-in description") { SourceKind = CopilotAgentSkillSourceKind.BuiltIn, SkillFilePath = builtInPath },
            ],
            catalogReloaded: true,
            pathOverrides: new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                [userPath] = CopilotAgentSkillOverrideState.Off,
            });

        Assert.Contains("当前可调用：2/3 个 Skill 路径；已强制从磁盘重扫目录。", report, StringComparison.Ordinal);
        Assert.Contains("1. $built-in-skill [内置] — Built-in description [自动]", report, StringComparison.Ordinal);
        Assert.Contains("2. $project-skill [项目] — Project description [自动]", report, StringComparison.Ordinal);
        Assert.Contains("3. $user-skill [用户] — User description [关闭]", report, StringComparison.Ordinal);
        Assert.Contains("路径：" + userPath, report, StringComparison.Ordinal);
        Assert.Contains("本地使用证据", report, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoveryReadsOpenAiInterfaceDependenciesAndDefaultPrompt()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            var skillDirectory = Path.Combine(projectRoot, ".agents", "skills", "document-review");
            WriteSkill(skillDirectory, "document-review", "Long discovery description");
            var agentsDirectory = Path.Combine(skillDirectory, "agents");
            Directory.CreateDirectory(agentsDirectory);
            File.WriteAllText(Path.Combine(agentsDirectory, "openai.yaml"), """
                interface:
                  display_name: "Document Review"
                  short_description: "Review the current document"
                  default_prompt: "Use $document-review to review the selected document for correctness."
                policy:
                  allow_implicit_invocation: false # explicit only
                dependencies:
                  tools:
                    - type: "mcp"
                      value: "openaiDeveloperDocs"
                      description: "OpenAI Docs MCP server"
                      transport: "streamable_http"
                      url: "https://developers.openai.com/mcp"
                """);

            var skill = Assert.Single(CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory));

            Assert.Equal("Document Review", skill.DisplayName);
            Assert.Equal("Review the current document", skill.ShortDescription);
            Assert.Equal("Use $document-review to review the selected document for correctness.", skill.DefaultPrompt);
            Assert.False(CopilotAgentSkillMetadata.Read(skillDirectory).AllowImplicitInvocation);
            var dependency = Assert.Single(skill.Dependencies);
            Assert.Equal("mcp", dependency.Type);
            Assert.Equal("openaiDeveloperDocs", dependency.Value);
            Assert.Equal("OpenAI Docs MCP server", dependency.Description);
            Assert.Equal("streamable_http", dependency.Transport);
            Assert.Equal("", dependency.Command);
            Assert.Equal("https://developers.openai.com/mcp", dependency.Url);

            var suggestion = Assert.Single(CopilotLocalCommandCatalog.Suggest("$document", [skill]));
            Assert.Contains("Document Review · Review the current document · 依赖 1", suggestion.Description, StringComparison.Ordinal);
            Assert.Equal("Use $document-review to review the selected document for correctness.", suggestion.CompletionText);

            var report = CopilotAgentSkillDiagnostics.FormatReport(
                new CopilotAgentSkillUsageSnapshot(),
                metadataCharacterBudget: 1_024,
                availableSkills: [skill]);
            Assert.Contains("$document-review [项目] — Document Review · Review the current document", report, StringComparison.Ordinal);
            Assert.Contains(
                "依赖：mcp:openaiDeveloperDocs（OpenAI Docs MCP server） [streamable_http · 可安全配置]",
                report,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Fact]
    public void SkillMcpDependencyPolicyCreatesOnlySafeStreamableHttpConfig()
    {
        var dependency = new CopilotAgentSkillDependency(
            "mcp",
            "openaiDeveloperDocs",
            "OpenAI Docs MCP server",
            "streamable_http",
            "",
            "https://developers.openai.com/mcp");

        Assert.True(CopilotAgentSkillMcpDependencyPolicy.TryCreateServerConfig(
            dependency,
            out var server,
            out var error), error);
        Assert.Equal("openaiDeveloperDocs", server.Name);
        Assert.Equal("https://developers.openai.com/mcp", server.Endpoint);
        Assert.Equal(CopilotMcpClientAccessPolicy.RequireApproval, server.AccessPolicy);
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.Installable,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(dependency));
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.Installed,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(
                dependency,
                [new CopilotMcpClientServerConfig
                {
                    Name = "docs-alias",
                    Endpoint = "https://developers.openai.com/mcp",
                }]));
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.Installable,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(
                dependency,
                [new CopilotMcpClientServerConfig
                {
                    Name = "openaiDeveloperDocs",
                    Endpoint = "https://example.test/different-mcp",
                }]));
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.ConfiguredDisabled,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(
                dependency,
                [new CopilotMcpClientServerConfig
                {
                    Name = "disabled-docs",
                    Endpoint = "https://developers.openai.com/mcp",
                    Enabled = false,
                }]));

        var missingUrl = dependency with { Url = "" };
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.MissingInstallMetadata,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(missingUrl));
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.Installed,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(
                missingUrl,
                [new CopilotMcpClientServerConfig
                {
                    Name = "openaiDeveloperDocs",
                    Endpoint = "https://configured.test/mcp",
                    Enabled = true,
                }]));
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.ConfiguredDisabled,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(
                missingUrl,
                [new CopilotMcpClientServerConfig
                {
                    Name = "openaiDeveloperDocs",
                    Endpoint = "https://configured.test/mcp",
                    Enabled = false,
                }]));

        var unsupportedTransport = dependency with
        {
            Transport = "stdio",
            Command = "openai-docs-mcp",
            Url = "",
        };
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.UnsupportedTransport,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(unsupportedTransport));
        Assert.False(CopilotAgentSkillMcpDependencyPolicy.TryCreateServerConfig(
            unsupportedTransport,
            out _,
            out _));

        var insecureRemote = dependency with { Url = "http://developers.openai.com/mcp" };
        Assert.Equal(
            CopilotAgentSkillMcpDependencyStatus.InvalidConfiguration,
            CopilotAgentSkillMcpDependencyPolicy.Evaluate(insecureRemote));
        Assert.False(CopilotAgentSkillMcpDependencyPolicy.TryCreateServerConfig(
            insecureRemote,
            out _,
            out _));
    }

    [Fact]
    public void SkillMcpDependencyInstallerBuildsAndAppliesAnAtomicSafePlan()
    {
        var dependencies = new[]
        {
            new CopilotAgentSkillDependency(
                "mcp",
                "docs",
                "Docs",
                Url: "https://example.test/docs-mcp"),
            new CopilotAgentSkillDependency(
                "mcp",
                "unsafe",
                "Unsafe",
                Url: "http://example.test/unsafe-mcp"),
            new CopilotAgentSkillDependency(
                "mcp",
                "local",
                "Local",
                Url: "http://127.0.0.1:8765/mcp"),
        };

        var plan = CopilotAgentSkillMcpDependencyInstaller.CreatePlan(dependencies, []);

        Assert.Equal(2, plan.Servers.Count);
        Assert.Contains(plan.Servers, server => server.Name == "docs");
        Assert.Contains(plan.Servers, server => server.Name == "local");
        Assert.Single(plan.Issues);
        Assert.Contains("unsafe", plan.Issues[0], StringComparison.Ordinal);

        var configured = new List<CopilotMcpClientServerConfig>();
        Assert.True(CopilotAgentSkillMcpDependencyInstaller.TryInstall(
            plan,
            configured,
            out var added,
            out var error), error);
        Assert.Equal(2, added.Count);
        Assert.Equal(2, configured.Count);
        Assert.All(configured, server => Assert.Equal(
            CopilotMcpClientAccessPolicy.RequireApproval,
            server.AccessPolicy));

        Assert.False(CopilotAgentSkillMcpDependencyInstaller.TryInstall(
            plan,
            configured,
            out var repeatedAdditions,
            out _));
        Assert.Empty(repeatedAdditions);
        Assert.Equal(2, configured.Count);

        var maliciousPlan = new CopilotAgentSkillMcpDependencyInstallPlan(
            [new CopilotMcpClientServerConfig
            {
                Name = "unsafe-plan",
                Endpoint = "http://example.test/mcp",
                AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly,
            }],
            []);
        var untouched = new List<CopilotMcpClientServerConfig>();
        Assert.False(CopilotAgentSkillMcpDependencyInstaller.TryInstall(
            maliciousPlan,
            untouched,
            out var maliciousAdditions,
            out _));
        Assert.Empty(maliciousAdditions);
        Assert.Empty(untouched);

        var disabled = new[]
        {
            new CopilotMcpClientServerConfig
            {
                Name = "docs-disabled",
                Endpoint = "https://example.test/docs-mcp",
                Enabled = false,
            },
        };
        var disabledPlan = CopilotAgentSkillMcpDependencyInstaller.CreatePlan(
            [dependencies[0]],
            disabled);
        Assert.False(disabledPlan.HasServers);
        Assert.Contains("禁用", Assert.Single(disabledPlan.Issues), StringComparison.Ordinal);
    }

    [Fact]
    public void SkillMcpDependencyIssueOnlyPlansRequireOneAcknowledgementPerConversation()
    {
        var dependency = new CopilotAgentSkillDependency(
            "mcp",
            "docs",
            "Docs",
            Url: "https://example.test/docs-mcp");
        var disabledPlan = CopilotAgentSkillMcpDependencyInstaller.CreatePlan(
            [dependency],
            [new CopilotMcpClientServerConfig
            {
                Name = "docs-disabled",
                Endpoint = dependency.Url,
                Enabled = false,
            }]);
        var acknowledgedPromptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var issueOnlyPrompt = CopilotAgentSkillMcpDependencyInstaller.CreatePendingPromptPlan(
            disabledPlan,
            "conversation-1",
            acknowledgedPromptKeys);
        Assert.True(issueOnlyPrompt.RequiresPrompt);
        Assert.False(issueOnlyPrompt.HasServers);
        Assert.Single(issueOnlyPrompt.Issues);
        string prompt = CopilotChatViewModel.FormatUnresolvedSkillMcpDependencyPrompt(
            [new CopilotAgentSkillCatalogItem("docs-skill", "Docs skill")],
            issueOnlyPrompt.Issues);
        Assert.Contains("MCP 依赖无法自动配置", prompt, StringComparison.Ordinal);
        Assert.Contains("仍要在不配置这些依赖的情况下发送", prompt, StringComparison.Ordinal);
        Assert.Contains("保留草稿", prompt, StringComparison.Ordinal);

        CopilotAgentSkillMcpDependencyInstaller.AcknowledgePromptPlan(
            issueOnlyPrompt,
            "conversation-1",
            acknowledgedPromptKeys);
        Assert.False(CopilotAgentSkillMcpDependencyInstaller.CreatePendingPromptPlan(
            disabledPlan,
            "conversation-1",
            acknowledgedPromptKeys).RequiresPrompt);
        Assert.True(CopilotAgentSkillMcpDependencyInstaller.CreatePendingPromptPlan(
            disabledPlan,
            "conversation-2",
            acknowledgedPromptKeys).RequiresPrompt);
    }

    [Fact]
    public void SkillMcpDependencyInstallerResolvesPersistedRetryReferenceAndUniqueMentions()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-skill-mcp-resolve");
        var unique = new CopilotAgentSkillCatalogItem("unique-skill", "Unique")
        {
            SkillFilePath = Path.GetFullPath(Path.Combine(root, "unique", "SKILL.md")),
        };
        var duplicateOne = new CopilotAgentSkillCatalogItem("duplicate-skill", "First")
        {
            SkillFilePath = Path.GetFullPath(Path.Combine(root, "one", "SKILL.md")),
        };
        var duplicateTwo = new CopilotAgentSkillCatalogItem("duplicate-skill", "Second")
        {
            SkillFilePath = Path.GetFullPath(Path.Combine(root, "two", "SKILL.md")),
        };
        var prompt = "Use $unique-skill and $duplicate-skill.";
        var retryMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
        {
            AgentSkillReference = CopilotAgentSkillReference.FromCatalogItem(duplicateTwo),
        };
        var persistedRetryMessage = JsonConvert.DeserializeObject<CopilotChatMessage>(
            JsonConvert.SerializeObject(retryMessage))!;
        persistedRetryMessage.EnsureValid();

        var withoutExact = CopilotAgentSkillMcpDependencyInstaller.ResolveExplicitSkills(
            prompt,
            exactReference: null,
            [unique, duplicateOne, duplicateTwo]);
        var withExact = CopilotAgentSkillMcpDependencyInstaller.ResolveExplicitSkills(
            persistedRetryMessage.Content,
            persistedRetryMessage.AgentSkillReference,
            [unique, duplicateOne, duplicateTwo]);

        Assert.Equal([unique], withoutExact);
        Assert.Equal([duplicateTwo, unique], withExact);
    }

    [Fact]
    public void DiscoveryReadsSkillJsonMetadataAsFallback()
    {
        var projectRoot = CreateTemporaryDirectory();
        var applicationBaseDirectory = CreateTemporaryDirectory();
        var userProfileDirectory = CreateTemporaryDirectory();
        try
        {
            var skillDirectory = Path.Combine(projectRoot, ".agents", "skills", "json-skill");
            WriteSkill(skillDirectory, "json-skill", "Frontmatter description");
            File.WriteAllText(Path.Combine(skillDirectory, "SKILL.json"), """
                {
                  "interface": {
                    "displayName": "JSON Skill",
                    "shortDescription": "Metadata from SKILL.json",
                    "defaultPrompt": "Use the JSON metadata."
                  },
                  "dependencies": {
                    "tools": [
                      {
                        "type": "env_var",
                        "value": "SAMPLE_TOKEN",
                        "description": "Sample token",
                        "transport": "stdio",
                        "command": "sample-tool",
                        "url": "https://example.test/mcp"
                      }
                    ]
                  }
                }
                """);

            var skill = Assert.Single(CopilotAgentSkillCatalog.Discover(
                [projectRoot],
                overrides: null,
                applicationBaseDirectory,
                userProfileDirectory));

            Assert.Equal("JSON Skill", skill.DisplayName);
            Assert.Equal("Metadata from SKILL.json", skill.EffectiveDescription);
            Assert.Equal("Use the JSON metadata.", skill.DefaultPrompt);
            var dependency = Assert.Single(skill.Dependencies);
            Assert.Equal("env_var", dependency.Type);
            Assert.Equal("SAMPLE_TOKEN", dependency.Value);
            Assert.Equal("stdio", dependency.Transport);
            Assert.Equal("sample-tool", dependency.Command);
            Assert.Equal("https://example.test/mcp", dependency.Url);
        }
        finally
        {
            DeleteTemporaryDirectory(projectRoot);
            DeleteTemporaryDirectory(applicationBaseDirectory);
            DeleteTemporaryDirectory(userProfileDirectory);
        }
    }

    [Theory]
    [InlineData("", "Show")]
    [InlineData("reload", "Reload")]
    [InlineData("refresh", "Reload")]
    [InlineData("off 2", "Disable")]
    [InlineData("disable 2", "Disable")]
    [InlineData("enable 3", "Enable")]
    [InlineData("on 3", "Enable")]
    [InlineData("off 0", "Invalid")]
    [InlineData("unknown", "Invalid")]
    public void SkillsCommandResolvesCatalogActions(string arguments, string expected)
    {
        Assert.Equal(expected, CopilotAgentSkillCommand.Resolve(arguments).ToString());

        var command = CopilotLocalCommandCatalog.FindExact("/skills");
        Assert.NotNull(command);
        Assert.True(command.AcceptsArguments);
        Assert.Contains(command.Arguments!, argument => argument.Value == "reload");
        Assert.Contains(command.Arguments!, argument => argument.Value == "off");
        Assert.Contains(command.Arguments!, argument => argument.Value == "enable");
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

    [Fact]
    public async Task MonitorInvalidatesWhenSkillInterfaceMetadataChanges()
    {
        var parentDirectory = CreateTemporaryDirectory();
        try
        {
            var skillRoot = Path.Combine(parentDirectory, ".agents", "skills");
            var skillDirectory = Path.Combine(skillRoot, "watched-skill");
            WriteSkill(skillDirectory, "watched-skill", "Watched skill");
            using var changed = new SemaphoreSlim(0);
            using var monitor = new CopilotAgentSkillCatalogMonitor(
                () => changed.Release(),
                TimeSpan.FromMilliseconds(50));
            monitor.UpdateRoots([skillRoot]);

            File.WriteAllText(Path.Combine(skillDirectory, "SKILL.json"), """
                {
                  "interface": {
                    "displayName": "Watched Skill"
                  }
                }
                """);

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

    private sealed class TestAgentSkill(string name, string description) : AgentSkill
    {
        public override AgentSkillFrontmatter Frontmatter { get; } = new(name, description);

        public override ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(string.Empty);

        public override ValueTask<AgentSkillResource?> GetResourceAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AgentSkillResource?>(null);

        public override ValueTask<AgentSkillScript?> GetScriptAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AgentSkillScript?>(null);
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
