using ColorVision.Engine.Templates.Flow.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public sealed record StoredFlowSubflowDefinition(
    string FlowKey,
    int Revision,
    string SidecarHash,
    FlowSubflowSidecar Sidecar);

/// <summary>
/// Stores the authoring-only subflow metadata for one immutable flow revision.
/// Implementations must never add this data to STN/CVFlow payloads.
/// </summary>
public interface IFlowSubflowDefinitionStore
{
    StoredFlowSubflowDefinition? GetRevision(
        string flowKey,
        int revision);

    /// <summary>
    /// Appends the sidecar for a revision. Repeating the same content is
    /// idempotent; attempting to replace it with different content conflicts.
    /// </summary>
    StoredFlowSubflowDefinition Append(
        string flowKey,
        int revision,
        FlowSubflowSidecar sidecar);
}

public sealed class FlowSubflowDefinitionConflictException :
    InvalidOperationException
{
    public FlowSubflowDefinitionConflictException(
        string flowKey,
        int revision,
        string existingHash,
        string incomingHash)
        : base(
            $"流程 {flowKey} 的子流程侧车版本 {revision} 已存在且内容不同；"
            + $"已有 {existingHash}，写入 {incomingHash}。")
    {
        FlowKey = flowKey;
        Revision = revision;
        ExistingHash = existingHash;
        IncomingHash = incomingHash;
    }

    public string FlowKey { get; }

    public int Revision { get; }

    public string ExistingHash { get; }

    public string IncomingHash { get; }
}

