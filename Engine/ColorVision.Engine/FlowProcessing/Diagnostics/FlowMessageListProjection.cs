using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal readonly record struct FlowMessageNodeKey
{
    private readonly bool _isIdentifier;
    private readonly string? _value;

    private FlowMessageNodeKey(bool isIdentifier, string value) => (_isIdentifier, _value) = (isIdentifier, value);

    internal static FlowMessageNodeKey From(FlowNodeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.NodeId))
            return new(true, message.NodeId);
        return string.IsNullOrWhiteSpace(message.NodeName) ? default : new(false, message.NodeName);
    }

    internal bool Matches(FlowNodeMessage message)
    {
        if (_isIdentifier)
            return string.Equals(_value, message.NodeId, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(message.NodeId) && (_value == null
            ? string.IsNullOrWhiteSpace(message.NodeName)
            : string.Equals(_value, message.NodeName, StringComparison.Ordinal));
    }
}

internal readonly record struct FlowMessageListProjection(
    IReadOnlyList<FlowNodeMessage> DisplayedMessages, FlowNodeMessage? SelectedMessage, int MatchingCount, int IssueCount)
{
    internal const int MaximumDisplayedMessages = 2000;

    internal static FlowMessageListProjection Build(IReadOnlyList<FlowNodeMessage> messages,
        FlowMessageNodeKey? nodeKey, FlowMessageState? state, FlowNodeMessage? currentSelection, int? requestedMessageId)
    {
        int issueCount = messages.Count(static message => message.State is FlowMessageState.Fail or FlowMessageState.Timeout);
        if (nodeKey == null && state == null)
        {
            FlowNodeMessage? preferred = requestedMessageId is int id ? messages.FirstOrDefault(message => message.Id == id) : null;
            if (preferred == null && currentSelection != null && messages.Contains(currentSelection))
                preferred = currentSelection;
            return Finish(messages.Take(MaximumDisplayedMessages).ToList(), preferred, messages.Count, issueCount);
        }

        var displayed = new List<FlowNodeMessage>();
        int matchingCount = 0;
        FlowNodeMessage? requested = null;
        bool currentMatches = false;
        foreach (FlowNodeMessage message in messages)
        {
            if ((state.HasValue && message.State != state.Value)
                || (nodeKey.HasValue && !nodeKey.Value.Matches(message)))
                continue;

            matchingCount++;
            if (displayed.Count < MaximumDisplayedMessages)
                displayed.Add(message);
            if (requested == null && message.Id == requestedMessageId)
                requested = message;
            if (!currentMatches && currentSelection != null)
                currentMatches = EqualityComparer<FlowNodeMessage>.Default.Equals(message, currentSelection);
        }

        return Finish(displayed, requested ?? (currentMatches ? currentSelection : null), matchingCount, issueCount);
    }

    private static FlowMessageListProjection Finish(List<FlowNodeMessage> displayed,
        FlowNodeMessage? selected, int matchingCount, int issueCount)
    {
        // Keep the requested or previously selected message visible beyond the cap.
        if (selected != null && displayed.Count == MaximumDisplayedMessages && !displayed.Contains(selected))
            displayed[^1] = selected;
        selected ??= displayed.Count > 0 ? displayed[0] : null;
        return new(displayed, selected, matchingCount, issueCount);
    }
}
