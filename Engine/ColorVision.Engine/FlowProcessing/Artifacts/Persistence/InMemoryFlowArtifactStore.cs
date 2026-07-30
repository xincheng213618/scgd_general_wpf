using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    /// <summary>
    /// Reference implementation of the shared-store rules. It is useful for
    /// tests and for callers that need to validate a complete artifact write
    /// before opening a database transaction.
    /// </summary>
    public sealed class InMemoryFlowArtifactStore : IFlowArtifactStore
    {
        private readonly object sync = new();
        private readonly Dictionary<string, FlowArtifactBlob> blobs =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<FlowArtifactRevision>>
            revisions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FlowArtifactReference>
            references = new(StringComparer.Ordinal);

        public FlowArtifactBlob PutArtifact(byte[] content)
        {
            ArgumentNullException.ThrowIfNull(content);
            byte[] stableContent = (byte[])content.Clone();
            string hash =
                FlowArtifactStoreRules.ComputeBlobHash(stableContent);
            lock (sync)
            {
                return PutArtifactCore(
                    hash,
                    stableContent,
                    DateTime.UtcNow)
                    .DeepClone();
            }
        }

        public FlowArtifactBlob? GetArtifact(string hash)
        {
            string normalizedHash =
                FlowArtifactStoreRules.NormalizeHash(
                    hash,
                    nameof(hash));
            lock (sync)
            {
                return blobs.TryGetValue(
                    normalizedHash,
                    out FlowArtifactBlob? blob)
                    ? blob.DeepClone()
                    : null;
            }
        }

        public FlowArtifactReference? GetReference(string flowKey)
        {
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                return references.TryGetValue(
                    key,
                    out FlowArtifactReference? reference)
                    ? reference.DeepClone()
                    : null;
            }
        }

        public FlowArtifactRevision? GetHead(string flowKey)
        {
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                if (!references.TryGetValue(
                        key,
                        out FlowArtifactReference? reference)
                    || reference.HeadRevision == null)
                {
                    return null;
                }
                return GetRevisionCore(
                    key,
                    reference.HeadRevision.Value)
                    ?.DeepClone();
            }
        }

        public FlowArtifactRevision? GetPublished(string flowKey)
        {
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                if (!references.TryGetValue(
                        key,
                        out FlowArtifactReference? reference)
                    || reference.PublishedRevision == null)
                {
                    return null;
                }
                return GetRevisionCore(
                    key,
                    reference.PublishedRevision.Value)
                    ?.DeepClone();
            }
        }

        public FlowArtifactRevision? GetRevision(
            string flowKey,
            int revision)
        {
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                revision);
            lock (sync)
            {
                return GetRevisionCore(key, revision)?.DeepClone();
            }
        }

        public IReadOnlyList<FlowArtifactRevision> ListRevisions(
            string flowKey)
        {
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowArtifactRevision>? values)
                    ? values
                        .Select(item => item.DeepClone())
                        .ToArray()
                    : Array.Empty<FlowArtifactRevision>();
            }
        }

        public FlowArtifactRevision Append(
            FlowArtifactRevisionWriteRequest request)
        {
            PreparedFlowArtifactRevision prepared =
                FlowArtifactStoreRules.Prepare(request);
            lock (sync)
            {
                FlowArtifactReference current =
                    GetReferenceOrEmpty(prepared.FlowKey);
                FlowArtifactStoreRules.EnsureExpectedHead(
                    prepared.FlowKey,
                    prepared.ExpectedHead,
                    current);

                foreach (PreparedFlowArtifactPart artifact in
                    prepared.Artifacts
                        .OrderBy(
                            item => item.Hash,
                            StringComparer.Ordinal))
                {
                    PutArtifactCore(
                        artifact.Hash,
                        artifact.Content,
                        prepared.CreatedTimeUtc);
                }

                int revisionNumber = current.LastRevision + 1;
                FlowArtifactRevisionState state =
                    prepared.PublishImmediately
                        ? FlowArtifactRevisionState.Published
                        : FlowArtifactRevisionState.Draft;
                FlowArtifactRevision revision = new()
                {
                    FlowKey = prepared.FlowKey,
                    Revision = revisionNumber,
                    ParentRevision = current.HeadRevision,
                    RevisionHash = prepared.RevisionHash,
                    State = state,
                    Artifacts = prepared.Artifacts
                        .Select(item => new FlowArtifactDescriptor
                        {
                            Role = item.Role,
                            Hash = item.Hash,
                            ContentLength = item.Content.Length,
                            ContentType = item.ContentType,
                        })
                        .ToArray(),
                    Dependencies = prepared.Dependencies
                        .Select(item => item.DeepClone())
                        .ToArray(),
                    Source = prepared.Source,
                    Author = prepared.Author,
                    Message = prepared.Message,
                    ExternalVersion = prepared.ExternalVersion,
                    CreatedTimeUtc = prepared.CreatedTimeUtc,
                    StateChangedTimeUtc = prepared.CreatedTimeUtc,
                    StateChangedBy = prepared.PublishImmediately
                        ? prepared.Author
                        : null,
                    StateChangeMessage = prepared.PublishImmediately
                        ? prepared.Message
                        : null,
                };

                if (!revisions.TryGetValue(
                        prepared.FlowKey,
                        out List<FlowArtifactRevision>? values))
                {
                    values = new List<FlowArtifactRevision>();
                    revisions.Add(prepared.FlowKey, values);
                }
                values.Add(revision);

                references[prepared.FlowKey] =
                    CreateAdvancedReference(
                        current,
                        revision,
                        prepared.PublishImmediately);
                return revision.DeepClone();
            }
        }

        public FlowArtifactRevision Publish(
            FlowArtifactRevisionTransitionRequest request)
        {
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            lock (sync)
            {
                FlowArtifactReference current =
                    GetReferenceOrEmpty(prepared.FlowKey);
                EnsureTransitionTargetsHead(prepared, current);
                FlowArtifactRevision revision =
                    GetRequiredRevision(
                        prepared.FlowKey,
                        prepared.Revision);

                if (revision.State
                    == FlowArtifactRevisionState.Published)
                {
                    if (current.PublishedRevision
                            != revision.Revision
                        || !string.Equals(
                            current.PublishedRevisionHash,
                            revision.RevisionHash,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "流程版本已标记发布，但 published 引用不一致。");
                    }
                    return revision.DeepClone();
                }
                if (revision.State
                    != FlowArtifactRevisionState.Draft)
                {
                    throw new InvalidOperationException(
                        $"只有 Draft 版本可以发布，当前状态为 "
                        + $"{revision.State}。");
                }

                FlowArtifactRevision published = WithState(
                    revision,
                    FlowArtifactRevisionState.Published,
                    prepared);
                ReplaceRevision(published);
                references[prepared.FlowKey] =
                    new FlowArtifactReference
                    {
                        FlowKey = current.FlowKey,
                        LastRevision = current.LastRevision,
                        HeadRevision = current.HeadRevision,
                        HeadRevisionHash = current.HeadRevisionHash,
                        PublishedRevision = published.Revision,
                        PublishedRevisionHash =
                            published.RevisionHash,
                        UpdatedTimeUtc =
                            prepared.ChangedTimeUtc,
                    };
                return published.DeepClone();
            }
        }

        public FlowArtifactRevision Abort(
            FlowArtifactRevisionTransitionRequest request)
        {
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            lock (sync)
            {
                FlowArtifactReference current =
                    GetReferenceOrEmpty(prepared.FlowKey);
                EnsureTransitionTargetsHead(prepared, current);
                FlowArtifactRevision revision =
                    GetRequiredRevision(
                        prepared.FlowKey,
                        prepared.Revision);
                if (revision.State
                    != FlowArtifactRevisionState.Draft)
                {
                    throw new InvalidOperationException(
                        $"只有 Draft 版本可以中止，当前状态为 "
                        + $"{revision.State}。");
                }

                FlowArtifactRevision aborted = WithState(
                    revision,
                    FlowArtifactRevisionState.Aborted,
                    prepared);
                ReplaceRevision(aborted);
                FlowArtifactRevision? parent =
                    aborted.ParentRevision == null
                        ? null
                        : GetRequiredRevision(
                            prepared.FlowKey,
                            aborted.ParentRevision.Value);
                references[prepared.FlowKey] =
                    new FlowArtifactReference
                    {
                        FlowKey = current.FlowKey,
                        LastRevision = current.LastRevision,
                        HeadRevision = parent?.Revision,
                        HeadRevisionHash = parent?.RevisionHash,
                        PublishedRevision =
                            current.PublishedRevision,
                        PublishedRevisionHash =
                            current.PublishedRevisionHash,
                        UpdatedTimeUtc =
                            prepared.ChangedTimeUtc,
                    };
                return aborted.DeepClone();
            }
        }

        private FlowArtifactBlob PutArtifactCore(
            string hash,
            byte[] content,
            DateTime createdTimeUtc)
        {
            if (blobs.TryGetValue(
                hash,
                out FlowArtifactBlob? existing))
            {
                if (existing.Content.Length != content.Length
                    || !CryptographicOperations.FixedTimeEquals(
                        existing.Content,
                        content))
                {
                    throw new InvalidOperationException(
                        $"检测到 artifact 哈希冲突：{hash}。");
                }
                return existing;
            }

            FlowArtifactBlob created = new()
            {
                Hash = hash,
                Content = (byte[])content.Clone(),
                CreatedTimeUtc =
                    FlowArtifactStoreRules.NormalizeUtc(
                        createdTimeUtc),
            };
            blobs.Add(hash, created);
            return created;
        }

        private FlowArtifactReference GetReferenceOrEmpty(
            string flowKey)
        {
            if (references.TryGetValue(
                flowKey,
                out FlowArtifactReference? existing))
            {
                return existing;
            }
            FlowArtifactReference created = new()
            {
                FlowKey = flowKey,
                UpdatedTimeUtc = DateTime.UtcNow,
            };
            return created;
        }

        private FlowArtifactRevision? GetRevisionCore(
            string flowKey,
            int revision)
        {
            return revisions.TryGetValue(
                flowKey,
                out List<FlowArtifactRevision>? values)
                ? values.FirstOrDefault(
                    item => item.Revision == revision)
                : null;
        }

        private FlowArtifactRevision GetRequiredRevision(
            string flowKey,
            int revision)
        {
            return GetRevisionCore(flowKey, revision)
                ?? throw new InvalidOperationException(
                    $"找不到流程 {flowKey} 的共享版本 {revision}。");
        }

        private static void EnsureTransitionTargetsHead(
            PreparedFlowArtifactTransition prepared,
            FlowArtifactReference current)
        {
            FlowArtifactStoreRules.EnsureExpectedHead(
                prepared.FlowKey,
                prepared.ExpectedHead,
                current);
            if (current.HeadRevision != prepared.Revision)
            {
                throw new InvalidOperationException(
                    "只能发布或中止当前 head 版本。");
            }
        }

        private void ReplaceRevision(
            FlowArtifactRevision replacement)
        {
            List<FlowArtifactRevision> values =
                revisions[replacement.FlowKey];
            int index = values.FindIndex(
                item => item.Revision == replacement.Revision);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"找不到流程 {replacement.FlowKey} "
                    + $"的共享版本 {replacement.Revision}。");
            }
            values[index] = replacement;
        }

        private static FlowArtifactReference
            CreateAdvancedReference(
                FlowArtifactReference current,
                FlowArtifactRevision revision,
                bool published)
        {
            return new FlowArtifactReference
            {
                FlowKey = current.FlowKey,
                LastRevision = revision.Revision,
                HeadRevision = revision.Revision,
                HeadRevisionHash = revision.RevisionHash,
                PublishedRevision = published
                    ? revision.Revision
                    : current.PublishedRevision,
                PublishedRevisionHash = published
                    ? revision.RevisionHash
                    : current.PublishedRevisionHash,
                UpdatedTimeUtc = revision.CreatedTimeUtc,
            };
        }

        private static FlowArtifactRevision WithState(
            FlowArtifactRevision revision,
            FlowArtifactRevisionState state,
            PreparedFlowArtifactTransition transition)
        {
            return new FlowArtifactRevision
            {
                FlowKey = revision.FlowKey,
                Revision = revision.Revision,
                ParentRevision = revision.ParentRevision,
                RevisionHash = revision.RevisionHash,
                State = state,
                Artifacts = revision.Artifacts
                    .Select(item => item.DeepClone())
                    .ToArray(),
                Dependencies = revision.Dependencies
                    .Select(item => item.DeepClone())
                    .ToArray(),
                Source = revision.Source,
                Author = revision.Author,
                Message = revision.Message,
                ExternalVersion = revision.ExternalVersion,
                CreatedTimeUtc = revision.CreatedTimeUtc,
                StateChangedTimeUtc =
                    transition.ChangedTimeUtc,
                StateChangedBy = transition.Actor,
                StateChangeMessage = transition.Message,
            };
        }
    }
}