/// <summary>
/// Append-only JSON store. Each revision owns one immutable sidecar file:
/// flows/{flow-key-sha256}/revisions/{revision}/subflow.json.
/// </summary>
public sealed class JsonFlowSubflowDefinitionStore :
    IFlowSubflowDefinitionStore
{
    private const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions PersistedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal static JsonSerializerOptions CanonicalJsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string rootDirectory;

    public JsonFlowSubflowDefinitionStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "子流程侧车根目录不能为空。",
                nameof(rootDirectory));
        }

        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    public string RootDirectory => rootDirectory;

    public StoredFlowSubflowDefinition? GetRevision(
        string flowKey,
        int revision)
    {
        string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
        ValidateRevision(revision);
        string path = GetDefinitionPath(key, revision);
        return File.Exists(path)
            ? Read(path, key, revision)
            : null;
    }

    public StoredFlowSubflowDefinition Append(
        string flowKey,
        int revision,
        FlowSubflowSidecar sidecar)
    {
        string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
        ValidateRevision(revision);
        FlowSubflowSidecar normalized =
            FlowSubflowSidecarPersistence.Normalize(sidecar);
        byte[] canonical =
            FlowSubflowSidecarPersistence.SerializeCanonical(normalized);
        string sidecarHash = ComputeHash(canonical);
        string path = GetDefinitionPath(key, revision);

        if (File.Exists(path))
        {
            return ResolveExisting(
                path,
                key,
                revision,
                sidecarHash);
        }

        string? directory = Path.GetDirectoryName(path);
        if (directory == null)
            throw new InvalidOperationException("无法确定子流程侧车目录。");
        Directory.CreateDirectory(directory);

        var document = PersistedDefinition.From(
            key,
            revision,
            sidecarHash,
            normalized);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            document,
            PersistedJsonOptions);
        string temporaryPath = Path.Combine(
            directory,
            $".subflow.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(
                    temporaryPath,
                    path,
                    overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                return ResolveExisting(
                    path,
                    key,
                    revision,
                    sidecarHash);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new StoredFlowSubflowDefinition(
            key,
            revision,
            sidecarHash,
            FlowSubflowSidecarPersistence.Clone(normalized));
    }

    private static StoredFlowSubflowDefinition ResolveExisting(
        string path,
        string flowKey,
        int revision,
        string incomingHash)
    {
        StoredFlowSubflowDefinition existing =
            Read(path, flowKey, revision);
        if (!string.Equals(
            existing.SidecarHash,
            incomingHash,
            StringComparison.Ordinal))
        {
            throw new FlowSubflowDefinitionConflictException(
                flowKey,
                revision,
                existing.SidecarHash,
                incomingHash);
        }
        return existing;
    }

    private static StoredFlowSubflowDefinition Read(
        string path,
        string expectedFlowKey,
        int expectedRevision)
    {
        PersistedDefinition? document;
        try
        {
            document = JsonSerializer.Deserialize<PersistedDefinition>(
                File.ReadAllBytes(path),
                PersistedJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"子流程侧车不是有效 JSON：{path}",
                ex);
        }

        if (document == null
            || document.FormatVersion != CurrentFormatVersion
            || !string.Equals(
                document.FlowKey,
                expectedFlowKey,
                StringComparison.Ordinal)
            || document.Revision != expectedRevision)
        {
            throw new InvalidDataException(
                $"子流程侧车标识或格式不匹配：{path}");
        }

        FlowSubflowSidecar sidecar =
            FlowSubflowSidecarPersistence.Normalize(
                document.ToSidecar());
        string actualHash = ComputeHash(
            FlowSubflowSidecarPersistence.SerializeCanonical(sidecar));
        string declaredHash;
        try
        {
            declaredHash = FlowRevisionStoreRules.NormalizeHash(
                document.SidecarHash,
                nameof(document.SidecarHash));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(
                $"子流程侧车哈希无效：{path}",
                ex);
        }
        if (!string.Equals(
            declaredHash,
            actualHash,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"子流程侧车内容与哈希不一致：{path}");
        }

        return new StoredFlowSubflowDefinition(
            expectedFlowKey,
            expectedRevision,
            actualHash,
            FlowSubflowSidecarPersistence.Clone(sidecar));
    }

    private string GetDefinitionPath(
        string flowKey,
        int revision)
    {
        string flowHash = ComputeHash(
            Encoding.UTF8.GetBytes(flowKey));
        return Path.Combine(
            rootDirectory,
            "flows",
            flowHash[..2],
            flowHash,
            "revisions",
            revision.ToString("D10", CultureInfo.InvariantCulture),
            "subflow.json");
    }

    private static string ComputeHash(byte[] bytes)
    {
        return Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void ValidateRevision(int revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
    }

    private sealed class PersistedDefinition
    {
        public int FormatVersion { get; set; }

        public string FlowKey { get; set; } = string.Empty;

        public int Revision { get; set; }

        public string SidecarHash { get; set; } = string.Empty;

        public List<PersistedCall> Calls { get; set; } = new();

        public static PersistedDefinition From(
            string flowKey,
            int revision,
            string sidecarHash,
            FlowSubflowSidecar sidecar)
        {
            return new PersistedDefinition
            {
                FormatVersion = CurrentFormatVersion,
                FlowKey = flowKey,
                Revision = revision,
                SidecarHash = sidecarHash,
                Calls = sidecar.Calls
                    .Select(PersistedCall.From)
                    .ToList(),
            };
        }

        public FlowSubflowSidecar ToSidecar()
        {
            IReadOnlyList<PersistedCall> calls =
                Calls ?? new List<PersistedCall>();
            return new FlowSubflowSidecar(
                calls.Select(call => call.ToCall()).ToArray());
        }
    }

    private sealed class PersistedCall
    {
        public string CallId { get; set; } = string.Empty;

        public Guid SourceNodeId { get; set; }

        public int SourceOptionIndex { get; set; }

        public Guid TargetNodeId { get; set; }

        public int TargetOptionIndex { get; set; }

        public string ChildFlowKey { get; set; } = string.Empty;

        public string? ChildRevision { get; set; }

        public string? ChildContentHash { get; set; }

        public static PersistedCall From(FlowSubflowCall call)
        {
            return new PersistedCall
            {
                CallId = call.CallId,
                SourceNodeId = call.Source.NodeId,
                SourceOptionIndex = call.Source.OptionIndex,
                TargetNodeId = call.Target.NodeId,
                TargetOptionIndex = call.Target.OptionIndex,
                ChildFlowKey = call.Child.FlowKey,
                ChildRevision = call.Child.Revision,
                ChildContentHash = call.Child.ContentHash,
            };
        }

        public FlowSubflowCall ToCall()
        {
            return new FlowSubflowCall(
                CallId,
                new FlowPortReference(
                    SourceNodeId,
                    SourceOptionIndex),
                new FlowPortReference(
                    TargetNodeId,
                    TargetOptionIndex),
                new FlowDefinitionReference(
                    ChildFlowKey,
                    ChildRevision,
                    ChildContentHash));
        }
    }
}

internal static class FlowSubflowSidecarPersistence
{
    private sealed class CanonicalSidecar
    {
        public IReadOnlyList<CanonicalCall> Calls { get; init; } =
            Array.Empty<CanonicalCall>();
    }

