using ColorVision.Copilot.Mcp;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Integration;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowPreviewPrivacyTests
{
    [Fact]
    public void DefaultAddNodePreviewRedactsParametersWithoutAddingNodesOrChangingTheirConstructorValues()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            editor.LoadAssembly(typeof(PreviewPrivacyNode).Assembly);
            var type = Assert.Single(host.CaptureCopilotFlowNodeCatalog(nameof(PreviewPrivacyNode), 100).NodeTypes,
                item => item.RuntimeType == typeof(PreviewPrivacyNode).FullName);
            var before = host.CaptureCopilotFlowSnapshot();
            var canvas = editor.GetCanvasData();
            var source = host.PreviewCopilotFlowNodeAddition(type.TypeKey, 31, 47, before.Revision);
            Assert.Equal(PreviewPrivacyNode.Credential, Assert.Single(source.Parameters, item => item.PropertyName == nameof(PreviewPrivacyNode.ApiKey)).Value);

            WithCurrentHost(host, () =>
            {
                var result = new CopilotMcpToolEnvironment().PreviewFlowPatchHandler(new CopilotFlowPatchRequest
                {
                    Operation = "add_node", ExpectedRevision = before.Revision, TypeKey = type.TypeKey, Left = 31, Top = 47,
                }, CancellationToken.None).GetAwaiter().GetResult();

                Assert.True(result.Success, result.Text);
                foreach (var secret in new[] { PreviewPrivacyNode.Credential, PreviewPrivacyNode.LocalCredential, PreviewPrivacyNode.MarkCredential })
                    Assert.DoesNotContain(secret, result.Text, StringComparison.Ordinal);
                using var json = JsonDocument.Parse(result.Text);
                Assert.Equal("colorvision.flow-patch-preview.v1", json.RootElement.GetProperty("format").GetString());
                var node = json.RootElement.GetProperty("change").GetProperty("node");
                Assert.Equal(type.TypeKey, node.GetProperty("typeKey").GetString());
                Assert.Equal(31, node.GetProperty("left").GetInt32());
                Assert.Equal(47, node.GetProperty("top").GetInt32());
                var parameters = node.GetProperty("parameters").EnumerateArray().ToArray();
                Assert.Equal("<redacted>", Assert.Single(parameters, item => item.GetProperty("propertyName").GetString() == nameof(PreviewPrivacyNode.ApiKey)).GetProperty("value").GetString());
                Assert.Equal("<redacted>", Assert.Single(parameters, item => item.GetProperty("propertyName").GetString() == nameof(PreviewPrivacyNode.Configuration)).GetProperty("value").GetString());
                Assert.Equal("17", Assert.Single(parameters, item => item.GetProperty("propertyName").GetString() == nameof(PreviewPrivacyNode.Threshold)).GetProperty("value").GetString());
                Assert.Equal(PreviewPrivacyNode.Credential, Assert.Single(source.Parameters, item => item.PropertyName == nameof(PreviewPrivacyNode.ApiKey)).Value);
                Assert.Equal(canvas, editor.GetCanvasData());
                Assert.Equal(before.Revision, host.CaptureCopilotFlowSnapshot().Revision);
                Assert.Empty(editor.Nodes.Cast<STNode>());
            });
        });
    }

    [Fact]
    public void DefaultSensitivePropertyPreviewAndApplyRejectBeforeCapturingAnyNodeValues()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) => WithCurrentHost(host, () =>
        {
            var node = new PreviewPrivacyNode();
            node.Create();
            editor.Nodes.Add(node);
            var revision = host.CaptureCopilotFlowSnapshot().Revision;
            node.ResetReads();
            var request = new CopilotFlowPatchRequest
            {
                Operation = "set_property", ExpectedRevision = revision, NodeId = node.Guid.ToString(),
                PropertyName = nameof(PreviewPrivacyNode.Configuration), Value = "not-applied",
            };
            var environment = new CopilotMcpToolEnvironment();

            var preview = environment.PreviewFlowPatchHandler(request, CancellationToken.None).GetAwaiter().GetResult();
            var apply = environment.ApplyFlowPatchHandler(request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.False(preview.Success);
            Assert.False(apply.Success);
            Assert.Contains("sensitive", preview.Text, StringComparison.Ordinal);
            Assert.Contains("sensitive", apply.Text, StringComparison.Ordinal);
            Assert.Equal(0, node.GetterCalls);
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));
        }));
    }

    private static void WithCurrentHost(FlowCopilotService host, Action action)
    {
        var instance = typeof(FlowEngineManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = instance.GetValue(null);
        instance.SetValue(null, typeof(FlowCopilotService).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host));
        try { action(); }
        finally { instance.SetValue(null, previous); }
    }

    [STNode("Tests/Preview privacy", "Offline preview serialization fixture")]
    public sealed class PreviewPrivacyNode : STNode
    {
        public const string Credential = "plain-preview-fixture-credential";
        public const string LocalCredential = "plain-preview-localized-fixture";
        public const string MarkCredential = "plain-preview-mark-fixture";
        private string _configuration = LocalCredential;
        public int GetterCalls { get; private set; }
        public void ResetReads() => GetterCalls = 0;

        [STNodeProperty("Connection setting", "The CLR name identifies sensitivity.")]
        public string ApiKey { get; set; } = Credential;

        [STNodeProperty("密码", "The display label also identifies sensitivity.")]
        public string Configuration { get { GetterCalls++; return _configuration; } set => _configuration = value; }

        [STNodeProperty("Threshold", "An ordinary parameter remains visible.")]
        public int Threshold { get; set; } = 17;

        protected override void OnCreate()
        {
            base.OnCreate();
            Title = "Preview privacy fixture";
            Mark = "password=" + MarkCredential;
        }
    }
}
