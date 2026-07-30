using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    /// <summary>
    /// MySQL authority for authoring and compiled flow artifacts.
    ///
    /// Write lock order is fixed:
    /// 1. compatibility resource row (only when the caller also updates it);
    /// 2. one flow reference row;
    /// 3. content-addressed blobs in ordinal hash order;
    /// 4. revision, parts in role order, dependencies in slot order;
    /// 5. reference update.
    ///
    /// Callers that already own the compatibility-save transaction must use
    /// the *InCurrentTransaction methods. The regular interface methods own
    /// and atomically commit their own transaction.
    /// </summary>
    public sealed class SqlSugarFlowArtifactStore :
        IFlowArtifactStore
    {
        private readonly SqlSugarClient db;
        private readonly Lazy<SchemaAvailability>
            schemaAvailability;

        public SqlSugarFlowArtifactStore(
            SqlSugarClient db,
            bool ensureSchema = false)
        {
            this.db = db
                ?? throw new ArgumentNullException(nameof(db));
            if (db.CurrentConnectionConfig.DbType != DbType.MySql)
            {
                throw new NotSupportedException(
                    "SqlSugarFlowArtifactStore 仅支持 MySQL。");
            }
            if (ensureSchema)
                FlowArtifactSchemaMigrator.EnsureSchema(db);
            schemaAvailability = ensureSchema
                ? new Lazy<SchemaAvailability>(
                    () => SchemaAvailability.Complete)
                : new Lazy<SchemaAvailability>(
                    ProbeSchema,
                    isThreadSafe: true);
        }

        public FlowArtifactBlob PutArtifact(byte[] content)
        {
            EnsureSchemaForWrite();
            ArgumentNullException.ThrowIfNull(content);
            byte[] stableContent = (byte[])content.Clone();
            return ExecuteInOwnTransaction(
                () => PutArtifactInCurrentTransaction(
                    stableContent));
        }

        /// <summary>
        /// Writes a blob using the transaction already active on the supplied
        /// SqlSugar client. This method never commits or rolls back it.
        /// </summary>
        public FlowArtifactBlob PutArtifactInCurrentTransaction(
            byte[] content)
        {
            EnsureSchemaForWrite();
            ArgumentNullException.ThrowIfNull(content);
            byte[] stableContent = (byte[])content.Clone();
            string hash =
                FlowArtifactStoreRules.ComputeBlobHash(stableContent);
            return PutArtifactCore(
                hash,
                stableContent,
                DateTime.UtcNow);
        }

        public FlowArtifactBlob? GetArtifact(string hash)
        {
            if (!EnsureSchemaForRead())
                return null;
            string normalizedHash =
                FlowArtifactStoreRules.NormalizeHash(
                    hash,
                    nameof(hash));
            FlowArtifactBlobModel? model =
                db.Queryable<FlowArtifactBlobModel>()
                    .Where(item =>
                        item.Hash == normalizedHash)
                    .First();
            return model == null
                ? null
                : ToDomain(model);
        }

        public FlowArtifactReference? GetReference(string flowKey)
        {
            if (!EnsureSchemaForRead())
                return null;
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            FlowArtifactReferenceModel? model =
                db.Queryable<FlowArtifactReferenceModel>()
                    .Where(item => item.FlowKey == key)
                    .First();
            return model == null
                ? null
                : ToDomain(model);
        }

        public FlowArtifactRevision? GetHead(string flowKey)
        {
            FlowArtifactReference? reference =
                GetReference(flowKey);
            if (reference?.HeadRevision == null)
                return null;
            FlowArtifactRevision revision =
                GetRevision(
                    reference.FlowKey,
                    reference.HeadRevision.Value)
                ?? throw new InvalidOperationException(
                    "共享流程 head 引用指向不存在的版本。");
            if (!string.Equals(
                reference.HeadRevisionHash,
                revision.RevisionHash,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "共享流程 head 引用的版本哈希不一致。");
            }
            return revision;
        }

        public FlowArtifactRevision? GetPublished(string flowKey)
        {
            FlowArtifactReference? reference =
                GetReference(flowKey);
            if (reference?.PublishedRevision == null)
                return null;
            FlowArtifactRevision revision =
                GetRevision(
                    reference.FlowKey,
                    reference.PublishedRevision.Value)
                ?? throw new InvalidOperationException(
                    "共享流程 published 引用指向不存在的版本。");
            if (revision.State
                    != FlowArtifactRevisionState.Published
                || !string.Equals(
                    reference.PublishedRevisionHash,
                    revision.RevisionHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "共享流程 published 引用与版本状态或哈希不一致。");
            }
            return revision;
        }

        public FlowArtifactRevision? GetRevision(
            string flowKey,
            int revision)
        {
            if (!EnsureSchemaForRead())
                return null;
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                revision);
            FlowArtifactRevisionModel? model =
                db.Queryable<FlowArtifactRevisionModel>()
                    .Where(item =>
                        item.FlowKey == key
                        && item.Revision == revision)
                    .First();
            return model == null
                ? null
                : LoadRevision(model);
        }

        public IReadOnlyList<FlowArtifactRevision> ListRevisions(
            string flowKey)
        {
            if (!EnsureSchemaForRead())
                return Array.Empty<FlowArtifactRevision>();
            string key =
                FlowArtifactStoreRules.NormalizeFlowKey(flowKey);
            return db.Queryable<FlowArtifactRevisionModel>()
                .Where(item => item.FlowKey == key)
                .OrderBy(item => item.Revision)
                .ToList()
                .Select(LoadRevision)
                .ToArray();
        }

        public FlowArtifactRevision Append(
            FlowArtifactRevisionWriteRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactRevision prepared =
                FlowArtifactStoreRules.Prepare(request);
            return ExecuteInOwnTransaction(
                () => AppendPreparedInCurrentTransaction(
                    prepared));
        }

        /// <summary>
        /// Appends through the transaction already active on this store's
        /// SqlSugar client. It is the integration seam used to atomically
        /// update legacy DataBase64 and the shared artifact authority.
        /// </summary>
        public FlowArtifactRevision AppendInCurrentTransaction(
            FlowArtifactRevisionWriteRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactRevision prepared =
                FlowArtifactStoreRules.Prepare(request);
            return AppendPreparedInCurrentTransaction(prepared);
        }

        public FlowArtifactRevision Publish(
            FlowArtifactRevisionTransitionRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            return ExecuteInOwnTransaction(
                () => PublishPreparedInCurrentTransaction(
                    prepared));
        }

        public FlowArtifactRevision PublishInCurrentTransaction(
            FlowArtifactRevisionTransitionRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            return PublishPreparedInCurrentTransaction(prepared);
        }

        public FlowArtifactRevision Abort(
            FlowArtifactRevisionTransitionRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            return ExecuteInOwnTransaction(
                () => AbortPreparedInCurrentTransaction(
                    prepared));
        }

        public FlowArtifactRevision AbortInCurrentTransaction(
            FlowArtifactRevisionTransitionRequest request)
        {
            EnsureSchemaForWrite();
            PreparedFlowArtifactTransition prepared =
                FlowArtifactStoreRules.Prepare(request);
            return AbortPreparedInCurrentTransaction(prepared);
        }

        private FlowArtifactRevision
            AppendPreparedInCurrentTransaction(
                PreparedFlowArtifactRevision prepared)
        {
            FlowArtifactReferenceModel reference =
                LockReference(prepared.FlowKey);
            FlowArtifactStoreRules.EnsureExpectedHead(
                prepared.FlowKey,
                prepared.ExpectedHead,
                ToDomain(reference));

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

            int revisionNumber = checked(
                reference.LastRevision + 1);
            FlowArtifactRevisionState state =
                prepared.PublishImmediately
                    ? FlowArtifactRevisionState.Published
                    : FlowArtifactRevisionState.Draft;
            FlowArtifactRevisionModel revisionModel = new()
            {
                FlowKey = prepared.FlowKey,
                Revision = revisionNumber,
                ParentRevision = reference.HeadRevision,
                RevisionHash = prepared.RevisionHash,
                State = state,
                Source = prepared.Source,
                Author = prepared.Author,
                Message = prepared.Message,
                ExternalVersion = prepared.ExternalVersion,
                CreatedTimeUtc = prepared.CreatedTimeUtc,
                StateChangedTimeUtc =
                    prepared.CreatedTimeUtc,
                StateChangedBy = prepared.PublishImmediately
                    ? prepared.Author
                    : null,
                StateChangeMessage =
                    prepared.PublishImmediately
                        ? prepared.Message
                        : null,
            };
            revisionModel.Id = db.Insertable(revisionModel)
                .ExecuteReturnBigIdentity();
            if (revisionModel.Id <= 0)
            {
                throw new InvalidOperationException(
                    "创建共享流程版本失败。");
            }

            FlowArtifactRevisionPartModel[] parts =
                prepared.Artifacts
                    .OrderBy(
                        item => item.Role,
                        StringComparer.Ordinal)
                    .Select(item =>
                        new FlowArtifactRevisionPartModel
                        {
                            RevisionId = revisionModel.Id,
                            Role = item.Role,
                            BlobHash = item.Hash,
                            ContentType = item.ContentType,
                        })
                    .ToArray();
            db.Insertable(parts).ExecuteCommand();

            FlowArtifactDependencyModel[] dependencies =
                prepared.Dependencies
                    .OrderBy(
                        item => item.DependencyKey,
                        StringComparer.Ordinal)
                    .Select(item =>
                        new FlowArtifactDependencyModel
                        {
                            RevisionId = revisionModel.Id,
                            DependencyKey =
                                item.DependencyKey,
                            DependencyKeyHash =
                                FlowArtifactStoreRules.ComputeBlobHash(
                                    System.Text.Encoding.UTF8.GetBytes(
                                        item.DependencyKey)),
                            DependencyFlowKey =
                                item.FlowKey,
                            DependencyRevision =
                                item.Revision,
                            DependencyContentHash =
                                item.ContentHash,
                            DependencyDefinitionHash =
                                item.DefinitionHash,
                        })
                    .ToArray();
            if (dependencies.Length > 0)
                db.Insertable(dependencies).ExecuteCommand();

            reference.LastRevision = revisionNumber;
            reference.HeadRevision = revisionNumber;
            reference.HeadRevisionHash =
                prepared.RevisionHash;
            if (prepared.PublishImmediately)
            {
                reference.PublishedRevision =
                    revisionNumber;
                reference.PublishedRevisionHash =
                    prepared.RevisionHash;
            }
            reference.UpdatedTimeUtc =
                prepared.CreatedTimeUtc;
            UpdateReference(reference);
            return BuildRevision(
                revisionModel,
                parts,
                dependencies,
                prepared.Artifacts
                    .GroupBy(
                        item => item.Hash,
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().Content.Length,
                    StringComparer.Ordinal));
        }

        private FlowArtifactRevision
            PublishPreparedInCurrentTransaction(
                PreparedFlowArtifactTransition prepared)
        {
            FlowArtifactReferenceModel reference =
                LockReference(prepared.FlowKey);
            EnsureTransitionTargetsHead(prepared, reference);
            FlowArtifactRevisionModel revision =
                GetRequiredRevisionModel(
                    prepared.FlowKey,
                    prepared.Revision);

            if (revision.State
                == FlowArtifactRevisionState.Published)
            {
                if (reference.PublishedRevision
                        != revision.Revision
                    || !string.Equals(
                        reference.PublishedRevisionHash,
                        revision.RevisionHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "流程版本已标记发布，但 published 引用不一致。");
                }
                return LoadRevision(revision);
            }
            if (revision.State
                != FlowArtifactRevisionState.Draft)
            {
                throw new InvalidOperationException(
                    $"只有 Draft 版本可以发布，当前状态为 "
                    + $"{revision.State}。");
            }

            revision.State =
                FlowArtifactRevisionState.Published;
            revision.StateChangedTimeUtc =
                prepared.ChangedTimeUtc;
            revision.StateChangedBy = prepared.Actor;
            revision.StateChangeMessage = prepared.Message;
            RequireSingleUpdate(
                db.Updateable(revision).ExecuteCommand(),
                "发布共享流程版本");

            reference.PublishedRevision = revision.Revision;
            reference.PublishedRevisionHash =
                revision.RevisionHash;
            reference.UpdatedTimeUtc =
                prepared.ChangedTimeUtc;
            UpdateReference(reference);
            return LoadRevision(revision);
        }

        private FlowArtifactRevision
            AbortPreparedInCurrentTransaction(
                PreparedFlowArtifactTransition prepared)
        {
            FlowArtifactReferenceModel reference =
                LockReference(prepared.FlowKey);
            EnsureTransitionTargetsHead(prepared, reference);
            FlowArtifactRevisionModel revision =
                GetRequiredRevisionModel(
                    prepared.FlowKey,
                    prepared.Revision);
            if (revision.State
                != FlowArtifactRevisionState.Draft)
            {
                throw new InvalidOperationException(
                    $"只有 Draft 版本可以中止，当前状态为 "
                    + $"{revision.State}。");
            }

            revision.State =
                FlowArtifactRevisionState.Aborted;
            revision.StateChangedTimeUtc =
                prepared.ChangedTimeUtc;
            revision.StateChangedBy = prepared.Actor;
            revision.StateChangeMessage = prepared.Message;
            RequireSingleUpdate(
                db.Updateable(revision).ExecuteCommand(),
                "中止共享流程版本");

            FlowArtifactRevisionModel? parent =
                revision.ParentRevision == null
                    ? null
                    : GetRequiredRevisionModel(
                        prepared.FlowKey,
                        revision.ParentRevision.Value);
            reference.HeadRevision = parent?.Revision;
            reference.HeadRevisionHash =
                parent?.RevisionHash;
            reference.UpdatedTimeUtc =
                prepared.ChangedTimeUtc;
            UpdateReference(reference);
            return LoadRevision(revision);
        }

        private FlowArtifactBlob PutArtifactCore(
            string hash,
            byte[] content,
            DateTime createdTimeUtc)
        {
            db.Ado.ExecuteCommand(
                $"""
                INSERT IGNORE INTO `{FlowArtifactTableNames.Blob}`
                    (`hash`, `content_length`, `content`, `created_time_utc`)
                VALUES
                    (@hash, @contentLength, @content, @createdTimeUtc)
                """,
                new SugarParameter("@hash", hash),
                new SugarParameter(
                    "@contentLength",
                    content.Length),
                new SugarParameter("@content", content),
                new SugarParameter(
                    "@createdTimeUtc",
                    FlowArtifactStoreRules.NormalizeUtc(
                        createdTimeUtc)));

            FlowArtifactBlobModel? stored =
                db.Queryable<FlowArtifactBlobModel>()
                    .Where(item => item.Hash == hash)
                    .First();
            if (stored == null)
            {
                throw new InvalidOperationException(
                    $"写入 artifact {hash} 后无法读取。");
            }
            if (stored.ContentLength != content.Length
                || stored.Content.Length != content.Length
                || !CryptographicOperations.FixedTimeEquals(
                    stored.Content,
                    content))
            {
                throw new InvalidOperationException(
                    $"检测到 artifact 哈希冲突：{hash}。");
            }
            return ToDomain(stored);
        }

        private FlowArtifactReferenceModel LockReference(
            string flowKey)
        {
            DateTime nowUtc = DateTime.UtcNow;
            db.Ado.ExecuteCommand(
                $"""
                INSERT IGNORE INTO `{FlowArtifactTableNames.Reference}`
                    (`flow_key`, `last_revision`, `head_revision`,
                     `head_revision_hash`, `published_revision`,
                     `published_revision_hash`, `updated_time_utc`)
                VALUES
                    (@flowKey, 0, NULL, NULL, NULL, NULL, @updatedTimeUtc)
                """,
                new SugarParameter("@flowKey", flowKey),
                new SugarParameter(
                    "@updatedTimeUtc",
                    nowUtc));

            FlowArtifactReferenceModel? reference =
                db.Ado.SqlQuery<FlowArtifactReferenceModel>(
                    $"""
                    SELECT
                        `flow_key` AS `FlowKey`,
                        `last_revision` AS `LastRevision`,
                        `head_revision` AS `HeadRevision`,
                        `head_revision_hash` AS `HeadRevisionHash`,
                        `published_revision` AS `PublishedRevision`,
                        `published_revision_hash` AS `PublishedRevisionHash`,
                        `updated_time_utc` AS `UpdatedTimeUtc`
                    FROM `{FlowArtifactTableNames.Reference}`
                    WHERE `flow_key` = @flowKey
                    FOR UPDATE
                    """,
                    new SugarParameter("@flowKey", flowKey))
                .SingleOrDefault();
            return reference
                ?? throw new InvalidOperationException(
                    $"无法锁定流程 {flowKey} 的共享版本引用。");
        }

        private static void EnsureTransitionTargetsHead(
            PreparedFlowArtifactTransition prepared,
            FlowArtifactReferenceModel reference)
        {
            FlowArtifactStoreRules.EnsureExpectedHead(
                prepared.FlowKey,
                prepared.ExpectedHead,
                ToDomain(reference));
            if (reference.HeadRevision != prepared.Revision)
            {
                throw new InvalidOperationException(
                    "只能发布或中止当前 head 版本。");
            }
        }

        private FlowArtifactRevisionModel GetRequiredRevisionModel(
            string flowKey,
            int revision)
        {
            return db.Queryable<FlowArtifactRevisionModel>()
                .Where(item =>
                    item.FlowKey == flowKey
                    && item.Revision == revision)
                .First()
                ?? throw new InvalidOperationException(
                    $"找不到流程 {flowKey} 的共享版本 {revision}。");
        }

        private FlowArtifactRevision LoadRevision(
            FlowArtifactRevisionModel revision)
        {
            FlowArtifactPartReadRow[] parts =
                db.Ado.SqlQuery<FlowArtifactPartReadRow>(
                    $"""
                    SELECT
                        p.`role` AS `Role`,
                        p.`blob_hash` AS `BlobHash`,
                        p.`content_type` AS `ContentType`,
                        b.`content_length` AS `ContentLength`
                    FROM `{FlowArtifactTableNames.RevisionPart}` p
                    INNER JOIN `{FlowArtifactTableNames.Blob}` b
                        ON b.`hash` = p.`blob_hash`
                    WHERE p.`revision_id` = @revisionId
                    ORDER BY p.`role`
                    """,
                    new SugarParameter(
                        "@revisionId",
                        revision.Id))
                .ToArray();
            FlowArtifactDependencyModel[] dependencies =
                db.Queryable<FlowArtifactDependencyModel>()
                    .Where(item =>
                        item.RevisionId == revision.Id)
                    .OrderBy(item => item.DependencyKey)
                    .ToArray();
            return BuildRevision(
                revision,
                parts.Select(item =>
                    new FlowArtifactRevisionPartModel
                    {
                        RevisionId = revision.Id,
                        Role = item.Role,
                        BlobHash = item.BlobHash,
                        ContentType = item.ContentType,
                    })
                    .ToArray(),
                dependencies,
                parts
                    .GroupBy(
                        item => item.BlobHash,
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().ContentLength,
                    StringComparer.Ordinal));
        }

        private static FlowArtifactRevision BuildRevision(
            FlowArtifactRevisionModel revision,
            IEnumerable<FlowArtifactRevisionPartModel> parts,
            IEnumerable<FlowArtifactDependencyModel> dependencies,
            IReadOnlyDictionary<string, int> contentLengths)
        {
            FlowArtifactDescriptor[] artifactDescriptors = parts
                .OrderBy(
                    item => item.Role,
                    StringComparer.Ordinal)
                .Select(item =>
                    new FlowArtifactDescriptor
                    {
                        Role = item.Role,
                        Hash = item.BlobHash,
                        ContentLength =
                            contentLengths[item.BlobHash],
                        ContentType = item.ContentType,
                    })
                .ToArray();
            FlowArtifactDependency[] dependencyDescriptors =
                dependencies
                    .OrderBy(
                        item => item.DependencyKey,
                        StringComparer.Ordinal)
                    .Select(item =>
                        new FlowArtifactDependency
                        {
                            DependencyKey =
                                item.DependencyKey,
                            FlowKey =
                                item.DependencyFlowKey,
                            Revision =
                                item.DependencyRevision,
                            ContentHash =
                                item.DependencyContentHash,
                            DefinitionHash =
                                item.DependencyDefinitionHash,
                        })
                    .ToArray();
            string computedRevisionHash =
                FlowArtifactStoreRules.ComputeRevisionHash(
                    artifactDescriptors,
                    dependencyDescriptors);
            if (!string.Equals(
                revision.RevisionHash,
                computedRevisionHash,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"共享流程 {revision.FlowKey} 版本 "
                    + $"{revision.Revision} 的内容哈希校验失败。");
            }
            return new FlowArtifactRevision
            {
                FlowKey = revision.FlowKey,
                Revision = revision.Revision,
                ParentRevision = revision.ParentRevision,
                RevisionHash = revision.RevisionHash,
                State = revision.State,
                Artifacts = artifactDescriptors,
                Dependencies = dependencyDescriptors,
                Source = revision.Source,
                Author = revision.Author,
                Message = revision.Message,
                ExternalVersion = revision.ExternalVersion,
                CreatedTimeUtc = NormalizeReadUtc(
                    revision.CreatedTimeUtc),
                StateChangedTimeUtc = NormalizeReadUtc(
                    revision.StateChangedTimeUtc),
                StateChangedBy = revision.StateChangedBy,
                StateChangeMessage =
                    revision.StateChangeMessage,
            };
        }

        private void UpdateReference(
            FlowArtifactReferenceModel reference)
        {
            RequireSingleUpdate(
                db.Updateable(reference).ExecuteCommand(),
                "更新共享流程版本引用");
        }

        private T ExecuteInOwnTransaction<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            db.Ado.BeginTran();
            try
            {
                T result = action();
                db.Ado.CommitTran();
                return result;
            }
            catch
            {
                try
                {
                    db.Ado.RollbackTran();
                }
                catch
                {
                    // Preserve the original database failure.
                }
                throw;
            }
        }

        private SchemaAvailability ProbeSchema()
        {
            int existingTables =
                FlowArtifactSchemaMigrator.TableNames.Count(
                    tableName =>
                        db.DbMaintenance.IsAnyTable(
                            tableName,
                            isCache: false));
            if (existingTables == 0)
                return SchemaAvailability.Absent;
            return existingTables
                    == FlowArtifactSchemaMigrator.TableNames.Count
                ? SchemaAvailability.Complete
                : SchemaAvailability.Partial;
        }

        private bool EnsureSchemaForRead()
        {
            return schemaAvailability.Value switch
            {
                SchemaAvailability.Complete => true,
                SchemaAvailability.Absent => false,
                _ => throw new InvalidOperationException(
                    "共享流程 Artifact 表结构不完整；"
                    + "请先执行显式初始化或迁移。"),
            };
        }

        private void EnsureSchemaForWrite()
        {
            if (schemaAvailability.Value
                != SchemaAvailability.Complete)
            {
                throw new InvalidOperationException(
                    "共享流程 Artifact 表尚未完整初始化；"
                    + "写入方必须显式启用 schema 初始化或迁移。");
            }
        }

        private static void RequireSingleUpdate(
            int affectedRows,
            string operation)
        {
            if (affectedRows != 1)
            {
                throw new InvalidOperationException(
                    $"{operation}应影响 1 行，实际为 "
                    + $"{affectedRows} 行。");
            }
        }

        private static FlowArtifactBlob ToDomain(
            FlowArtifactBlobModel model)
        {
            return new FlowArtifactBlob
            {
                Hash = model.Hash,
                Content = (byte[])model.Content.Clone(),
                CreatedTimeUtc =
                    NormalizeReadUtc(model.CreatedTimeUtc),
            };
        }

        private static FlowArtifactReference ToDomain(
            FlowArtifactReferenceModel model)
        {
            return new FlowArtifactReference
            {
                FlowKey = model.FlowKey,
                LastRevision = model.LastRevision,
                HeadRevision = model.HeadRevision,
                HeadRevisionHash = model.HeadRevisionHash,
                PublishedRevision =
                    model.PublishedRevision,
                PublishedRevisionHash =
                    model.PublishedRevisionHash,
                UpdatedTimeUtc =
                    NormalizeReadUtc(model.UpdatedTimeUtc),
            };
        }

        private static DateTime NormalizeReadUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc);
        }

        private sealed class FlowArtifactPartReadRow
        {
            public string Role { get; set; } = string.Empty;

            public string BlobHash { get; set; } =
                string.Empty;

            public string? ContentType { get; set; }

            public int ContentLength { get; set; }
        }

        private enum SchemaAvailability
        {
            Absent,
            Complete,
            Partial,
        }
    }
}
