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

        internal static string Build(SqlSugarClient db)
        {
            var masters = db.Ado.SqlQuery<SensorTemplateMigrationMasterRow>(
                $@"SELECT m.`id` AS ModMasterId,
                           m.`mm_id` AS SourceDictionaryId,
                           m.`name` AS TemplateName,
                           dm.`code` AS DictionaryCode,
                           dm.`name` AS DictionaryName,
                           dm.`p_type` AS DictionaryPType,
                           dm.`pid` AS DictionaryParentId,
                           dm.`mod_type` AS DictionaryModType,
                           dm.`cfg_json` AS DictionaryJson,
                           dm.`version` AS DictionaryVersion,
                           dm.`create_date` AS DictionaryCreateDate,
                           dm.`remark` AS DictionaryRemark,
                           dm.`tenant_id` AS DictionaryTenantId
                    FROM `{ModParamMasterTableName}` m
                    LEFT JOIN `{DictionaryMasterTableName}` dm ON dm.`id` = m.`mm_id`
                    WHERE dm.`mod_type` = 5
                       OR (m.`name` LIKE 'Sensor.%' AND (dm.`id` IS NULL OR dm.`mod_type` <> 5))");

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
                    WHERE dm.`mod_type` = 5
                       OR (m.`name` LIKE 'Sensor.%' AND (dm.`id` IS NULL OR dm.`mod_type` <> 5))");

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
                    WHERE dm.`mod_type` = 5");

            return Build(masters, details, items);
        }

        internal static string Build(
            IReadOnlyCollection<SensorTemplateMigrationMasterRow> masters,
            IReadOnlyCollection<SensorTemplateMigrationDetailRow> details,
            IReadOnlyCollection<SensorTemplateMigrationItemRow> items)
        {
            var resolvedMasters = masters
                .Select(master => new ResolvedSensorMaster(master, ResolveCode(master)))
                .Where(master => !string.IsNullOrWhiteSpace(master.Code))
                .ToList();
            if (resolvedMasters.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sql = new();
            sql.AppendLine("-- Generic sensor dictionaries are restored by business key and references are remapped");

            foreach (var group in resolvedMasters.GroupBy(master => master.Code!, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                AppendSensorGroup(sql, group.Key, group.ToList(), details, items);
            }

            return sql.ToString();
        }

        private static string? ResolveCode(SensorTemplateMigrationMasterRow master)
        {
            if (master.DictionaryModType == 5 && !string.IsNullOrWhiteSpace(master.DictionaryCode))
            {
                return master.DictionaryCode.Trim();
            }

            return SensorTemplateDictionaryService.InferLegacySensorCode(master.TemplateName);
        }

        private static void AppendSensorGroup(
            StringBuilder sql,
            string code,
            IReadOnlyCollection<ResolvedSensorMaster> masters,
            IReadOnlyCollection<SensorTemplateMigrationDetailRow> details,
            IReadOnlyCollection<SensorTemplateMigrationItemRow> items)
        {
            SensorTemplateMigrationMasterRow sourceDictionary = masters
                .Select(master => master.Source)
                .FirstOrDefault(master => master.DictionaryModType == 5 && string.Equals(master.DictionaryCode, code, StringComparison.OrdinalIgnoreCase))
                ?? masters.First().Source;
            string dictionaryName = string.IsNullOrWhiteSpace(sourceDictionary.DictionaryName) || sourceDictionary.DictionaryModType != 5
                ? code
                : sourceDictionary.DictionaryName;
            DateTime createDate = sourceDictionary.DictionaryModType == 5 && sourceDictionary.DictionaryCreateDate.HasValue
                ? sourceDictionary.DictionaryCreateDate.Value
                : DateTime.Now;
            int tenantId = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryTenantId : 0;
            int pType = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryPType : 0;
            int? parentId = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryParentId : null;
            string? dictionaryJson = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryJson : null;
            string? dictionaryVersion = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryVersion : null;
            string? dictionaryRemark = sourceDictionary.DictionaryModType == 5 ? sourceDictionary.DictionaryRemark : null;
            int[] modMasterIds = masters.Select(master => master.Source.ModMasterId).Distinct().Order().ToArray();
            var groupDetails = details.Where(detail => modMasterIds.Contains(detail.ModMasterId)).ToList();
            int[] healthySourceDictionaryIds = masters
                .Select(master => master.Source)
                .Where(master => master.DictionaryModType == 5 && string.Equals(master.DictionaryCode, code, StringComparison.OrdinalIgnoreCase))
                .Select(master => master.SourceDictionaryId)
                .Distinct()
                .ToArray();
            var groupItems = items
                .Where(item => healthySourceDictionaryIds.Contains(item.SourceDictionaryId))
                .GroupBy(item => item.SourceItemId)
                .Select(group => group.First())
                .OrderBy(item => item.SourceItemId)
                .ToList();

            sql.AppendLine($"-- Generic sensor: {code}");
            sql.AppendLine($@"INSERT INTO `{DictionaryMasterTableName}`
(`code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`)
SELECT {FormatSqlValue(code)}, {FormatSqlValue(dictionaryName)}, {pType}, {FormatSqlValue(parentId)}, 5,
       {FormatSqlValue(dictionaryJson)}, {FormatSqlValue(dictionaryVersion)}, {FormatSqlValue(createDate)}, 1, 0, {FormatSqlValue(dictionaryRemark)}, {tenantId}
WHERE NOT EXISTS (
    SELECT 1 FROM `{DictionaryMasterTableName}` WHERE `code` = {FormatSqlValue(code)}
);");
            sql.AppendLine($@"SET @cv_sensor_dictionary_id = (
    SELECT `id`
    FROM `{DictionaryMasterTableName}`
    WHERE `code` = {FormatSqlValue(code)}
    ORDER BY CASE WHEN `mod_type` = 5 THEN 0 ELSE 1 END, `is_delete`, `id`
    LIMIT 1
);");
            sql.AppendLine($@"UPDATE `{DictionaryMasterTableName}`
SET `name` = {FormatSqlValue(dictionaryName)}, `p_type` = {pType}, `pid` = {FormatSqlValue(parentId)}, `mod_type` = 5,
    `cfg_json` = {FormatSqlValue(dictionaryJson)}, `version` = {FormatSqlValue(dictionaryVersion)},
    `is_enable` = 1, `is_delete` = 0, `remark` = {FormatSqlValue(dictionaryRemark)}, `tenant_id` = {tenantId}
WHERE `id` = @cv_sensor_dictionary_id;");
            sql.AppendLine($@"UPDATE `{ModParamMasterTableName}`
SET `mm_id` = @cv_sensor_dictionary_id
WHERE `id` IN ({string.Join(", ", modMasterIds)})
  AND @cv_sensor_dictionary_id IS NOT NULL;");

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
            int[] missingItemIds = groupDetails
                .Where(detail => detail.SourceItemId > 0 && !knownItemIds.Contains(detail.SourceItemId))
                .Select(detail => detail.SourceItemId)
                .Distinct()
                .Order()
                .ToArray();
            for (int index = 0; index < missingItemIds.Length; index++)
            {
                int missingItemId = missingItemIds[index];
                string symbol = index == 0
                    ? SensorTemplateDictionaryService.DefaultCommandSymbol
                    : $"{SensorTemplateDictionaryService.DefaultCommandSymbol}_{missingItemId}";
                int[] detailIds = groupDetails
                    .Where(detail => detail.SourceItemId == missingItemId)
                    .Select(detail => detail.DetailId)
                    .Distinct()
                    .Order()
                    .ToArray();
                AppendItem(
                    sql,
                    new SensorTemplateMigrationItemRow
                    {
                        SourceItemId = missingItemId,
                        Symbol = symbol,
                        Name = symbol,
                        DefaultValue = SensorTemplateDictionaryService.DefaultCommandValue,
                        ValueType = 3,
                        CreateDate = DateTime.Now,
                        IsEnable = true,
                        IsDelete = false
                    },
                    detailIds);
            }
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

        private sealed record ResolvedSensorMaster(SensorTemplateMigrationMasterRow Source, string? Code);
    }

    internal sealed class SensorTemplateMigrationMasterRow
    {
        public int ModMasterId { get; set; }
        public int SourceDictionaryId { get; set; }
        public string? TemplateName { get; set; }
        public string? DictionaryCode { get; set; }
        public string? DictionaryName { get; set; }
        public int DictionaryPType { get; set; }
        public int? DictionaryParentId { get; set; }
        public int? DictionaryModType { get; set; }
        public string? DictionaryJson { get; set; }
        public string? DictionaryVersion { get; set; }
        public DateTime? DictionaryCreateDate { get; set; }
        public string? DictionaryRemark { get; set; }
        public int DictionaryTenantId { get; set; }
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
