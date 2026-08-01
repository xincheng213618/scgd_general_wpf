using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Compilation;

/// <summary>
/// Expands sidecar subflow calls into one ordinary STND v1 graph. Published
/// data therefore remains readable by existing editors and runtime hosts.
/// </summary>
public sealed class FlowSubflowCompiler
{
    private const string DefaultRootFlowKey = "$root";
    private readonly IFlowSubflowResolver _resolver;
    private readonly FlowSubflowCompilerOptions _options;

    public FlowSubflowCompiler(
        IFlowSubflowResolver resolver,
        FlowSubflowCompilerOptions? options = null)
    {
        _resolver = resolver
            ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? new FlowSubflowCompilerOptions();
        ValidateOptions(_options);
    }

    public FlowCompilationResult Compile(
        byte[] parentCanvasData,
        FlowSubflowSidecar? sidecar = null,
        string rootFlowKey = DefaultRootFlowKey,
        string? rootRevision = null)
    {
        ArgumentNullException.ThrowIfNull(parentCanvasData);
        if (string.IsNullOrWhiteSpace(rootFlowKey))
            throw new ArgumentException(
                "根流程 FlowKey 不能为空。",
                nameof(rootFlowKey));

        string rootSourceHash =
            StnV1NeutralCodec.ComputeHash(parentCanvasData);
        var activeDefinitions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        CompilationUnit unit = CompileUnit(
            parentCanvasData,
            sidecar ?? FlowSubflowSidecar.Empty,
            new SourceIdentity(
                rootFlowKey,
                rootRevision,
                rootSourceHash),
            logicalPath: DefaultRootFlowKey,
            rootSourceHash,
            depth: 0,
            activeDefinitions);
        byte[] compiledData = StnV1NeutralCodec.Encode(
            unit.Canvas,
            _options);
        string compiledHash =
            StnV1NeutralCodec.ComputeHash(compiledData);

        Dictionary<Guid, FlowCompiledNodeMap> mapsByNode =
            unit.NodeMaps.ToDictionary(
                map => map.CompiledNodeId);
        FlowCompiledNodeMap[] orderedNodeMaps = unit.Canvas.Nodes
            .Select(node => mapsByNode[node.NodeId])
            .ToArray();
        FlowCompiledCallMap[] orderedCallMaps = unit.CallMaps
            .OrderBy(map =>
                map.LogicalCallPath,
                StringComparer.Ordinal)
            .ToArray();
        return new FlowCompilationResult(
            compiledData,
            compiledHash,
            new FlowCompilationMap(
                orderedNodeMaps,
                orderedCallMaps));
    }

    private CompilationUnit CompileUnit(
        byte[] canvasData,
        FlowSubflowSidecar sidecar,
        SourceIdentity source,
        string logicalPath,
        string rootSourceHash,
        int depth,
        HashSet<string> activeDefinitions)
    {
        if (depth > _options.MaximumDepth)
        {
            throw new FlowCompilationException(
                FlowCompilationError.MaximumDepthExceeded,
                $"子流程嵌套深度超过限制 {_options.MaximumDepth}。");
        }

        NeutralCanvas canvas =
            StnV1NeutralCodec.Decode(canvasData, _options);
        var unit = new CompilationUnit(canvas);
        foreach (NeutralNode node in canvas.Nodes)
        {
            unit.NodeMaps.Add(new FlowCompiledNodeMap(
                node.NodeId,
                node.NodeId,
                source.FlowKey,
                source.Revision,
                source.ContentHash,
                $"{logicalPath}/nodes/{node.NodeId:N}"));
        }

        FlowSubflowCall[] calls = ValidateAndOrderCalls(
            sidecar,
            logicalPath);
        foreach ((FlowSubflowCall call, NeutralConnection callSite)
                 in ResolveCallSites(unit.Canvas, calls))
        {
            if (callSite.Output.Schema.IsLoop
                || callSite.Input.Schema.IsLoop)
            {
                throw new FlowCompilationException(
                    FlowCompilationError.LoopBoundaryNotAllowed,
                    $"子流程调用 {call.CallId} 不能跨越循环端口。");
            }

            ResolvedChild resolved = ResolveChild(call.Child);
            string childLogicalPath =
                $"{logicalPath}/calls/{EscapePath(call.CallId)}";
            string activeKey = BuildActiveKey(resolved);
            if (!activeDefinitions.Add(activeKey))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.RecursiveReference,
                    $"检测到递归子流程引用：{resolved.FlowKey} " +
                    $"({resolved.Revision ?? resolved.ContentHash})。");
            }

