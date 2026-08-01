using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    internal static class FlowArtifactTableNames
    {
        public const string Blob =
            "t_scgd_flow_artifact_blob";
        public const string Revision =
            "t_scgd_flow_artifact_revision";
        public const string RevisionPart =
            "t_scgd_flow_artifact_revision_part";
        public const string Dependency =
            "t_scgd_flow_artifact_dependency";
        public const string Reference =
            "t_scgd_flow_artifact_ref";
    }

    [SugarTable(FlowArtifactTableNames.Blob)]
    internal sealed class FlowArtifactBlobModel : IInitTables
    {
        [SugarColumn(
            ColumnName = "hash",
            Length = 64,
            IsPrimaryKey = true)]
        public string Hash { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "content_length")]
        public int ContentLength { get; set; }

        [SugarColumn(
            ColumnName = "content",
            ColumnDataType = "LONGBLOB")]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        [SugarColumn(ColumnName = "created_time_utc")]
        public DateTime CreatedTimeUtc { get; set; }
    }

    [SugarTable(FlowArtifactTableNames.Revision)]
    [SugarIndex(
        "ux_flow_artifact_revision_flow_revision",
        nameof(FlowKey),
        OrderByType.Asc,
        nameof(Revision),
        OrderByType.Asc,
        true)]
    [SugarIndex(
        "idx_flow_artifact_revision_flow_state",
        nameof(FlowKey),
        OrderByType.Asc,
        nameof(State),
        OrderByType.Asc)]
    internal sealed class FlowArtifactRevisionModel : IInitTables
    {
        [SugarColumn(
            ColumnName = "id",
            ColumnDataType = "BIGINT",
            IsPrimaryKey = true,
            IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "flow_key", Length = 256)]
        public string FlowKey { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "revision")]
        public int Revision { get; set; }

        [SugarColumn(
            ColumnName = "parent_revision",
            IsNullable = true)]
        public int? ParentRevision { get; set; }

        [SugarColumn(
            ColumnName = "revision_hash",
            Length = 64)]
        public string RevisionHash { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "state")]
        public FlowArtifactRevisionState State { get; set; }

        [SugarColumn(
            ColumnName = "source",
            Length = 64,
            IsNullable = true)]
        public string? Source { get; set; }

        [SugarColumn(
            ColumnName = "author",
            Length = 256,
            IsNullable = true)]
        public string? Author { get; set; }

        [SugarColumn(
            ColumnName = "message",
            Length = 2_000,
            IsNullable = true)]
        public string? Message { get; set; }

        [SugarColumn(
            ColumnName = "external_version",
            Length = 256,
            IsNullable = true)]
        public string? ExternalVersion { get; set; }

        [SugarColumn(ColumnName = "created_time_utc")]
        public DateTime CreatedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "state_changed_time_utc")]
        public DateTime StateChangedTimeUtc { get; set; }

        [SugarColumn(
            ColumnName = "state_changed_by",
            Length = 256,
            IsNullable = true)]
        public string? StateChangedBy { get; set; }

        [SugarColumn(
            ColumnName = "state_change_message",
            Length = 2_000,
            IsNullable = true)]
        public string? StateChangeMessage { get; set; }
    }

    [SugarTable(FlowArtifactTableNames.RevisionPart)]
    [SugarIndex(
        "ux_flow_artifact_revision_part_role",
        nameof(RevisionId),
        OrderByType.Asc,
        nameof(Role),
        OrderByType.Asc,
        true)]
    [SugarIndex(
        "idx_flow_artifact_revision_part_hash",
        nameof(BlobHash),
        OrderByType.Asc)]
    internal sealed class FlowArtifactRevisionPartModel : IInitTables
    {
        [SugarColumn(
            ColumnName = "id",
            ColumnDataType = "BIGINT",
            IsPrimaryKey = true,
            IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(
            ColumnName = "revision_id",
            ColumnDataType = "BIGINT")]
        public long RevisionId { get; set; }

        [SugarColumn(ColumnName = "role", Length = 64)]
        public string Role { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "blob_hash", Length = 64)]
        public string BlobHash { get; set; } = string.Empty;

        [SugarColumn(
            ColumnName = "content_type",
            Length = 128,
            IsNullable = true)]
        public string? ContentType { get; set; }
    }

    [SugarTable(FlowArtifactTableNames.Dependency)]
    [SugarIndex(
        "ux_flow_artifact_dependency_slot",
        nameof(RevisionId),
        OrderByType.Asc,
        nameof(DependencyKeyHash),
        OrderByType.Asc,
        true)]
    [SugarIndex(
        "idx_flow_artifact_dependency_target",
        nameof(DependencyFlowKey),
        OrderByType.Asc,
        nameof(DependencyRevision),
        OrderByType.Asc)]
    internal sealed class FlowArtifactDependencyModel : IInitTables
    {
        [SugarColumn(
            ColumnName = "id",
            ColumnDataType = "BIGINT",
            IsPrimaryKey = true,
            IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(
            ColumnName = "revision_id",
            ColumnDataType = "BIGINT")]
        public long RevisionId { get; set; }

        [SugarColumn(
            ColumnName = "dependency_key",
            Length = 2_000)]
        public string DependencyKey { get; set; } = string.Empty;

        [SugarColumn(
            ColumnName = "dependency_key_hash",
            Length = 64)]
        public string DependencyKeyHash { get; set; } = string.Empty;

        [SugarColumn(
            ColumnName = "dependency_flow_key",
            Length = 256)]
        public string DependencyFlowKey { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "dependency_revision")]
        public int DependencyRevision { get; set; }

        [SugarColumn(
            ColumnName = "dependency_content_hash",
            Length = 64)]
        public string DependencyContentHash { get; set; } =
            string.Empty;

        [SugarColumn(
            ColumnName = "dependency_definition_hash",
            Length = 64)]
        public string DependencyDefinitionHash { get; set; } =
            string.Empty;
    }

    [SugarTable(FlowArtifactTableNames.Reference)]
    internal sealed class FlowArtifactReferenceModel : IInitTables
    {
        [SugarColumn(
            ColumnName = "flow_key",
            Length = 256,
            IsPrimaryKey = true)]
        public string FlowKey { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "last_revision")]
        public int LastRevision { get; set; }

        [SugarColumn(
            ColumnName = "head_revision",
            IsNullable = true)]
        public int? HeadRevision { get; set; }

        [SugarColumn(
            ColumnName = "head_revision_hash",
            Length = 64,
            IsNullable = true)]
        public string? HeadRevisionHash { get; set; }

        [SugarColumn(
            ColumnName = "published_revision",
            IsNullable = true)]
        public int? PublishedRevision { get; set; }

        [SugarColumn(
            ColumnName = "published_revision_hash",
            Length = 64,
            IsNullable = true)]
        public string? PublishedRevisionHash { get; set; }

        [SugarColumn(ColumnName = "updated_time_utc")]
        public DateTime UpdatedTimeUtc { get; set; }
    }
}
