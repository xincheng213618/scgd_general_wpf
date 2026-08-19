using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests
{
    public sealed class CopilotAttachmentSnapshotTests
    {
        [Fact]
        public void AgentRequestDetachesAttachmentAuthorizationViews()
        {
            var source = CopilotAttachmentItem.CreateFile(@"C:\evidence\source.txt");
            var request = new CopilotAgentRequest { Attachments = [source] };

            source.Value = @"C:\evidence\mutated-source.txt";
            var firstView = Assert.Single(request.Attachments);
            firstView.Value = @"C:\evidence\mutated-view.txt";

            var stableView = Assert.Single(request.Attachments);
            Assert.Equal(@"C:\evidence\source.txt", stableView.Value);
            Assert.NotSame(source, stableView);
            Assert.NotSame(firstView, stableView);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CopilotAttachmentItem>)request.Attachments).Clear());
        }

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
