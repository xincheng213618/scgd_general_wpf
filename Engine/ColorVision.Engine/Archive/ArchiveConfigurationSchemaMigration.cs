using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Archive.Dao
{
    internal enum ArchiveSchemaMigrationStage
    {
        InspectColumns
    }

    internal sealed class ArchiveSchemaMigrationException : InvalidOperationException
    {
        internal ArchiveSchemaMigrationException(Exception exception)
            : base($"Archive schema migration failed. Stage={ArchiveSchemaMigrationStage.InspectColumns}; FailureType={exception.GetType().Name}; ErrorCode={exception.HResult}.")
        {
            Stage = ArchiveSchemaMigrationStage.InspectColumns;
            FailureType = exception.GetType().Name;
            ErrorCode = exception.HResult;
        }

        public ArchiveSchemaMigrationStage Stage { get; }

        public string FailureType { get; }

        public int ErrorCode { get; }

        public string GetDiagnosticSummary()
        {
            return $"Stage={Stage}; FailureType={FailureType}; ErrorCode={ErrorCode}";
        }
    }

    internal static class ArchiveConfigurationSchemaMigration
    {
        internal const string TableName = "t_scgd_sys_config_archived";
        internal const string ExcludingImagesColumn = "excluding_images";
        internal const string DeleteLocalFileColumn = "del_local_file";
        internal const string DataSaveHoursColumn = "data_save_hours";

        private static readonly ColumnDefinition[] RequiredColumns =
        [
            new(ExcludingImagesColumn, "ADD COLUMN `excluding_images` TINYINT(1) NOT NULL DEFAULT '0' AFTER `data_save_days`"),
            new(DeleteLocalFileColumn, "ADD COLUMN `del_local_file` TINYINT(1) NOT NULL DEFAULT '0'"),
            new(DataSaveHoursColumn, "ADD COLUMN `data_save_hours` INT(11) NOT NULL DEFAULT '0'")
        ];

        internal static int EnsureColumnsAndExecute(Action afterReady)
        {
            return EnsureColumnsAndExecute(InspectExistingColumns, MySqlControl.BatchExecuteNonQuery, afterReady);
        }

        internal static int EnsureColumnsAndExecute(
            Func<IReadOnlySet<string>> inspectExistingColumns,
            Func<string, int> executeAlter,
            Action afterReady)
        {
            ArgumentNullException.ThrowIfNull(inspectExistingColumns);
            ArgumentNullException.ThrowIfNull(executeAlter);
            ArgumentNullException.ThrowIfNull(afterReady);

            IReadOnlySet<string> existingColumns = InspectColumns(inspectExistingColumns);
            ColumnDefinition[] missingColumns = RequiredColumns
                .Where(item => !existingColumns.Contains(item.Name))
                .ToArray();

            if (missingColumns.Length == 0)
            {
                afterReady();
                return 0;
            }

            string alterSql = BuildAlterStatement(missingColumns);
            int affectedRows;
            try
            {
                affectedRows = executeAlter(alterSql);
            }
            catch (BatchExecuteNonQueryException)
            {
                // Another process may have added the missing columns after the precheck.
                // Treat the operation as successful only when a fresh inspection proves the target schema is complete.
                if (!TryInspectAllColumns(inspectExistingColumns))
                    throw;

                affectedRows = 0;
            }

            afterReady();
            return affectedRows;
        }

        private static IReadOnlySet<string> InspectColumns(Func<IReadOnlySet<string>> inspectExistingColumns)
        {
            try
            {
                IReadOnlySet<string> columns = inspectExistingColumns()
                    ?? throw new InvalidOperationException("The archive schema inspector returned null.");
                return new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                throw new ArchiveSchemaMigrationException(ex);
            }
        }

        private static bool TryInspectAllColumns(Func<IReadOnlySet<string>> inspectExistingColumns)
        {
            try
            {
                IReadOnlySet<string>? columns = inspectExistingColumns();
                if (columns == null)
                    return false;

                var normalizedColumns = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
                return RequiredColumns.All(item => normalizedColumns.Contains(item.Name));
            }
            catch
            {
                return false;
            }
        }

        private static string BuildAlterStatement(IEnumerable<ColumnDefinition> missingColumns)
        {
            // Both the table identifier and every clause come only from this fixed whitelist.
            return $"ALTER TABLE `{TableName}` {string.Join(", ", missingColumns.Select(item => item.AddClause))};";
        }

        private static IReadOnlySet<string> InspectExistingColumns()
        {
            using SqlSugarClient db = MySqlControl.CreateDbClient();
            List<string> columns = db.Ado.SqlQuery<string>(
                @"SELECT COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @tableName
                    AND COLUMN_NAME IN (@excludingImages, @deleteLocalFile, @dataSaveHours)",
                new
                {
                    tableName = TableName,
                    excludingImages = ExcludingImagesColumn,
                    deleteLocalFile = DeleteLocalFileColumn,
                    dataSaveHours = DataSaveHoursColumn
                });

            return new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        }

        private readonly record struct ColumnDefinition(string Name, string AddClause);
    }
}
