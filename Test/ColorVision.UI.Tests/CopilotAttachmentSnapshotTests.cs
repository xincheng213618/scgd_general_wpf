using ColorVision.Copilot;

namespace ColorVision.UI.Tests
{
    public sealed class CopilotAttachmentSnapshotTests
    {
        [Fact]
        public void HostedTurnRefreshesAttachedLiveContextWhenTheTurnIsCaptured()
        {
            const string sourceId = "flow-engine-manager";
            var attachment = CopilotAttachmentItem.CreateContext(
                "stale flow snapshot",
                "Old flow context",
                sourceId);
            var liveContext = new CopilotLiveContext
            {
                SourceId = sourceId,
                AttachmentTitle = "Current flow context",
                SnapshotItems =
                [
                    new CopilotContextItem
                    {
                        Id = "flow-context",
                        Title = "Flow context",
                        Content = "latest flow snapshot",
                    },
                ],
            };

            var snapshot = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: [attachment],
                liveContext);

            var refreshedAttachment = Assert.Single(snapshot.Attachments);
            Assert.Equal("Current flow context", refreshedAttachment.Title);
            Assert.Contains("latest flow snapshot", refreshedAttachment.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("stale flow snapshot", refreshedAttachment.Value, StringComparison.Ordinal);
            Assert.Equal(attachment.Id, refreshedAttachment.Id);
        }
    }
}
