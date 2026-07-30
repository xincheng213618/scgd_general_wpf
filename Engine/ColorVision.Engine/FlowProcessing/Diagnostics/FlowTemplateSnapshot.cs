using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowTemplateSnapshot")]
    [SugarIndex(
        "idx_flow_template_snapshot_template_hash",
        nameof(TemplateId),
        OrderByType.Asc,
        nameof(ContentHash),
        OrderByType.Asc)]
    [SugarIndex(
        "ux_flow_template_snapshot_flow_key_hash",
        nameof(FlowKey),
        OrderByType.Asc,
        nameof(ContentHash),
        OrderByType.Asc,
        true)]
    public sealed class FlowTemplateSnapshot
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "template_id")]
        public int TemplateId { get; set; }

        [SugarColumn(ColumnName = "flow_key", IsNullable = true)]
        public string? FlowKey { get; set; }

        [SugarColumn(ColumnName = "template_revision", IsNullable = true)]
        public int? TemplateRevision { get; set; }

        [SugarColumn(ColumnName = "content_hash", Length = 64)]
        public string ContentHash { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "content")]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        [SugarColumn(ColumnName = "content_length")]
        public int ContentLength { get; set; }

        [SugarColumn(ColumnName = "captured_time_utc")]
        public DateTime CapturedTimeUtc { get; set; }
    }
}
