using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class FlowMessageListProjectionTests
{
    [Fact]
    public void NodeFilterKeepsIdentifiersNamesAndUnknownMessagesSeparate()
    {
        FlowNodeMessage identified = new() { NodeId = "station", NodeName = "label" };
        FlowNodeMessage named = new() { NodeId = " ", NodeName = "station" };
        FlowNodeMessage otherCase = new() { NodeId = "STATION", NodeName = "station" };
        FlowNodeMessage unknown = new() { NodeId = " ", NodeName = "\u3000" };
        FlowNodeMessage namedUnknown = new() { NodeName = "unknown" };
        FlowNodeMessage[] messages = [identified, named, otherCase, unknown, namedUnknown];

        foreach (FlowNodeMessage message in messages)
        {
            var projection = FlowMessageListProjection.Build(messages,
                FlowMessageNodeKey.From(message), null, null, null);
            Assert.Same(message, Assert.Single(projection.DisplayedMessages));
            Assert.Same(message, projection.SelectedMessage);
            Assert.Equal(1, projection.MatchingCount);
        }
    }

    [Fact]
    public void RequestedMessageWinsBeyondLimitAndUsesTheFirstMatchingId()
    {
        const int limit = FlowMessageListProjection.MaximumDisplayedMessages;
        FlowNodeMessage[] matches = Enumerable.Range(1, limit + 2)
            .Select(id => new FlowNodeMessage { Id = id, NodeId = "station", State = FlowMessageState.Success })
            .ToArray();
        FlowNodeMessage requested = matches[^2];
        matches[^1].Id = requested.Id;
        FlowNodeMessage[] messages =
        [
            new() { Id = requested.Id, NodeId = "station", State = FlowMessageState.Timeout },
            .. matches,
            new() { NodeId = "another station", State = FlowMessageState.Fail },
        ];

        var projection = FlowMessageListProjection.Build(messages, FlowMessageNodeKey.From(requested),
            FlowMessageState.Success, matches[0], requested.Id);

        Assert.Equal(matches.Length, projection.MatchingCount);
        Assert.Equal(2, projection.IssueCount);
        Assert.Same(requested, projection.SelectedMessage);
        Assert.Equal(matches.Take(limit - 1).Append(requested), projection.DisplayedMessages);
    }

    [Fact]
    public void UnfilteredListKeepsPriorSelectionBeyondLimitWhenRequestedIdIsMissing()
    {
        const int limit = FlowMessageListProjection.MaximumDisplayedMessages;
        FlowNodeMessage[] messages = Enumerable.Range(1, limit + 3)
            .Select(id => new FlowNodeMessage { Id = id }).ToArray();
        FlowNodeMessage selected = messages[^1];

        var projection = FlowMessageListProjection.Build(messages, null, null, selected, -1);

        Assert.Equal(messages.Length, projection.MatchingCount);
        Assert.Same(selected, projection.SelectedMessage);
        Assert.Equal(messages.Take(limit - 1).Append(selected), projection.DisplayedMessages);
    }
}
