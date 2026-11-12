using ColorVision.UI;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowLogProjectionPrivacyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceCaptureRedactsCompleteRunMessagesBeforeFailureExtractionOrTailCropping(bool credentialCrossesRawTailBoundary)
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            const string visibleFailure = "ERROR visible-sensor-failure-7319";
            string source = string.Join(Environment.NewLine, new[]
            {
                "password=\"fixture-first-secret",
                credentialCrossesRawTailBoundary ? new string('x', 7000) : "fixture-middle-secret",
                "ERROR plain-tail-value-7319",
                "fixture-final-secret\"; Mode=Production",
                visibleFailure,
            });
            var node = new LogProjectionNode();
            node.Create();
            editor.Nodes.Add(node);
            var view = host.Manager.View;
            view.ShowExecutionSummary(source);

            var snapshot = host.CaptureCopilotFlowSnapshot();
            var context = Assert.Single(host.CaptureCopilotContext().Items);

            Assert.Equal(new[] { visibleFailure }, snapshot.FailureEvidence);
            Assert.Equal(visibleFailure, snapshot.RecentFailureSummary);
            foreach (string projection in new[] { snapshot.RecentRunMessage, snapshot.RecentFailureSummary,
                string.Join(Environment.NewLine, snapshot.FailureEvidence), context.Content })
            {
                foreach (string secret in new[] { "fixture-first-secret", "fixture-middle-secret", "plain-tail-value-7319", "fixture-final-secret" })
                    Assert.DoesNotContain(secret, projection, StringComparison.Ordinal);
                Assert.Contains(visibleFailure, projection, StringComparison.Ordinal);
            }
            Assert.Contains("password=<redacted>", snapshot.RecentRunMessage, StringComparison.Ordinal);
            Assert.Contains("Mode=Production", snapshot.RecentRunMessage, StringComparison.Ordinal);
            Assert.Contains("Mode=Production", context.Content, StringComparison.Ordinal);
            Assert.Equal(source, view.logTextBox.Text);
            var parameter = Assert.Single(Assert.Single(snapshot.Nodes).Parameters,
                property => property.PropertyName == nameof(LogProjectionNode.ApiKey));
            Assert.Equal("original-node-parameter-7319", parameter.Value);
            Assert.Equal(parameter.Value, node.ApiKey);
        });
    }

    private sealed class LogProjectionNode : STNode
    {
        [STNodeProperty("Connection configuration", "Log projection must not rewrite captured node parameter values.")]
        public string ApiKey { get; set; } = "original-node-parameter-7319";
    }
}
