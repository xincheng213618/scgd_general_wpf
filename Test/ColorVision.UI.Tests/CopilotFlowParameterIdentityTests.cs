using ColorVision.UI;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowParameterIdentityTests
{
    [Fact]
    public void RealNodeSnapshotPreservesClrPropertyNamesAndValuesWhenDisplayNamesRepeat()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            var node = new ParameterIdentityNode();
            node.Create();
            editor.Nodes.Add(node);

            var snapshot = host.CaptureCopilotFlowSnapshot();

            var parameters = Assert.Single(snapshot.Nodes).Parameters;
            Assert.Equal(parameters.Select(property => property.PropertyName).Order(StringComparer.Ordinal), parameters.Select(property => property.PropertyName));
            var expected = parameters.Where(property => property.PropertyName is nameof(ParameterIdentityNode.AlphaValue)
                or nameof(ParameterIdentityNode.ApiKey) or nameof(ParameterIdentityNode.ZuluValue)).ToArray();
            Assert.Equal(new[] { "AlphaValue", "ApiKey", "ZuluValue" }, expected.Select(property => property.PropertyName));
            Assert.Equal(new[] { "Duplicate label", "Connection setting", "Duplicate label" }, expected.Select(property => property.Name));
            Assert.Equal(new[] { "alpha-value", "fixture credential without token formatting", "zulu-value" }, expected.Select(property => property.Value));
            Assert.Equal(2, expected.Count(property => property.Name == "Duplicate label"));
            Assert.Empty(new CopilotContextProperty { Name = "Legacy display", Value = "Legacy value" }.PropertyName);
            Assert.Equal(node.Authorization, Assert.Single(parameters, property => property.PropertyName == nameof(node.Authorization)).Value);
            Assert.Equal(node.RequestSetting, Assert.Single(parameters, property => property.PropertyName == nameof(node.RequestSetting)).Value);

            var context = CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot);
            Assert.DoesNotContain(node.ApiKey, context.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(node.Authorization, context.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(node.RequestSetting, context.Content, StringComparison.Ordinal);
            Assert.Contains("Connection setting=<redacted>", context.Content, StringComparison.Ordinal);
            Assert.Contains("Request header=<redacted>", context.Content, StringComparison.Ordinal);
            Assert.Contains("Bearer=<redacted>", context.Content, StringComparison.Ordinal);
            Assert.Contains("Duplicate label=alpha-value", context.Content, StringComparison.Ordinal);
            Assert.Contains("Duplicate label=zulu-value", context.Content, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SharedPropertyRenderingMasksClrAndDisplayKeysWithoutChangingExplicitlyUnmaskedMetadata()
    {
        var device = CopilotBusinessContextBuilder.BuildDeviceContextItem(new CopilotDeviceContextSnapshot
        {
            ServiceName = "Test service",
            ConfigProperties =
            [
                new() { PropertyName = "ApiKey", Name = "Connection setting", Value = "device fixture credential" },
                new() { PropertyName = "OrdinaryField", Name = "Password", Value = "display sensitive fixture" },
                new() { PropertyName = "Gain", Name = "Gain setting", Value = "ordinary-gain-value" },
                new() { PropertyName = "Authorization", Name = "Request header", Value = "plain-device-header-value" },
                new() { PropertyName = "OrdinaryRoute", Name = "Bearer", Value = "plain-device-request-value" },
                new() { PropertyName = "Mode", Name = "Mode", Value = "Production" },
            ],
        });
        Assert.DoesNotContain("device fixture credential", device.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("display sensitive fixture", device.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-device-header-value", device.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-device-request-value", device.Content, StringComparison.Ordinal);
        Assert.Contains("Connection setting: <redacted>", device.Content, StringComparison.Ordinal);
        Assert.Contains("Password: <redacted>", device.Content, StringComparison.Ordinal);
        Assert.Contains("Gain setting: ordinary-gain-value", device.Content, StringComparison.Ordinal);
        Assert.Contains("Request header: <redacted>", device.Content, StringComparison.Ordinal);
        Assert.Contains("Bearer: <redacted>", device.Content, StringComparison.Ordinal);
        Assert.Contains("Mode: Production", device.Content, StringComparison.Ordinal);

        var image = CopilotBusinessContextBuilder.BuildImageContextItem(new CopilotImageContextSnapshot
        {
            Metadata = [new() { PropertyName = "ApiKey", Name = "Explicitly unmasked metadata", Value = "public fixture metadata" }],
        });
        Assert.Contains("public fixture metadata", image.Content, StringComparison.Ordinal);
    }

    private sealed class ParameterIdentityNode : STNode
    {
        [STNodeProperty("Duplicate label", "Keep its distinct CLR identity even when another field has the same display label.")]
        public string ZuluValue { get; set; } = "zulu-value";

        [STNodeProperty("Duplicate label", "Declared later so reflection order cannot replace the CLR ordinal contract.")]
        public string AlphaValue { get; set; } = "alpha-value";

        [STNodeProperty("Connection setting", "The snapshot keeps the CLR sensitivity key; output consumers own redaction.")]
        public string ApiKey { get; set; } = "fixture credential without token formatting";

        [STNodeProperty("Request header", "The ordinary label preserves the Authorization CLR identity for output masking.")]
        public string Authorization { get; set; } = "plain-flow-header-value";

        [STNodeProperty("Bearer", "The shared display-name policy must not depend on token-looking value syntax.")]
        public string RequestSetting { get; set; } = "plain-flow-request-value";
    }
}
