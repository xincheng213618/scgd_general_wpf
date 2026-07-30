using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public sealed record FlowSubflowConnectionChoice(
    FlowPortReference Source,
    FlowPortReference Target,
    string DisplayName);

public sealed record FlowSubflowTargetChoice(
    string TemplateName,
    string FlowKey,
    int Revision,
    string ContentHash)
{
    public string DisplayName =>
        $"{TemplateName} · r{Revision} · {ContentHash[..8]}";
}

/// <summary>
/// Testable authoring operations for the WPF subflow dialog. All state remains
/// in the sidecar; the editor graph and STN bytes are never mutated.
/// </summary>
public static class FlowSubflowAuthoring
{
    public static IReadOnlyList<FlowSubflowConnectionChoice>
        CaptureConnections(STNodeEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.GetConnectionInfo()
            .Where(connection =>
                connection.Output?.Owner != null
                && connection.Input?.Owner != null)
            .Select(connection =>
            {
                STNode outputNode = connection.Output.Owner;
                STNode inputNode = connection.Input.Owner;
                int outputIndex = Array.IndexOf(
                    outputNode.GetAllOutputOptions(),
                    connection.Output);
                int inputIndex = Array.IndexOf(
                    inputNode.GetAllInputOptions(),
                    connection.Input);
                if (outputIndex < 0 || inputIndex < 0)
                {
                    throw new InvalidOperationException(
                        "画布连接端口不属于对应节点。");
                }
                return new FlowSubflowConnectionChoice(
                    new FlowPortReference(
                        outputNode.Guid,
                        outputIndex),
                    new FlowPortReference(
                        inputNode.Guid,
                        inputIndex),
                    $"{DescribeNode(outputNode)} [OUT {outputIndex}]"
                    + $" → {DescribeNode(inputNode)} [IN {inputIndex}]");
            })
            .OrderBy(
                choice => choice.DisplayName,
                StringComparer.CurrentCulture)
            .ToArray();
    }

    public static FlowSubflowSidecar Upsert(
        FlowSubflowSidecar current,
        string callId,
        FlowSubflowConnectionChoice connection,
        FlowSubflowTargetChoice target)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(callId))
            throw new ArgumentException("调用名称不能为空。", nameof(callId));

        string normalizedCallId = callId.Trim();
        FlowSubflowCall[] retained = current.Calls
            .Where(call =>
                !string.Equals(
                    call.CallId,
                    normalizedCallId,
                    StringComparison.Ordinal)
                && !IsSameConnection(call, connection))
            .ToArray();
        var replacement = new FlowSubflowCall(
            normalizedCallId,
            connection.Source,
            connection.Target,
            new FlowDefinitionReference(
                target.FlowKey,
                target.Revision.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                target.ContentHash));
        return FlowSubflowSidecarPersistence.Normalize(
            new FlowSubflowSidecar(
                retained.Append(replacement).ToArray()));
    }

    public static FlowSubflowSidecar Remove(
        FlowSubflowSidecar current,
        string callId)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (string.IsNullOrWhiteSpace(callId))
            return FlowSubflowSidecarPersistence.Clone(current);
        return FlowSubflowSidecarPersistence.Normalize(
            new FlowSubflowSidecar(
                current.Calls
                    .Where(call => !string.Equals(
                        call.CallId,
                        callId.Trim(),
                        StringComparison.Ordinal))
                    .ToArray()));
    }

    private static bool IsSameConnection(
        FlowSubflowCall call,
        FlowSubflowConnectionChoice connection)
    {
        return call.Source == connection.Source
            && call.Target == connection.Target;
    }

    private static string DescribeNode(STNode node)
    {
        string title = string.IsNullOrWhiteSpace(node.Title)
            ? node.GetType().Name
            : node.Title;
        return $"{title} ({node.Guid:N})";
    }
}
