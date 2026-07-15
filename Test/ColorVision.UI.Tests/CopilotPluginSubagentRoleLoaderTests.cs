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
    public void SettingsDiagnostics_ListsSubagentSourceVersionDomainAndFingerprint()
    {
        var viewModel = new CopilotSettingsViewModel();

        Assert.Contains("built-in", viewModel.SubagentRolesSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source=ColorVision [builtin] version=1", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("domain=WorkspaceReadOnly", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("fingerprint=", viewModel.SubagentRolesDiagnosticsText, StringComparison.Ordinal);
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
}