    private sealed class CanonicalCall
    {
        public string CallId { get; init; } = string.Empty;

        public string SourceNodeId { get; init; } = string.Empty;

        public int SourceOptionIndex { get; init; }

        public string TargetNodeId { get; init; } = string.Empty;

        public int TargetOptionIndex { get; init; }

        public string ChildFlowKey { get; init; } = string.Empty;

        public string? ChildRevision { get; init; }

        public string? ChildContentHash { get; init; }
    }

    public static FlowSubflowSidecar Normalize(
        FlowSubflowSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        IReadOnlyList<FlowSubflowCall> source =
            sidecar.Calls ?? Array.Empty<FlowSubflowCall>();
        var callIds = new HashSet<string>(StringComparer.Ordinal);
        var callSites = new HashSet<CallSite>();
        var normalized = new List<FlowSubflowCall>(source.Count);
        foreach (FlowSubflowCall? call in source)
        {
            if (call == null
                || call.Source == null
                || call.Target == null
                || call.Child == null
                || string.IsNullOrWhiteSpace(call.CallId))
            {
                throw new ArgumentException(
                    "子流程侧车包含不完整的调用。",
                    nameof(sidecar));
            }

            string callId = call.CallId.Trim();
            if (!callIds.Add(callId))
            {
                throw new ArgumentException(
                    $"子流程侧车包含重复 CallId：{callId}。",
                    nameof(sidecar));
            }
            if (call.Source.OptionIndex < 0
                || call.Target.OptionIndex < 0)
            {
                throw new ArgumentException(
                    $"子流程调用 {callId} 的端口索引不能为负数。",
                    nameof(sidecar));
            }

            var callSite = new CallSite(
                call.Source.NodeId,
                call.Source.OptionIndex,
                call.Target.NodeId,
                call.Target.OptionIndex);
            if (!callSites.Add(callSite))
            {
                throw new ArgumentException(
                    $"子流程侧车的同一连接被重复引用：{callId}。",
                    nameof(sidecar));
            }

            string childFlowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(
                    call.Child.FlowKey);
            string? childRevision = NormalizeOptional(
                call.Child.Revision);
            string? childContentHash =
                NormalizeOptionalHash(call.Child.ContentHash);
            normalized.Add(new FlowSubflowCall(
                callId,
                new FlowPortReference(
                    call.Source.NodeId,
                    call.Source.OptionIndex),
                new FlowPortReference(
                    call.Target.NodeId,
                    call.Target.OptionIndex),
                new FlowDefinitionReference(
                    childFlowKey,
                    childRevision,
                    childContentHash)));
        }

        FlowSubflowCall[] ordered = normalized
            .OrderBy(call => call.CallId, StringComparer.Ordinal)
            .ThenBy(call => call.Source.NodeId)
            .ThenBy(call => call.Source.OptionIndex)
            .ThenBy(call => call.Target.NodeId)
            .ThenBy(call => call.Target.OptionIndex)
            .ToArray();
        return new FlowSubflowSidecar(
            Array.AsReadOnly(ordered));
    }

    public static FlowSubflowSidecar Clone(
        FlowSubflowSidecar sidecar)
    {
        return Normalize(sidecar);
    }

    public static byte[] SerializeCanonical(
        FlowSubflowSidecar sidecar)
    {
        var document = new CanonicalSidecar
        {
            Calls = sidecar.Calls.Select(call => new CanonicalCall
            {
                CallId = call.CallId,
                SourceNodeId = call.Source.NodeId.ToString("D"),
                SourceOptionIndex = call.Source.OptionIndex,
                TargetNodeId = call.Target.NodeId.ToString("D"),
                TargetOptionIndex = call.Target.OptionIndex,
                ChildFlowKey = call.Child.FlowKey,
                ChildRevision = call.Child.Revision,
                ChildContentHash = call.Child.ContentHash,
            }).ToArray(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(
            document,
            JsonFlowSubflowDefinitionStore.CanonicalJsonOptions);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeOptionalHash(string? value)
    {
        string? normalized = NormalizeOptional(value);
        if (normalized == null)
            return null;
        const string prefix = "sha256:";
        if (normalized.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }
        return FlowRevisionStoreRules.NormalizeHash(
            normalized,
            nameof(value));
    }

    private readonly record struct CallSite(
        Guid SourceNodeId,
        int SourceOptionIndex,
        Guid TargetNodeId,
        int TargetOptionIndex);
}
