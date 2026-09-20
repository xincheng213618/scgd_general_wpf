using ColorVision.Database;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ColorVision.Engine.Services
{
    /// <summary>Opt-in configuration storage. Result DAOs continue to use their existing backend.</summary>
    public class LocalConfigurationDao<T> : BaseTableDao<T> where T : class, IEntity, new()
    {
        private readonly string kind;
        protected readonly LocalTemplateStore LocalStore;
        private readonly Func<bool> isConnected;
        private static readonly JsonSerializerSettings JsonSettings = new() { ContractResolver = new ConfigurationContractResolver() };

        protected LocalConfigurationDao(string kind, LocalTemplateStore? store = null, Func<bool>? isConnected = null)
        {
            this.kind = kind;
            LocalStore = store ?? new LocalTemplateStore(LocalTemplateStore.DefaultDatabasePath);
            this.isConnected = isConnected ?? (() => MySqlSetting.IsConnect);
        }

        public bool UseLocal => !isConnected();
        public static bool IsLocalId(int id) => id <= -2;

        public List<T> GetAll(int limit = -1) => UseLocal ? GetLocal(limit) : BaseTableDaoExtensions.GetAll(this, limit);
        public List<T> GetLocal(int limit = -1)
        {
            IEnumerable<LocalTemplateDocument> documents = LocalStore.List(kind);
            if (limit > 0) documents = documents.Take(limit);
            return documents.Select(Decode).ToList();
        }

        private T Decode(LocalTemplateDocument document)
        {
            if (document.SchemaVersion != 1) throw new InvalidDataException("不支持的本地设备配置版本。");
            T model = JsonConvert.DeserializeObject<T>(document.Payload, JsonSettings) ?? throw new InvalidDataException("本地设备配置为空。");
            model.Id = checked(-document.Id - 1);
            return model;
        }

        public T? GetById(int? id)
        {
            if (id == null) return null;
            if (IsLocalId(id.Value)) return GetLocal().FirstOrDefault(item => item.Id == id);
            return UseLocal ? null : BaseTableDaoExtensions.GetById(this, id);
        }

        public List<T> GetAllByPid(int pid) => GetAllByParam(new() { ["pid"] = pid }, local: IsLocalId(pid));

        public List<T> GetAllByParam(Dictionary<string, object> parameters, int limit = -1, bool? local = null)
        {
            if (!(local ?? UseLocal)) return BaseTableDaoExtensions.GetAllByParam(this, parameters, limit);
            IEnumerable<T> rows = GetLocal();
            foreach (var parameter in parameters)
            {
                PropertyInfo property = typeof(T).GetProperties().FirstOrDefault(p => string.Equals(p.GetCustomAttribute<SugarColumn>()?.ColumnName ?? p.Name, parameter.Key, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"未知的配置字段：{parameter.Key}");
                rows = rows.Where(row => string.Equals(Convert.ToString(property.GetValue(row)), Convert.ToString(ConvertValue(parameter.Value, property.PropertyType)), StringComparison.Ordinal));
            }
            if (limit > 0) rows = rows.Take(limit);
            return rows.ToList();
        }

        private static object? ConvertValue(object? value, Type type) => value == null ? null : Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type);

        public int Save(T model, bool? local = null)
        {
            bool useLocal = IsLocalId(model.Id) || (model is SysResourceModel resource && resource.Pid is int pid && IsLocalId(pid)) || (local ?? UseLocal);
            if (!useLocal) return BaseTableDaoExtensions.Save(this, model);
            if (model.Id > 0) throw new InvalidOperationException("MySQL 配置不能写入本地库，请重新创建本地配置。");
            int id = LocalStore.Save(kind, IsLocalId(model.Id) ? checked(-model.Id - 1) : null, typeof(T).Name, 1, JsonConvert.SerializeObject(model, JsonSettings));
            model.Id = checked(-id - 1);
            return 1;
        }

        public int SaveAndReturnId(T model, bool? local = null)
        {
            if (Save(model, local) <= 0) throw new IOException("保存设备配置失败。");
            return model.Id;
        }

        public int DeleteById(int id)
        {
            if (IsLocalId(id)) { LocalStore.Delete(kind, checked(-id - 1)); return 1; }
            if (UseLocal) throw new InvalidOperationException("MySQL 未连接，不能删除服务器配置。");
            return BaseTableDaoExtensions.DeleteById(this, id);
        }

        protected bool DeleteUnchanged(T model, Func<T, bool> canDelete)
        {
            LocalTemplateDocument document = LocalStore.Read(kind, checked(-model.Id - 1));
            // Recheck the expiry from the current document before deleting, including concurrent renewals.
            return canDelete(Decode(document)) && LocalStore.TryDelete(kind, document);
        }

        private sealed class ConfigurationContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
            {
                JsonProperty property = base.CreateProperty(member, serialization);
                if (!property.Writable || member.GetCustomAttribute<SugarColumn>()?.IsIgnore == true) property.Ignored = true;
                return property;
            }
        }
    }
}
