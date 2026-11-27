using ColorVision.Database;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    internal static class SensorTemplateDictionaryService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SensorTemplateDictionaryService));

        internal const int DefaultDictionaryId = 50;
        internal const string DefaultCategoryCode = "Sensor.Default";
        internal const string DefaultCommandSymbol = "defaultcommand";
        internal const string DefaultCommandValue = "\n,,Ascii,1000/0,0";

        internal static bool IsDefaultCategory(string? category) => string.Equals(category, DefaultCategoryCode, StringComparison.OrdinalIgnoreCase);

        internal static string GetUnavailableMessage(string? category) => IsDefaultCategory(category)
            ? EngineLocalization.Format($"未找到传感器默认类型 {DefaultCategoryCode}（ID {DefaultDictionaryId}）。")
            : EngineLocalization.Format($"传感器模板仅支持 {DefaultCategoryCode}，当前传感器类型为“{category}”。");

        internal static void RequireDefaultCategory(string? category)
        {
            if (!IsDefaultCategory(category))
                throw new InvalidOperationException(GetUnavailableMessage(category));
        }

        internal static SysDictionaryModModel? GetDefaultDictionary()
        {
            if (WorkstationConfiguration.ConnectedEndpoint is not { } endpoint) return null;
            try
            {
                using var db = endpoint.CreateDbClient();
                return GetDefaultDictionary(db);
            }
            catch (Exception ex)
            {
                log.Warn("读取传感器默认类型失败。", ex);
                return null;
            }
        }

        internal static SysDictionaryModModel? GetDefaultDictionary(SqlSugarClient db) => db.Queryable<SysDictionaryModModel>()
            .Where(item => item.Id == DefaultDictionaryId && item.Code == DefaultCategoryCode && item.ModType == 5 && item.IsEnable && !item.IsDelete)
            .First();

        internal static void DeleteType(SqlSugarClient db, int dictionaryId, string code)
        {
            if (dictionaryId == DefaultDictionaryId)
                throw new InvalidOperationException(EngineLocalization.Format($"不能删除传感器默认类型 {DefaultCategoryCode}。"));
            db.Ado.BeginTran();
            try
            {
                if (!db.Queryable<SysDictionaryModModel>().Any(item => item.Id == dictionaryId && item.Code == code && item.ModType == 5))
                    throw new InvalidOperationException(Properties.Resources.Sensor_TypeNotFound);
                var devices = db.Queryable<SysResourceModel>().Where(item => item.Type == 5 && !item.IsDelete)
                    .Select(item => new { item.Id, item.Name, item.Code, item.Value }).ToList();
                var references = devices.Where(item => JsonConvert.DeserializeObject<ConfigSensor>(item.Value ?? "{}")?.Category == code)
                    .Select(item => item.Name ?? item.Code ?? item.Id.ToString())
                    .Concat(ServiceManager.Current?.DeviceServices.OfType<DeviceSensor>().Where(item => item.Config.Category == code).Select(item => item.Code) ?? Enumerable.Empty<string>())
                    .Distinct().ToArray();
                if (references.Length > 0)
                    throw new InvalidOperationException(string.Format(Properties.Resources.Sensor_TypeInUse, string.Join(", ", references)));

                int[] templates = db.Queryable<ModMasterModel>().Where(item => item.Pid == dictionaryId).Select(item => item.Id).ToArray();
                int[] definitions = db.Queryable<SysDictionaryModDetaiModel>().Where(item => item.PId == dictionaryId).Select(item => item.Id).ToArray();
                if (definitions.Length > 0 && db.Queryable<ModDetailModel>().Any(item => definitions.Contains(item.SysPid) && !templates.Contains(item.Pid)))
                    throw new InvalidOperationException(Properties.Resources.Sensor_TypeSharedCommands);
                if (templates.Length > 0)
                    db.Deleteable<ModDetailModel>().Where(item => templates.Contains(item.Pid)).ExecuteCommand();
                db.Deleteable<ModMasterModel>().Where(item => item.Pid == dictionaryId).ExecuteCommand();
                db.Deleteable<SysDictionaryModDetaiModel>().Where(item => item.PId == dictionaryId).ExecuteCommand();
                db.Deleteable<SysDictionaryModModel>().Where(item => item.Id == dictionaryId && item.ModType == 5).ExecuteCommand();
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
        }

        internal static List<SysDictionaryModDetaiModel> GetOrCreateDefaultCommandDefinitions(SqlSugarClient db)
        {
            var definitions = db.Queryable<SysDictionaryModDetaiModel>().Where(x => x.PId == DefaultDictionaryId).ToList();
            if (definitions.Count > 0)
            {
                return definitions;
            }

            int id = db.Queryable<SysDictionaryModDetaiModel>().Max(x => (int?)x.Id) ?? 0;
            var defaultCommand = CreateCommandDefinition(id + 1, DefaultDictionaryId);
            InsertCommandDefinition(db, defaultCommand);
            return [defaultCommand];
        }

        private static SysDictionaryModDetaiModel CreateCommandDefinition(int id, int templateDictionaryId)
        {
            return new SysDictionaryModDetaiModel
            {
                Id = id,
                AddressCode = id,
                PId = templateDictionaryId,
                Symbol = DefaultCommandSymbol,
                Name = DefaultCommandSymbol,
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

    }
}
