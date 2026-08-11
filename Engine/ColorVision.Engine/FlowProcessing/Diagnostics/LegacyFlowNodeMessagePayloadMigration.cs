using ColorVision.Database;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    /// <summary>
    /// Transitional field migration. Runtime reads only the compressed BLOB
    /// columns; this class is the sole reader of the retired TEXT columns.
    /// </summary>
    internal static class LegacyFlowNodeMessagePayloadMigration
    {
        private static readonly SqliteGzipTextMigrationSpec[] Specifications =
        [
            new(
                FlowNodeMessagePayloadStorage.TableName,
                FlowNodeMessagePayloadStorage.IdColumnName,
                FlowNodeMessagePayloadStorage.SendLegacyColumnName,
                FlowNodeMessagePayloadStorage.SendGzipColumnName,
                FlowNodeMessagePayloadStorage.SendLengthColumnName),
            new(
                FlowNodeMessagePayloadStorage.TableName,
                FlowNodeMessagePayloadStorage.IdColumnName,
                FlowNodeMessagePayloadStorage.RecvLegacyColumnName,
                FlowNodeMessagePayloadStorage.RecvGzipColumnName,
                FlowNodeMessagePayloadStorage.RecvLengthColumnName),
        ];

        internal static SqliteGzipTextMigrationReport Execute(string databasePath)
        {
            return SqliteGzipTextMigration.Execute(
                databasePath,
                Specifications,
                batchSize: 500);
        }
    }
}
