using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowPatchParameterPrivacyTests
{
    [Fact]
    public async Task TemplateCandidatesExcludeSensitiveClrOrDisplayNamesAndKeepNormalDuplicateLabels()
    {
        const string clrSecret = "ordinary-clr-value-7319";
        const string displaySecret = "ordinary-display-value-4821";
        const string legacySecret = "ordinary-legacy-value-6173";
        const string licenseSecret = "ordinary-license-value-3286";
        const string localizedSecret = "ordinary-localized-value-5924";
        var node = new CopilotFlowNodeContextSnapshot
        {
            NodeId = "node-privacy", Title = "Parameter test", IsSelected = true, Parameters =
            [
                new() { PropertyName = "ApiKey", Name = "Hidden candidate", Value = clrSecret },
                new() { PropertyName = "Endpoint", Name = "AccessKey", Value = displaySecret },
                new() { Name = "Password", Value = legacySecret },
                new() { PropertyName = "License", Name = "许可候选", Value = licenseSecret },
                new() { PropertyName = "EndpointText", Name = "密码", Value = localizedSecret },
                new() { PropertyName = "ModeA", Name = "Setting", Value = "Production" },
                new() { PropertyName = "ModeB", Name = "Setting", Value = "Reference" },
                new() { Name = "LegacyGain", Value = "5" },
            ],
        };
        var savedTemplate = new CopilotLiveContext
        {
            SourceId = "template-json-editor:parameter-privacy", SnapshotItems =
            [
                new() { Content = "```json\n{\"Gain\":7}\n```" },
            ],
        };
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowSnapshotProvider = _ => Task.FromResult<CopilotFlowContextSnapshot?>(new() { Nodes = [node] }),
            LiveContextProvider = () => savedTemplate,
        });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"template_identifier\":\"retained-template\",\"node_id\":\"node-privacy\",\"intent\":\"review Gain\"}")!;

        var result = await dispatcher.CallAsync("suggest_template_patch", arguments, CancellationToken.None);

        Assert.True(result.Success, result.Text);
        Assert.Contains("## Candidate Fields", result.Text, StringComparison.Ordinal);
        foreach (var secret in new[] { clrSecret, displaySecret, legacySecret, licenseSecret, localizedSecret })
            Assert.DoesNotContain(secret, result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden candidate (node parameter", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessKey (node parameter", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Password (node parameter", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("许可候选 (node parameter", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("密码 (node parameter", result.Text, StringComparison.Ordinal);
        Assert.Contains("Setting (node parameter, value=Production)", result.Text, StringComparison.Ordinal);
        Assert.Contains("Setting (node parameter, value=Reference)", result.Text, StringComparison.Ordinal);
        Assert.Contains("LegacyGain (node parameter, value=5)", result.Text, StringComparison.Ordinal);
        Assert.Equal(clrSecret, node.Parameters[0].Value);
        Assert.Equal("```json\n{\"Gain\":7}\n```", savedTemplate.SnapshotItems[0].Content);
    }

    [Fact]
    public void AddNodePreviewPreservesItsShapeAndCameraNumbersWithoutPublishingPrivateParameterValues()
    {
        const string clrSecret = "ordinary-clr-preview-7319";
        const string displaySecret = "ordinary-display-preview-4821";
        const string legacySecret = "ordinary-legacy-preview-6173";
        const string markSecret = "ordinary-mark-preview-9021";
        var node = new CopilotFlowNodeContextSnapshot
        {
            InstanceId = "12345678-1234-1234-1234-123456789abc", NodeId = "recorded-node", TypeKey = "Test|Camera",
            RuntimeType = "Test.CameraNode", CategoryPath = "Camera", Title = "Camera preview", NodeName = "Camera 1", NodeType = "Camera",
            DeviceCode = "camera-test", Left = 31, Top = 47, Width = 120, Height = 90, Position = "31,47",
            Mark = "password=" + markSecret, IsActive = true, IsSelected = true,
            Inputs = ["IN"], Outputs = ["OUT"],
            InputPorts = [new() { PortId = "in:0", Name = "IN", DataType = "System.Object" }],
            OutputPorts = [new() { PortId = "out:0", Name = "OUT", DataType = "System.Object" }],
            Parameters =
            [
                new() { PropertyName = "ApiKey", Name = "Connection setting", Value = clrSecret },
                new() { PropertyName = "Endpoint", Name = "AccessKey", Value = displaySecret },
                new() { Name = "Password", Value = legacySecret },
                new() { PropertyName = "ModeA", Name = "Setting", Value = "Production" },
                new() { PropertyName = "ModeB", Name = "Setting", Value = "Reference" },
                new() { Name = "LegacyGain", Value = "7" },
            ],
            CameraDefaults = new("camera-test", [100f, 200f], 7f, 3, "XY", "calibration-test"),
            CameraDefaultsFingerprint = new string('a', 64),
        };

        var text = CopilotFlowPatchPreviewFormatter.Serialize(new string('b', 64), new { operation = "add_node", node });

        foreach (var secret in new[] { clrSecret, displaySecret, legacySecret, markSecret })
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.Equal("colorvision.flow-patch-preview.v1", root.GetProperty("format").GetString());
        Assert.Equal(new string('b', 64), root.GetProperty("expectedRevision").GetString());
        Assert.Equal("add_node", root.GetProperty("change").GetProperty("operation").GetString());
        var output = root.GetProperty("change").GetProperty("node");
        Assert.Equal(node.InstanceId, output.GetProperty("instanceId").GetString());
        Assert.Equal(node.TypeKey, output.GetProperty("typeKey").GetString());
        Assert.Equal(31, output.GetProperty("left").GetInt32());
        Assert.Equal(47, output.GetProperty("top").GetInt32());
        Assert.Equal("in:0", Assert.Single(output.GetProperty("inputPorts").EnumerateArray()).GetProperty("portId").GetString());
        var parameters = output.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(6, parameters.Length);
        Assert.All(parameters.Take(3), parameter => Assert.Equal("<redacted>", parameter.GetProperty("value").GetString()));
        Assert.Equal(new[] { "Production", "Reference" }, parameters.Where(parameter => parameter.GetProperty("name").GetString() == "Setting").Select(parameter => parameter.GetProperty("value").GetString()));
        Assert.Equal("7", parameters[5].GetProperty("value").GetString());
        Assert.Equal("ApiKey", parameters[0].GetProperty("propertyName").GetString());
        Assert.Equal(new[] { 100f, 200f }, output.GetProperty("cameraDefaults").GetProperty("exposureMilliseconds").EnumerateArray().Select(value => value.GetSingle()));
        Assert.Equal(7f, output.GetProperty("cameraDefaults").GetProperty("gain").GetSingle());
        Assert.Equal(3, output.GetProperty("cameraDefaults").GetProperty("averageCount").GetInt32());
        Assert.Equal(new string('a', 64), output.GetProperty("cameraDefaultsFingerprint").GetString());
        Assert.Equal(clrSecret, node.Parameters[0].Value);
        Assert.Equal("password=" + markSecret, node.Mark);
    }

    [Fact]
    public void SetPropertyPreviewRedactsInlineAssignmentsInBothOldAndNewValues()
    {
        const string oldSecret = "ordinary-old-preview-7319";
        const string newSecret = "ordinary-new-preview-4821";
        var change = new { operation = "set_property", propertyName = "Description", oldValue = "password=" + oldSecret, newValue = "api_key=" + newSecret };

        var text = CopilotFlowPatchPreviewFormatter.Serialize(new string('a', 64), change);

        Assert.DoesNotContain(oldSecret, text, StringComparison.Ordinal);
        Assert.DoesNotContain(newSecret, text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text);
        var output = json.RootElement.GetProperty("change");
        Assert.Equal("Description", output.GetProperty("propertyName").GetString());
        Assert.Contains("<redacted>", output.GetProperty("oldValue").GetString(), StringComparison.Ordinal);
        Assert.Contains("<redacted>", output.GetProperty("newValue").GetString(), StringComparison.Ordinal);
        Assert.Equal("password=" + oldSecret, change.oldValue);
    }
}