            CompilationUnit child;
            try
            {
                child = CompileUnit(
                    resolved.CanvasData,
                    resolved.Sidecar,
                    new SourceIdentity(
                        resolved.FlowKey,
                        resolved.Revision,
                        resolved.ContentHash),
                    childLogicalPath,
                    rootSourceHash,
                    depth + 1,
                    activeDefinitions);
            }
            finally
            {
                activeDefinitions.Remove(activeKey);
            }

            ImportChild(
                unit,
                call,
                callSite,
                child,
                rootSourceHash);
            unit.CallMaps.Add(new FlowCompiledCallMap(
                childLogicalPath,
                call.Child,
                resolved.FlowKey,
                resolved.Revision,
                resolved.ContentHash));
            unit.CallMaps.AddRange(child.CallMaps);
            StnV1NeutralCodec.ValidateCounts(
                unit.Canvas,
                _options);
        }
        return unit;
    }

    private ResolvedChild ResolveChild(
        FlowDefinitionReference requested)
    {
        if (requested == null
            || string.IsNullOrWhiteSpace(requested.FlowKey))
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidCallSite,
                "子流程引用缺少 FlowKey。");
        }

        ResolvedFlowDefinition? definition =
            _resolver.Resolve(requested);
        if (definition == null)
        {
            throw new FlowCompilationException(
                FlowCompilationError.MissingDefinition,
                $"找不到子流程：{requested.FlowKey} " +
                $"({requested.Revision ?? requested.ContentHash ?? "latest"})。");
        }
        if (!string.Equals(
                requested.FlowKey,
                definition.FlowKey,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(definition.FlowKey))
        {
            throw new FlowCompilationException(
                FlowCompilationError.ReferenceMismatch,
                $"解析结果 FlowKey 不匹配：请求 {requested.FlowKey}，" +
                $"返回 {definition.FlowKey}。");
        }
        if (!string.IsNullOrWhiteSpace(requested.Revision)
            && !string.Equals(
                requested.Revision,
                definition.Revision,
                StringComparison.Ordinal))
        {
            throw new FlowCompilationException(
                FlowCompilationError.ReferenceMismatch,
                $"子流程 {requested.FlowKey} 的固定版本不匹配：" +
                $"请求 {requested.Revision}，返回 " +
                $"{definition.Revision ?? "<null>"}。");
        }
        if (definition.CanvasData == null)
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidCanvas,
                $"子流程 {requested.FlowKey} 没有画布数据。");
        }

        string actualHash =
            StnV1NeutralCodec.ComputeHash(definition.CanvasData);
        ValidateHash(
            requested.ContentHash,
            actualHash,
            $"子流程 {requested.FlowKey} 的请求哈希");
        ValidateHash(
            definition.ContentHash,
            actualHash,
            $"子流程 {requested.FlowKey} 的解析哈希");
        return new ResolvedChild(
            definition.FlowKey,
            definition.Revision,
            actualHash,
            definition.CanvasData,
            definition.Sidecar ?? FlowSubflowSidecar.Empty);
    }

    private static void ValidateHash(
        string? expected,
        string actual,
        string name)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return;
        string normalized = NormalizeHash(expected);
        if (!string.Equals(
                normalized,
                actual,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FlowCompilationException(
                FlowCompilationError.HashMismatch,
                $"{name}不匹配：期望 {normalized}，实际 {actual}。");
        }
    }

    private static string NormalizeHash(string value)
    {
        string normalized = value.Trim();
        const string prefix = "sha256:";
        if (normalized.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }
        return normalized.Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static FlowSubflowCall[] ValidateAndOrderCalls(
        FlowSubflowSidecar sidecar,
        string logicalPath)
    {
        IReadOnlyList<FlowSubflowCall> sourceCalls =
            sidecar.Calls ?? Array.Empty<FlowSubflowCall>();
        var callIds = new HashSet<string>(StringComparer.Ordinal);
        var callSites = new HashSet<CallSiteKey>();
        foreach (FlowSubflowCall call in sourceCalls)
        {
            if (call == null
                || string.IsNullOrWhiteSpace(call.CallId)
                || call.Source == null
                || call.Target == null
                || call.Child == null)
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCallSite,
                    $"{logicalPath} 包含不完整的子流程调用。");
            }
            if (!callIds.Add(call.CallId))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.DuplicateCall,
                    $"{logicalPath} 包含重复 CallId：{call.CallId}。");
            }
            if (!callSites.Add(new CallSiteKey(
                    call.Source.NodeId,
                    call.Source.OptionIndex,
                    call.Target.NodeId,
                    call.Target.OptionIndex)))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.DuplicateCall,
                    $"{logicalPath} 的同一连接被多个子流程调用替换。");
            }
        }
        return sourceCalls
            .OrderBy(call => call.CallId, StringComparer.Ordinal)
            .ThenBy(call => call.Source.NodeId)
            .ThenBy(call => call.Source.OptionIndex)
            .ThenBy(call => call.Target.NodeId)
            .ThenBy(call => call.Target.OptionIndex)
            .ToArray();
    }

    private static (
        FlowSubflowCall Call,
        NeutralConnection Connection)[] ResolveCallSites(
        NeutralCanvas canvas,
        FlowSubflowCall[] calls)
    {
        Dictionary<Guid, NeutralNode> nodesById =
            canvas.Nodes.ToDictionary(node => node.NodeId);
        Dictionary<CallSiteKey, NeutralConnection> connections =
            canvas.Connections.ToDictionary(
                connection => new CallSiteKey(
                    connection.Output.Node.NodeId,
                    connection.Output.LocalIndex,
                    connection.Input.Node.NodeId,
                    connection.Input.LocalIndex));
        var result = new (
            FlowSubflowCall,
            NeutralConnection)[calls.Length];
        for (int i = 0; i < calls.Length; i++)
        {
            FlowSubflowCall call = calls[i];
            if (!nodesById.TryGetValue(
                    call.Source.NodeId,
                    out NeutralNode? sourceNode)
                || !nodesById.TryGetValue(
                    call.Target.NodeId,
                    out NeutralNode? targetNode)
                || call.Source.OptionIndex < 0
                || call.Source.OptionIndex
                    >= sourceNode.Outputs.Length
                || call.Target.OptionIndex < 0
                || call.Target.OptionIndex
                    >= targetNode.Inputs.Length)
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCallSite,
                    $"子流程调用 {call.CallId} 引用了不存在的节点或端口。");
            }
            var key = new CallSiteKey(
                call.Source.NodeId,
                call.Source.OptionIndex,
                call.Target.NodeId,
                call.Target.OptionIndex);
            if (!connections.TryGetValue(
                    key,
                    out NeutralConnection? connection))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCallSite,
                    $"子流程调用 {call.CallId} 必须替换一条已存在的连接。");
            }
            result[i] = (call, connection);
        }
        return result;
    }

    private static void ImportChild(
        CompilationUnit parent,
        FlowSubflowCall call,
        NeutralConnection callSite,
        CompilationUnit child,
        string rootSourceHash)
    {
        ChildBoundary boundary = ValidateChildBoundary(
            child.Canvas,
            call.CallId);
        Dictionary<Guid, FlowCompiledNodeMap> childMaps =
            child.NodeMaps.ToDictionary(map => map.CompiledNodeId);
        var clonedNodes =
            new Dictionary<NeutralNode, NeutralNode>();
        var importedMaps =
            new Dictionary<NeutralNode, FlowCompiledNodeMap>();
        var occupiedIds = parent.Canvas.Nodes
            .Select(node => node.NodeId)
            .ToHashSet();
        foreach (NeutralNode node in child.Canvas.Nodes)
        {
            if (!childMaps.TryGetValue(
                    node.NodeId,
                    out FlowCompiledNodeMap? sourceMap))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCanvas,
                    $"子流程 {call.CallId} 缺少节点映射：{node.NodeId}。");
            }
            Guid compiledNodeId =
                StnV1NeutralCodec.CreateDeterministicGuid(
                    rootSourceHash,
                    sourceMap.LogicalPath,
                    node.NodeId);
            if (!occupiedIds.Add(compiledNodeId))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCanvas,
                    $"子流程 {call.CallId} 的确定性节点 ID 冲突：" +
                    $"{compiledNodeId}。");
            }
            NeutralNode clone = node.WithNodeId(compiledNodeId);
            clonedNodes.Add(node, clone);
            importedMaps.Add(
                clone,
                sourceMap with
                {
                    CompiledNodeId = compiledNodeId,
                });
        }

        NeutralNode clonedStart = clonedNodes[boundary.Start];
        NeutralNode clonedEnd = clonedNodes[boundary.End];
        NeutralPort? entryInput = boundary.IsDirect
            ? null
            : clonedNodes[boundary.Entry.Input.Node]
                .Inputs[boundary.Entry.Input.LocalIndex];
        NeutralPort? exitOutput = boundary.IsDirect
            ? null
            : clonedNodes[boundary.Exit.Output.Node]
                .Outputs[boundary.Exit.Output.LocalIndex];

        parent.Canvas.Connections.Remove(callSite);
        foreach (NeutralNode original in child.Canvas.Nodes)
        {
            NeutralNode clone = clonedNodes[original];
            if (ReferenceEquals(clone, clonedStart)
                || ReferenceEquals(clone, clonedEnd))
            {
                continue;
            }
            parent.Canvas.Nodes.Add(clone);
            parent.NodeMaps.Add(importedMaps[clone]);
        }
        foreach (NeutralConnection connection
                     in child.Canvas.Connections)
        {
            if (ReferenceEquals(
                    connection.Output.Node,
                    boundary.Start)
                || ReferenceEquals(
                    connection.Input.Node,
                    boundary.Start)
                || ReferenceEquals(
                    connection.Output.Node,
                    boundary.End)
                || ReferenceEquals(
                    connection.Input.Node,
                    boundary.End))
            {
                continue;
            }
            NeutralNode outputNode =
                clonedNodes[connection.Output.Node];
            NeutralNode inputNode =
                clonedNodes[connection.Input.Node];
            parent.Canvas.Connections.Add(
                new NeutralConnection(
                    outputNode.Outputs[
                        connection.Output.LocalIndex],
                    inputNode.Inputs[
                        connection.Input.LocalIndex]));
        }

        if (boundary.IsDirect)
        {
            EnsureCompatible(
                callSite.Output,
                callSite.Input,
                call.CallId);
            parent.Canvas.Connections.Add(
                new NeutralConnection(
                    callSite.Output,
                    callSite.Input));
        }
        else
        {
            EnsureCompatible(
                callSite.Output,
                entryInput!,
                call.CallId);
            EnsureCompatible(
                exitOutput!,
                callSite.Input,
                call.CallId);
            parent.Canvas.Connections.Add(
                new NeutralConnection(
                    callSite.Output,
                    entryInput!));
            parent.Canvas.Connections.Add(
                new NeutralConnection(
                    exitOutput!,
                    callSite.Input));
        }
    }

    private static ChildBoundary ValidateChildBoundary(
        NeutralCanvas child,
        string callId)
    {
        NeutralNode[] starts = child.Nodes
            .Where(node => node.Schema.IsStart)
            .ToArray();
        NeutralNode[] ends = child.Nodes
            .Where(node => node.Schema.IsEnd)
            .ToArray();
        if (starts.Length != 1 || ends.Length != 1)
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidChildBoundary,
                $"子流程 {callId} 必须恰好包含一个 BaseStartNode " +
                $"和一个 CVEndNode；当前为 {starts.Length}/{ends.Length}。");
        }

        NeutralNode start = starts[0];
        NeutralNode end = ends[0];
        if (start.Schema.PrimaryStartOutputIndex < 0
            || end.Schema.PrimaryEndInputIndex < 0)
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidChildBoundary,
                $"子流程 {callId} 无法识别主入口或主出口。");
        }

        NeutralConnection[] touchingBoundary =
            child.Connections.Where(connection =>
                ReferenceEquals(connection.Output.Node, start)
                || ReferenceEquals(connection.Input.Node, start)
                || ReferenceEquals(connection.Output.Node, end)
                || ReferenceEquals(connection.Input.Node, end))
            .ToArray();
        if (touchingBoundary.Any(connection =>
                connection.Output.Schema.IsLoop
                || connection.Input.Schema.IsLoop))
        {
            throw new FlowCompilationException(
                FlowCompilationError.LoopBoundaryNotAllowed,
                $"子流程 {callId} 的入口或出口不能使用循环端口。");
        }

        NeutralConnection[] incomingToStart =
            child.Connections.Where(connection =>
                ReferenceEquals(connection.Input.Node, start))
            .ToArray();
        NeutralConnection[] outgoingFromEnd =
            child.Connections.Where(connection =>
                ReferenceEquals(connection.Output.Node, end))
            .ToArray();
        NeutralConnection[] entries =
            child.Connections.Where(connection =>
                ReferenceEquals(connection.Output.Node, start))
            .ToArray();
        NeutralConnection[] exits =
            child.Connections.Where(connection =>
                ReferenceEquals(connection.Input.Node, end))
            .ToArray();
        if (incomingToStart.Length != 0
            || outgoingFromEnd.Length != 0
            || entries.Length != 1
            || exits.Length != 1
            || entries[0].Output.LocalIndex
                != start.Schema.PrimaryStartOutputIndex
            || exits[0].Input.LocalIndex
                != end.Schema.PrimaryEndInputIndex)
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidChildBoundary,
                $"子流程 {callId} 必须是单入口、单出口，且入口/出口" +
                "只能使用主流程端口。");
        }

        bool isDirect = ReferenceEquals(entries[0], exits[0]);
        if (!isDirect
            && (ReferenceEquals(entries[0].Input.Node, end)
                || ReferenceEquals(exits[0].Output.Node, start)))
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidChildBoundary,
                $"子流程 {callId} 的入口/出口边界不完整。");
        }
        return new ChildBoundary(
            start,
            end,
            entries[0],
            exits[0],
            isDirect);
    }

    private static void EnsureCompatible(
        NeutralPort output,
        NeutralPort input,
        string callId)
    {
        if (output.Schema.IsEmpty
            || input.Schema.IsEmpty
            || output.IsInput
            || !input.IsInput
            || (!ReferenceEquals(
                    output.DataType,
                    input.DataType)
                && !input.DataType.IsAssignableFrom(
                    output.DataType)))
        {
            throw new FlowCompilationException(
                FlowCompilationError.IncompatibleBoundaryType,
                $"子流程调用 {callId} 展开后的端口类型不兼容：" +
                $"{output.DataType.FullName} -> " +
                $"{input.DataType.FullName}。");
        }
    }

    private static string EscapePath(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string BuildActiveKey(ResolvedChild child)
    {
        return $"{child.FlowKey}\u001f{child.Revision}\u001f" +
            child.ContentHash;
    }

    private static void ValidateOptions(
        FlowSubflowCompilerOptions options)
    {
        if (options.MaximumDepth <= 0
            || options.MaximumDepth
                > FlowSubflowCompilerOptions.DefaultMaximumDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"最大嵌套深度必须在 1 到 " +
                $"{FlowSubflowCompilerOptions.DefaultMaximumDepth} 之间。");
        }
        if (options.MaximumNodeCount <= 0
            || options.MaximumNodeCount
                > FlowSubflowCompilerOptions.DefaultMaximumNodeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"最大节点数必须在 1 到 " +
                $"{FlowSubflowCompilerOptions.DefaultMaximumNodeCount} 之间。");
        }
        if (options.MaximumConnectionCount <= 0
            || options.MaximumConnectionCount
                > FlowSubflowCompilerOptions
                    .DefaultMaximumConnectionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"最大连接数必须在 1 到 " +
                $"{FlowSubflowCompilerOptions.DefaultMaximumConnectionCount} 之间。");
        }
    }

    private sealed class CompilationUnit
    {
        public CompilationUnit(NeutralCanvas canvas)
        {
            Canvas = canvas;
        }

        public NeutralCanvas Canvas { get; }

        public List<FlowCompiledNodeMap> NodeMaps { get; } = new();

        public List<FlowCompiledCallMap> CallMaps { get; } = new();
    }

    private sealed record SourceIdentity(
        string FlowKey,
        string? Revision,
        string ContentHash);

    private sealed record ResolvedChild(
        string FlowKey,
        string? Revision,
        string ContentHash,
        byte[] CanvasData,
        FlowSubflowSidecar Sidecar);

    private sealed record ChildBoundary(
        NeutralNode Start,
        NeutralNode End,
        NeutralConnection Entry,
        NeutralConnection Exit,
        bool IsDirect);

    private sealed record CallSiteKey(
        Guid SourceNodeId,
        int SourceOptionIndex,
        Guid TargetNodeId,
        int TargetOptionIndex);
}
