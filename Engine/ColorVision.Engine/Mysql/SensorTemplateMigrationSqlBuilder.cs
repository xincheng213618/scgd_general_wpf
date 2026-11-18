using ColorVision.Engine.Services.Devices.Sensor.Templates;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Database
{
    internal static class SensorTemplateMigrationSqlBuilder
    {
        private const string DictionaryMasterTableName = "t_scgd_sys_dictionary_mod_master";
        private const string DictionaryItemTableName = "t_scgd_sys_dictionary_mod_item";
        private const string ModParamMasterTableName = "t_scgd_mod_param_master";
        private const string ModParamDetailTableName = "t_scgd_mod_param_detail";
        private const int DefaultDictionaryId = SensorTemplateDictionaryService.DefaultDictionaryId;
        private const string DefaultCategoryCode = SensorTemplateDictionaryService.DefaultCategoryCode;
        private static readonly string SensorTemplateFilter = $@"dm.`mod_type` = 5
                       OR (dm.`id` IS NULL AND m.`mm_id` = {DefaultDictionaryId})
                       OR (m.`name` LIKE 'Sensor.%' AND (dm.`id` IS NULL OR dm.`mod_type` <> 5))";

        internal static string Build(SqlSugarClient db)
        {
            var masters = db.Ado.SqlQuery<SensorTemplateMigrationMasterRow>(
                $@"SELECT m.`id` AS ModMasterId,
                           m.`mm_id` AS SourceDictionaryId,
                           dm.`code` AS DictionaryCode,
                           dm.`mod_type` AS DictionaryModType
                    FROM `{ModParamMasterTableName}` m
                    LEFT JOIN `{DictionaryMasterTableName}` dm ON dm.`id` = m.`mm_id`
                    WHERE {SensorTemplateFilter}");

            if (masters.Count == 0)
            {
                return string.Empty;
            }

            var details = db.Ado.SqlQuery<SensorTemplateMigrationDetailRow>(
                $@"SELECT d.`id` AS DetailId,
                           d.`pid` AS ModMasterId,
                           d.`cc_pid` AS SourceItemId
                    FROM `{ModParamDetailTableName}` d
                    INNER JOIN `{ModParamMasterTableName}` m ON m.`id` = d.`pid`
                    LEFT JOIN `{DictionaryMasterTableName}` dm ON dm.`id` = m.`mm_id`
                    WHERE {SensorTemplateFilter}");

            var items = db.Ado.SqlQuery<SensorTemplateMigrationItemRow>(
                $@"SELECT i.`id` AS SourceItemId,
                           i.`pid` AS SourceDictionaryId,
                           i.`symbol` AS Symbol,
                           i.`name` AS Name,
                           i.`default_val` AS DefaultValue,
                           i.`val_type` AS ValueType,
                           i.`value_range` AS ValueRange,
                           i.`create_date` AS CreateDate,
                           i.`is_enable` AS IsEnable,
                           i.`is_delete` AS IsDelete,
                           i.`remark` AS Remark
                    FROM `{DictionaryItemTableName}` i
                    INNER JOIN `{DictionaryMasterTableName}` dm ON dm.`id` = i.`pid`
                    WHERE dm.`mod_type` = 5 AND dm.`id` = {DefaultDictionaryId} AND dm.`code` = '{DefaultCategoryCode}'");

            return Build(masters, details, items);
        }

        internal static string Build(
            IReadOnlyCollection<SensorTemplateMigrationMasterRow> masters,
            IReadOnlyCollection<SensorTemplateMigrationDetailRow> details,
            IReadOnlyCollection<SensorTemplateMigrationItemRow> items)
        {
            if (masters.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sql = new();
            int[] defaultTemplateIds = masters.Where(IsDefaultTemplate).Select(master => master.ModMasterId).Distinct().Order().ToArray();
            int[] otherTemplateIds = masters.Where(master => !IsDefaultTemplate(master)).Select(master => master.ModMasterId).Distinct().Order().ToArray();
            if (defaultTemplateIds.Length > 0) AppendDefaultSensorTemplates(sql, defaultTemplateIds, details, items);
            if (otherTemplateIds.Length > 0) DetachOtherSensorTemplates(sql, otherTemplateIds);

            return sql.ToString();
        }

        private static bool IsDefaultTemplate(SensorTemplateMigrationMasterRow master) => master.SourceDictionaryId == DefaultDictionaryId
            && (master.DictionaryModType == null || (master.DictionaryModType == 5 && string.Equals(master.DictionaryCode, DefaultCategoryCode, StringComparison.OrdinalIgnoreCase)));

        private static void AppendDefaultSensorTemplates(
            StringBuilder sql,
            int[] modMasterIds,
            IReadOnlyCollection<SensorTemplateMigrationDetailRow> details,
            IReadOnlyCollection<SensorTemplateMigrationItemRow> items)
        {
            var groupDetails = details.Where(detail => modMasterIds.Contains(detail.ModMasterId)).ToList();
            var groupItems = items
                .Where(item => item.SourceDictionaryId == DefaultDictionaryId)
                .GroupBy(item => item.SourceItemId)
                .Select(group => group.First())
                .OrderBy(item => item.SourceItemId)
                .ToList();

            sql.AppendLine($"-- Generic sensor: {DefaultCategoryCode}");
            sql.AppendLine($@"SET @cv_sensor_dictionary_id = (
    SELECT `id`
    FROM `{DictionaryMasterTableName}`
    WHERE `id` = {DefaultDictionaryId} AND `code` = '{DefaultCategoryCode}' AND `mod_type` = 5 AND `is_enable` = 1 AND `is_delete` = 0
);");
            string ids = string.Join(", ", modMasterIds);
            sql.AppendLine($@"UPDATE `{ModParamMasterTableName}`
SET `mm_id` = @cv_sensor_dictionary_id
WHERE `id` IN ({ids});");
            sql.AppendLine($@"UPDATE `{ModParamDetailTableName}`
SET `cc_pid` = NULL
WHERE `pid` IN ({ids}) AND @cv_sensor_dictionary_id IS NULL;");

            foreach (var item in groupItems)
            {
                int[] detailIds = groupDetails
                    .Where(detail => detail.SourceItemId == item.SourceItemId)
                    .Select(detail => detail.DetailId)
                    .Distinct()
                    .Order()
                    .ToArray();
                AppendItem(sql, item, detailIds);
            }

            var knownItemIds = groupItems.Select(item => item.SourceItemId).ToHashSet();
            int[] missingDetailIds = groupDetails
                .Where(detail => detail.SourceItemId > 0 && !knownItemIds.Contains(detail.SourceItemId))
                .Select(detail => detail.DetailId)
                .Distinct()
                .Order()
                .ToArray();
            if (missingDetailIds.Length > 0)
            {
                sql.AppendLine($@"UPDATE `{ModParamDetailTableName}`
SET `cc_pid` = (
    SELECT `id` FROM `{DictionaryItemTableName}`
    WHERE `pid` = @cv_sensor_dictionary_id AND `symbol` = '{SensorTemplateDictionaryService.DefaultCommandSymbol}'
      AND `is_enable` = 1 AND `is_delete` = 0
    ORDER BY `id` LIMIT 1
)
WHERE `id` IN ({string.Join(", ", missingDetailIds)});");
            }
        }

        private static void DetachOtherSensorTemplates(StringBuilder sql, int[] modMasterIds)
        {
            // Old numeric references can be occupied by unrelated dictionary entries after the reset.
            string ids = string.Join(", ", modMasterIds);
            sql.AppendLine($@"-- Preserve legacy sensor templates and commands without reusing their dictionary IDs
UPDATE `{ModParamMasterTableName}` SET `mm_id` = NULL WHERE `id` IN ({ids});
UPDATE `{ModParamDetailTableName}` SET `cc_pid` = NULL WHERE `pid` IN ({ids});");
        }

        private static void AppendItem(StringBuilder sql, SensorTemplateMigrationItemRow item, int[] detailIds)
        {
            string symbol = !string.IsNullOrWhiteSpace(item.Symbol)
                ? item.Symbol.Trim()
                : !string.IsNullOrWhiteSpace(item.Name)
                    ? item.Name.Trim()
                    : $"legacycommand_{item.SourceItemId}";
            string name = string.IsNullOrWhiteSpace(item.Name) ? symbol : item.Name;
            DateTime createDate = item.CreateDate ?? DateTime.Now;

            sql.AppendLine($@"SET @cv_sensor_item_id = (
    SELECT `id`
    FROM `{DictionaryItemTableName}`
    WHERE `pid` = @cv_sensor_dictionary_id AND `symbol` = {FormatSqlValue(symbol)}
    ORDER BY `is_delete`, `id`
    LIMIT 1
);");
            sql.AppendLine($"SET @cv_sensor_item_new_id = (SELECT COALESCE(MAX(`id`), 0) + 1 FROM `{DictionaryItemTableName}`);");
            sql.AppendLine($@"INSERT INTO `{DictionaryItemTableName}`
(`id`, `pid`, `address_code`, `symbol`, `name`, `default_val`, `val_type`, `value_range`, `create_date`, `is_enable`, `is_delete`, `remark`)
SELECT @cv_sensor_item_new_id, @cv_sensor_dictionary_id, @cv_sensor_item_new_id,
       {FormatSqlValue(symbol)}, {FormatSqlValue(name)}, {FormatSqlValue(item.DefaultValue)}, {item.ValueType}, {FormatSqlValue(item.ValueRange)},
       {FormatSqlValue(createDate)}, {FormatBoolean(item.IsEnable)}, {FormatBoolean(item.IsDelete)}, {FormatSqlValue(item.Remark)}
WHERE @cv_sensor_dictionary_id IS NOT NULL
  AND @cv_sensor_item_id IS NULL;");
            sql.AppendLine($@"SET @cv_sensor_item_id = (
    SELECT `id`
    FROM `{DictionaryItemTableName}`
    WHERE `pid` = @cv_sensor_dictionary_id AND `symbol` = {FormatSqlValue(symbol)}
    ORDER BY `is_delete`, `id`
    LIMIT 1
);");
            sql.AppendLine($@"UPDATE `{DictionaryItemTableName}`
SET `address_code` = `id`, `name` = {FormatSqlValue(name)}, `default_val` = {FormatSqlValue(item.DefaultValue)},
    `val_type` = {item.ValueType}, `value_range` = {FormatSqlValue(item.ValueRange)}, `is_enable` = {FormatBoolean(item.IsEnable)},
    `is_delete` = {FormatBoolean(item.IsDelete)}, `remark` = {FormatSqlValue(item.Remark)}
WHERE `id` = @cv_sensor_item_id;");

            if (detailIds.Length > 0)
            {
                sql.AppendLine($@"UPDATE `{ModParamDetailTableName}`
SET `cc_pid` = @cv_sensor_item_id
WHERE `id` IN ({string.Join(", ", detailIds)})
  AND `cc_pid` = {item.SourceItemId}
  AND @cv_sensor_item_id IS NOT NULL;");
            }
        }

        private static int FormatBoolean(bool value) => value ? 1 : 0;

        private static string FormatSqlValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "NULL";
            }

            return value switch
            {
                bool boolValue => boolValue ? "1" : "0",
                DateTime dateTime => $"'{dateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
                DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.LocalDateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL",
                _ => $"'{EscapeSqlValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}'"
            };
        }

        private static string EscapeSqlValue(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\0", "\\0")
                .Replace("\b", "\\b")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\u001A", "\\Z");
        }
    }

    internal sealed class SensorTemplateMigrationMasterRow
    {
        public int ModMasterId { get; set; }
        public int SourceDictionaryId { get; set; }
        public string? DictionaryCode { get; set; }
        public int? DictionaryModType { get; set; }
    }

    internal sealed class SensorTemplateMigrationDetailRow
    {
        public int DetailId { get; set; }
        public int ModMasterId { get; set; }
        public int SourceItemId { get; set; }
    }

    internal sealed class SensorTemplateMigrationItemRow
    {
        public int SourceItemId { get; set; }
        public int SourceDictionaryId { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string? DefaultValue { get; set; }
        public int ValueType { get; set; }
        public string? ValueRange { get; set; }
        public DateTime? CreateDate { get; set; }
        public bool IsEnable { get; set; }
        public bool IsDelete { get; set; }
        public string? Remark { get; set; }
    }
}
