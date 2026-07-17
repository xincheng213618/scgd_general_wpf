using ColorVision.Database;
using ColorVision.Engine.Templates;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    internal static partial class SensorTemplateDictionaryService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SensorTemplateDictionaryService));

        internal const string DefaultCommandSymbol = "defaultcommand";
        internal const string DefaultCommandValue = "\n,,Ascii,1000/0,0";

        internal static List<SysDictionaryModDetaiModel> GetOrCreateCommandDefinitions(int templateDictionaryId)
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            return GetOrCreateCommandDefinitions(db, templateDictionaryId);
        }

        internal static List<SysDictionaryModDetaiModel> GetOrCreateCommandDefinitions(SqlSugarClient db, int templateDictionaryId)
        {
            RepairMissingCommandDefinitions(db, templateDictionaryId);

            var definitions = db.Queryable<SysDictionaryModDetaiModel>().Where(x => x.PId == templateDictionaryId).ToList();
            if (definitions.Count > 0)
            {
                return definitions;
            }

            int id = db.Queryable<SysDictionaryModDetaiModel>().Max(x => (int?)x.Id) ?? 0;
            var defaultCommand = CreateCommandDefinition(id + 1, templateDictionaryId);
            InsertCommandDefinition(db, defaultCommand);
            SymbolCache.Instance.Cache.TryAdd(defaultCommand.Id, defaultCommand);
            return [defaultCommand];
        }

        internal static string? InferLegacySensorCode(string? templateName)
        {
            string name = templateName?.Trim() ?? string.Empty;
            if (!name.StartsWith("Sensor.", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string code = TrailingTemplateNumberRegex().Replace(name, string.Empty).TrimEnd('_', '-');
            return code.Length > "Sensor.".Length ? code : null;
        }

        private static void RepairMissingCommandDefinitions(SqlSugarClient db, int templateDictionaryId)
        {
            var missingReferences = db.Ado.SqlQuery<MissingCommandReference>(
                @"SELECT d.id AS DetailId,
                         d.cc_pid AS SourceItemId
                  FROM t_scgd_mod_param_detail d
                  INNER JOIN t_scgd_mod_param_master m ON d.pid = m.id
                  LEFT JOIN t_scgd_sys_dictionary_mod_item i ON i.id = d.cc_pid AND i.pid = @templateDictionaryId
                  WHERE m.mm_id = @templateDictionaryId
                    AND d.cc_pid > 0
                    AND i.id IS NULL",
                new { templateDictionaryId });

            int index = 0;
            foreach (var group in missingReferences.GroupBy(reference => reference.SourceItemId).OrderBy(group => group.Key))
            {
                string symbol = index++ == 0 ? DefaultCommandSymbol : $"{DefaultCommandSymbol}_{group.Key}";
                var commandDefinition = db.Queryable<SysDictionaryModDetaiModel>()
                    .Where(item => item.PId == templateDictionaryId && item.Symbol == symbol)
                    .First();
                if (commandDefinition == null)
                {
                    int id = (db.Queryable<SysDictionaryModDetaiModel>().Max(item => (int?)item.Id) ?? 0) + 1;
                    commandDefinition = CreateCommandDefinition(id, templateDictionaryId, symbol);
                    InsertCommandDefinition(db, commandDefinition);
                }

                int[] detailIds = group.Select(reference => reference.DetailId).Distinct().ToArray();
                db.Ado.ExecuteCommand(
                    $@"UPDATE t_scgd_mod_param_detail
                       SET cc_pid = @targetItemId
                       WHERE id IN ({string.Join(", ", detailIds)})",
                    new SugarParameter("@targetItemId", commandDefinition.Id));
                SymbolCache.Instance.Cache.TryAdd(commandDefinition.Id, commandDefinition);
                log.Warn($"已自动重建传感器模板字典引用: templateDictionaryId={templateDictionaryId}, oldCcPid={group.Key}, newCcPid={commandDefinition.Id}");
            }
        }

        private static SysDictionaryModDetaiModel CreateCommandDefinition(int id, int templateDictionaryId, string symbol = DefaultCommandSymbol)
        {
            return new SysDictionaryModDetaiModel
            {
                Id = id,
                AddressCode = id,
                PId = templateDictionaryId,
                Symbol = symbol,
                Name = symbol,
                DefaultValue = DefaultCommandValue,
                ValueType = SValueType.String,
                CreateDate = DateTime.Now,
                IsEnable = true,
                IsDelete = false
            };
        }

        private static void InsertCommandDefinition(SqlSugarClient db, SysDictionaryModDetaiModel commandDefinition)
        {
            db.Ado.ExecuteCommand(
                @"INSERT IGNORE INTO t_scgd_sys_dictionary_mod_item
                  (id, pid, address_code, symbol, name, default_val, val_type, create_date, is_enable, is_delete)
                  VALUES
                  (@id, @pid, @addressCode, @symbol, @name, @defaultValue, @valueType, @createDate, @isEnable, @isDelete)",
                new SugarParameter("@id", commandDefinition.Id),
                new SugarParameter("@pid", commandDefinition.PId),
                new SugarParameter("@addressCode", commandDefinition.AddressCode),
                new SugarParameter("@symbol", commandDefinition.Symbol),
                new SugarParameter("@name", commandDefinition.Name),
                new SugarParameter("@defaultValue", commandDefinition.DefaultValue),
                new SugarParameter("@valueType", (int)commandDefinition.ValueType),
                new SugarParameter("@createDate", commandDefinition.CreateDate),
                new SugarParameter("@isEnable", commandDefinition.IsEnable ? 1 : 0),
                new SugarParameter("@isDelete", commandDefinition.IsDelete ? 1 : 0));
        }

        [GeneratedRegex(@"\d+$", RegexOptions.CultureInvariant)]
        private static partial Regex TrailingTemplateNumberRegex();

        private sealed class MissingCommandReference
        {
            public int DetailId { get; set; }
            public int SourceItemId { get; set; }
        }
    }
}
