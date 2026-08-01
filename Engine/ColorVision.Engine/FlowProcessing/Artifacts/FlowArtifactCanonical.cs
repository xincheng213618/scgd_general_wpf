using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

internal static class FlowArtifactCanonical
{
    public static string ComputeHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    public static string ComputeFieldsHash(
        Action<CanonicalWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        using var writer = new CanonicalWriter();
        write(writer);
        return ComputeHash(writer.ToArray());
    }

    public static string ComputePolicyHash(
        string flowKey,
        FlowArtifactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return ComputeHash(
            FlowArtifactSerializer.SerializePolicy(
                flowKey,
                policy.ErrorRoutes,
                policy.RetryPolicies));
    }

    public static NormalizedFlowExecutionPolicy NormalizePolicy(
        string flowKey,
        FlowArtifactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return FlowExecutionPolicyRules.Normalize(
            flowKey,
            policy.ErrorRoutes,
            policy.RetryPolicies);
    }

    public static string ComputeCompilationMapHash(
        FlowCompilationMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return ComputeHash(
            FlowArtifactSerializer.SerializeCompilationMap(map));
    }

    public static string ComputeDependencyHash(
        IReadOnlyList<FlowArtifactDependencyLock> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return ComputeFieldsHash(writer =>
        {
            FlowArtifactDependencyLock[] ordered = dependencies
                .OrderBy(item => item.LogicalCallPath, StringComparer.Ordinal)
                .ThenBy(item => item.FlowKey, StringComparer.Ordinal)
                .ThenBy(item => item.Revision, StringComparer.Ordinal)
                .ThenBy(item => item.ContentHash, StringComparer.Ordinal)
                .ToArray();
            writer.Add(ordered.Length);
            foreach (FlowArtifactDependencyLock dependency in ordered)
            {
                writer.Add(dependency.LogicalCallPath);
                writer.Add(dependency.FlowKey);
                writer.Add(dependency.Revision);
                writer.Add(dependency.ContentHash);
                writer.Add(dependency.DefinitionHash);
            }
        });
    }

    public static string ComputeCompilerHash(
        FlowArtifactCompilerDescriptor compiler)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        return ComputeFieldsHash(writer =>
        {
            writer.Add(compiler.Name);
            writer.Add(compiler.Version);
            writer.Add(compiler.StndVersion);
            writer.Add(compiler.MaximumDepth);
            writer.Add(compiler.MaximumNodeCount);
            writer.Add(compiler.MaximumConnectionCount);
        });
    }

    public static string ComputeDefinitionHash(
        string flowKey,
        string? revision,
        string sourceHash,
        string subflowHash,
        string policyHash,
        string semanticHash,
        string layoutHash)
    {
        return ComputeFieldsHash(writer =>
        {
            writer.Add(flowKey);
            writer.Add(revision);
            writer.Add(sourceHash);
            writer.Add(subflowHash);
            writer.Add(policyHash);
            writer.Add(semanticHash);
            writer.Add(layoutHash);
        });
    }

    public static string ComputeArtifactHash(
        FlowArtifactManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return ComputeFieldsHash(writer =>
        {
            writer.Add(manifest.FormatVersion);
            writer.Add(manifest.FlowKey);
            writer.Add(manifest.Revision);
            writer.Add(manifest.SourceHash);
            writer.Add(manifest.SubflowHash);
            writer.Add(manifest.PolicyHash);
            writer.Add(manifest.SemanticHash);
            writer.Add(manifest.LayoutHash);
            writer.Add(manifest.DefinitionHash);
            writer.Add(manifest.DependencyHash);
            writer.Add(manifest.CompiledStnHash);
            writer.Add(manifest.EffectivePolicyHash);
            writer.Add(manifest.CompilationMapHash);
            writer.Add(manifest.CompilerHash);
        });
    }

    public static FlowExecutionPolicySnapshot CreateSnapshot(
        NormalizedFlowExecutionPolicy normalized)
    {
        return new FlowExecutionPolicySnapshot(
            normalized.FlowKey,
            revision: 0,
            normalized.ContentHash,
            DateTime.UnixEpoch,
            normalized.ErrorRoutes,
            normalized.RetryPolicies);
    }

    internal sealed class CanonicalWriter : IDisposable
    {
        private static readonly UTF8Encoding Utf8 =
            new(encoderShouldEmitUTF8Identifier: false);
        private readonly MemoryStream stream = new();
        private readonly BinaryWriter writer;

        public CanonicalWriter()
        {
            writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        }

        public void Add(string? value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            byte[] bytes = Utf8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        public void Add(int value)
        {
            Add(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Add(Guid value)
        {
            Add(value.ToString("D"));
        }

        public byte[] ToArray()
        {
            writer.Flush();
            return stream.ToArray();
        }

        public void Dispose()
        {
            writer.Dispose();
            stream.Dispose();
        }
    }
}
