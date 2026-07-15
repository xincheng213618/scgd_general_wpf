#pragma warning disable CA1707
using ColorVision.Copilot;
using ColorVision.UI.Plugins;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotPluginSubagentRoleLoaderTests
{
    [Fact]
    public void PluginManifest_DeserializesDeclarativeCopilotAgentRole()
    {
        const string json = """
            {
              "id": "sample.plugin",
              "name": "Sample Plugin",
              "version": "2.4.1",
              "copilot_agents": [
                {
                  "id": "plugin-reviewer",
                  "name": "Plugin Reviewer",
                  "scope": "WorkspaceReadOnly",
                  "capabilities": ["GrepText", "ReadLocalFile"],
                  "parent_modes": ["Code", "Diagnose"]
                }
              ]
            }
            """;

        var manifest = Assert.IsType<PluginManifest>(JsonConvert.DeserializeObject<PluginManifest>(json));
        var role = Assert.Single(manifest.CopilotAgents);

        Assert.Equal("plugin-reviewer", role.Id);
        Assert.Equal("Plugin Reviewer", role.Name);
        Assert.Equal("WorkspaceReadOnly", role.Scope);
        Assert.Equal(["GrepText", "ReadLocalFile"], role.Capabilities);
        Assert.Equal(["Code", "Diagnose"], role.ParentModes);
    }

    [Fact]
    public void Synchronize_RegistersStableRoleAndUnregistersWhenPluginIsDisabled()
    {
        var capabilityCatalog = new CopilotCapabilityCatalog();
        var roleRegistry = new CopilotSubagentRoleRegistry(capabilityCatalog);
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        var initialRevision = roleRegistry.GetSnapshot().Revision;

        var first = loader.Synchronize([plugin]);
        var firstCatalog = roleRegistry.GetSnapshot();
        var role = firstCatalog.GetRequired("plugin-reviewer");

        Assert.Equal(1, first.LoadedRoleCount);
        Assert.Empty(first.Issues);
        Assert.Equal(initialRevision + 1, firstCatalog.Revision);
        Assert.Equal("sample.plugin", role.SourceId);
        Assert.Equal("2.4.1", role.SourceVersion);
        Assert.Equal("DelegatePluginReviewer", role.ToolName);
        Assert.Equal(CopilotSubagentContextScope.WorkspaceReadOnly, role.ContextScope);
        Assert.Equal(CopilotSubagentReadCapabilities.GrepText | CopilotSubagentReadCapabilities.ReadLocalFile, role.ReadCapabilities);
        Assert.Contains(capabilityCatalog.GetSnapshot().Capabilities, entry => entry.Id == "subagent:sample.plugin:plugin-reviewer");

        loader.Synchronize([plugin]);
        Assert.Equal(firstCatalog.Revision, roleRegistry.GetSnapshot().Revision);

        plugin.Enabled = false;
        var disabled = loader.GetSnapshot();
        Assert.Equal(0, disabled.LoadedRoleCount);
        Assert.Empty(disabled.Issues);
        Assert.DoesNotContain(roleRegistry.GetSnapshot().Roles, candidate => candidate.Id == "plugin-reviewer");
        Assert.DoesNotContain(capabilityCatalog.GetSnapshot().Capabilities, entry => entry.Id == "subagent:sample.plugin:plugin-reviewer");
    }

    [Fact]
    public void Synchronize_PluginVersionChangeReplacesCapabilityFingerprint()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        loader.Synchronize([plugin]);
        var first = roleRegistry.GetSnapshot().GetRequired("plugin-reviewer");
        var firstRevision = roleRegistry.GetSnapshot().Revision;

        plugin.Manifest.Version = "2.5.0";
        var update = loader.Synchronize([plugin]);
        var secondCatalog = roleRegistry.GetSnapshot();
        var second = secondCatalog.GetRequired("plugin-reviewer");

        Assert.Empty(update.Issues);
        Assert.Equal("2.5.0", second.SourceVersion);
        Assert.NotEqual(first.CapabilityFingerprint, second.CapabilityFingerprint);
        Assert.Equal(firstRevision + 2, secondCatalog.Revision);
    }

    [Fact]
    public void Synchronize_DisabledRoleIsNotRegisteredAndCanBeReenabled()
    {
        var capabilityCatalog = new CopilotCapabilityCatalog();
        var roleRegistry = new CopilotSubagentRoleRegistry(capabilityCatalog);
        var toolRegistry = CopilotToolRegistry.CreateDefault(roleRegistry);
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();

        var disabled = loader.Synchronize([plugin], ["sample.plugin/plugin-reviewer"]);
        var declared = Assert.Single(disabled.DeclaredRoles);

        Assert.Equal(0, disabled.LoadedRoleCount);
        Assert.False(declared.IsEnabled);
        Assert.Equal("sample.plugin/plugin-reviewer", declared.Key);
        Assert.Equal(5, declared.MaximumToolCalls);
        Assert.True(declared.AdvertisedCharacters > 0);
        Assert.DoesNotContain(roleRegistry.GetSnapshot().Roles, role => role.Id == "plugin-reviewer");
        Assert.DoesNotContain(capabilityCatalog.GetSnapshot().Capabilities, entry => entry.Id == "subagent:sample.plugin:plugin-reviewer");
        Assert.DoesNotContain(toolRegistry.Tools, tool => tool.Name == "DelegatePluginReviewer");

        plugin.Enabled = false;
        plugin.Enabled = true;
        Assert.False(Assert.Single(loader.GetSnapshot().DeclaredRoles).IsEnabled);
        Assert.DoesNotContain(roleRegistry.GetSnapshot().Roles, role => role.Id == "plugin-reviewer");

        var enabled = loader.SetDisabledRoleKeys([]);

        Assert.Equal(1, enabled.LoadedRoleCount);
        Assert.True(Assert.Single(enabled.DeclaredRoles).IsEnabled);
        Assert.Contains(roleRegistry.GetSnapshot().Roles, role => role.Id == "plugin-reviewer");
        Assert.Contains(capabilityCatalog.GetSnapshot().Capabilities, entry => entry.Id == "subagent:sample.plugin:plugin-reviewer");
        Assert.Contains(toolRegistry.Tools, tool => tool.Name == "DelegatePluginReviewer");
    }

    [Fact]
    public void Synchronize_RejectsMixedTrustDomainAndReportsManifestIssue()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        plugin.Manifest.CopilotAgents[0].Capabilities.Add("WebSearch");

        var snapshot = loader.Synchronize([plugin]);
        var issue = Assert.Single(snapshot.Issues);

        Assert.Equal(0, snapshot.LoadedRoleCount);
        Assert.Equal("sample.plugin", issue.SourceId);
        Assert.Equal("plugin-reviewer", issue.RoleId);
        Assert.Contains("cannot mix", issue.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(roleRegistry.GetSnapshot().Roles, candidate => candidate.Id == "plugin-reviewer");
    }

    [Fact]
    public void Synchronize_DoesNotPublishRolesForPluginWhoseAssemblyFailedToLoad()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        plugin.Assembly = null!;

        var snapshot = loader.Synchronize([plugin]);
        var issue = Assert.Single(snapshot.Issues);

        Assert.Equal(0, snapshot.LoadedRoleCount);
        Assert.Contains("assembly did not load", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Synchronize_RejectsPluginWhoseRoleCountExceedsRuntimeLimit()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        plugin.Manifest.CopilotAgents = Enumerable.Range(0, 17)
            .Select(index => CreateRole(index, "Delegate a bounded read-only review."))
            .ToList();

        var snapshot = loader.Synchronize([plugin]);

        Assert.Equal(0, snapshot.LoadedRoleCount);
        Assert.Contains(snapshot.Issues, issue => issue.Message.Contains("at most 16", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Synchronize_RejectsPluginWhoseAdvertisedRoleMetadataExceedsRuntimeLimit()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var loader = new CopilotPluginSubagentRoleLoader(roleRegistry);
        var plugin = CreatePlugin();
        plugin.Manifest.CopilotAgents = Enumerable.Range(0, 7)
            .Select(index => CreateRole(index, new string('a', 1_180)))
            .ToList();

        var snapshot = loader.Synchronize([plugin]);

        Assert.Equal(0, snapshot.LoadedRoleCount);
        Assert.Contains(snapshot.Issues, issue => issue.Message.Contains("8,000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SettingsDiagnostics_ListsSubagentSourceVersionDomainAndFingerprint()
    {
        var viewModel = new CopilotSettingsViewModel();

        Assert.Contains("built-in", viewModel.SubagentRolesSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source=ColorVision [builtin] version=1", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("domain=WorkspaceReadOnly", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("fingerprint=", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsSave_PersistsVisibleAndTemporarilyUnavailableDisabledRoles()
    {
        var config = CopilotConfig.Instance;
        var originalDisabledRoles = config.DisabledPluginSubagentRoles.ToArray();

        try
        {
            config.DisabledPluginSubagentRoles.Clear();
            config.DisabledPluginSubagentRoles.Add("absent.plugin/old-role");
            var viewModel = new CopilotSettingsViewModel();
            viewModel.PluginSubagentRoles.Clear();
            viewModel.PluginSubagentRoles.Add(new CopilotPluginSubagentRoleSetting(
                new CopilotPluginSubagentRoleInfo
                {
                    Key = "sample.plugin/plugin-reviewer",
                    SourceName = "Sample Plugin",
                    DisplayName = "Plugin Reviewer",
                    ToolName = "DelegatePluginReviewer",
                },
                isEnabled: false,
                permissionSummary: "Read access: WorkspaceReadOnly",
                budgetSummary: "Limit: bounded",
                changed: () => { }));

            Assert.True(viewModel.Save());
            Assert.Contains("absent.plugin/old-role", config.DisabledPluginSubagentRoles);
            Assert.Contains("sample.plugin/plugin-reviewer", config.DisabledPluginSubagentRoles);
        }
        finally
        {
            config.DisabledPluginSubagentRoles.Clear();
            foreach (var roleKey in originalDisabledRoles)
                config.DisabledPluginSubagentRoles.Add(roleKey);
            ColorVision.UI.ConfigHandler.GetInstance().Save<CopilotConfig>();
            CopilotPluginSubagentRoleLoader.Shared.SetDisabledRoleKeys(config.DisabledPluginSubagentRoles);
        }
    }

    private static PluginInfo CreatePlugin()
    {
        return new PluginInfo
        {
            Enabled = true,
            Assembly = typeof(CopilotPluginSubagentRoleLoaderTests).Assembly,
            AssemblyVersion = new Version(2, 4, 1),
            Name = "Sample Plugin",
            Manifest = new PluginManifest
            {
                Id = "sample.plugin",
                Name = "Sample Plugin",
                Version = "2.4.1",
                CopilotAgents =
                [
                    new CopilotSubagentRoleManifest
                    {
                        Id = "plugin-reviewer",
                        Name = "Plugin Reviewer",
                        Description = "Delegate a bounded plugin code review to a read-only child Agent.",
                        Instructions = "Review only the requested plugin source evidence and return exact file paths.",
                        Scope = "WorkspaceReadOnly",
                        Capabilities = ["GrepText", "ReadLocalFile"],
                        ChildMode = "Code",
                        ParentModes = ["Code", "Diagnose"],
                        MaximumToolCalls = 5,
                        MaximumAgentPasses = 2,
                        MaximumDurationSeconds = 60,
                        MaximumAnswerCharacters = 8_000,
                    },
                ],
            },
        };
    }

    private static CopilotSubagentRoleManifest CreateRole(int index, string description)
    {
        return new CopilotSubagentRoleManifest
        {
            Id = $"reviewer-{index:00}",
            Name = $"Reviewer {index:00}",
            Description = description,
            Instructions = "Review only the supplied workspace evidence and return a concise result.",
            Scope = "WorkspaceReadOnly",
            Capabilities = ["GrepText", "ReadLocalFile"],
            ChildMode = "Code",
            ParentModes = ["Code", "Diagnose"],
            MaximumToolCalls = 5,
            MaximumAgentPasses = 2,
            MaximumDurationSeconds = 60,
            MaximumAnswerCharacters = 8_000,
        };
    }
}
