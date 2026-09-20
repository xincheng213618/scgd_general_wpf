using ColorVision.Database;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>Routes POI templates only; other Engine/MySQL resources keep their own persistence contracts.</summary>
    public sealed class PoiTemplateStorage
    {
        private const string Kind = "poi";
        private static readonly ILog log = LogManager.GetLogger(typeof(PoiTemplateStorage));
        public static PoiTemplateStorage Default { get; } = new(new LocalTemplateStore(LocalTemplateStore.DefaultDatabasePath), () => MySqlSetting.IsConnect);
        private readonly LocalTemplateStore local;
        private readonly Func<bool> isMySqlConnected;
        private readonly Func<SqlSugarClient> openMySql;
        private bool remoteReadFailed;
        public bool IsLocal => remoteReadFailed || !isMySqlConnected();
        public string Location => IsLocal ? local.DatabasePath : "MySQL";

        public PoiTemplateStorage(LocalTemplateStore local, Func<bool> isMySqlConnected, Func<SqlSugarClient>? openMySql = null)
        {
            this.local = local;
            this.isMySqlConnected = isMySqlConnected;
            this.openMySql = openMySql ?? (() => new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true }));
        }

        // Local IDs are <= -2; -1 remains the empty/new sentinel. MySQL identities stay positive.
        public static bool IsLocalId(int id) => id < -1;
        private static int LocalId(int id) => checked(-id - 1);

        public IReadOnlyList<PoiParam> Load()
        {
            remoteReadFailed = false;
            if (isMySqlConnected())
            {
                try
                {
                    using var db = openMySql();
                    return db.Queryable<PoiMasterModel>().Where(x => x.TenantId == 0 && x.IsDelete == false).OrderBy(x => x.Id).ToList()
                        .Select(x => Attach(new PoiParam(x))).ToList();
                }
                catch (Exception ex)
                {
                    remoteReadFailed = true;
                    log.Warn("MySQL POI templates unavailable; using local templates.", ex);
                }
            }
            return local.List(Kind).Select(Decode).ToList();
        }

        private PoiParam Attach(PoiParam value) { value.Storage = this; return value; }

        private PoiParam Decode(LocalTemplateDocument document)
        {
            if (document.SchemaVersion != 1) throw new InvalidDataException($"不支持的 POI 模板版本：{document.SchemaVersion}");
            var value = JsonConvert.DeserializeObject<PoiParam>(document.Payload) ?? throw new InvalidDataException("本地 POI 模板内容无效。");
            value.Id = LocalId(document.Id);
            value.Name = document.Name;
            value.DetailsLoaded = true;
            return Attach(value);
        }

        public List<PoiPoint> ReadPoints(int id)
        {
            if (IsLocalId(id)) return Decode(local.Read(Kind, LocalId(id))).PoiPoints.ToList();
            if (id <= 0) return new();
            if (!isMySqlConnected()) throw new InvalidOperationException("此模板来自 MySQL，当前连接已断开。请重新打开模板列表使用本地模板。");
            using var db = openMySql();
            return db.Queryable<PoiDetailModel>().Where(x => x.Pid == id).OrderBy(x => x.Id).ToList().Select(x => new PoiPoint(x)).ToList();
        }

        public void Save(PoiParam value)
        {
            if (IsLocalId(value.Id) || IsLocal)
            {
                SaveLocal(value);
                return;
            }
            try
            {
                using var db = openMySql();
                db.Ado.BeginTran();
                try
                {
                    var master = new PoiMasterModel(value);
                    int id = value.Id;
                    if (id <= 0) id = db.Insertable(master).ExecuteReturnIdentity();
                    else if (db.Updateable(master).ExecuteCommand() == 0 && !db.Queryable<PoiMasterModel>().Any(x => x.Id == id))
                        throw new InvalidDataException("MySQL POI 模板已不存在。");
                    if (id <= 0) throw new InvalidDataException("创建 MySQL POI 模板失败。");
                    db.Deleteable<PoiDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                    var details = value.PoiPoints.Select(x => new PoiDetailModel(id, x) { Id = 0 }).ToList();
                    if (details.Count > 0) db.Insertable(details).ExecuteCommand();
                    db.Ado.CommitTran();
                    value.Id = id;
                    value.DetailsLoaded = true;
                    Attach(value);
                }
                catch { db.Ado.RollbackTran(); throw; }
            }
            catch (Exception ex)
            {
                log.Warn("MySQL POI save failed; preserving complete template in local storage.", ex);
                remoteReadFailed = true;
                SaveLocal(value);
            }
        }

        private void SaveLocal(PoiParam value)
        {
            // Never overwrite a same-numbered local template with a disconnected server record.
            if (value.Id > 0 && !value.DetailsLoaded) throw new InvalidOperationException("MySQL 模板明细尚未完整加载，不能保存不完整的本地副本。");
            int? id = IsLocalId(value.Id) ? LocalId(value.Id) : null;
            var snapshot = TemplatePoi.CreatePortableSnapshot(value);
            for (int i = 0; i < snapshot.PoiPoints.Count; i++) snapshot.PoiPoints[i].Id = i + 1;
            int saved = local.Save(Kind, id, value.Name, 1, JsonConvert.SerializeObject(snapshot));
            value.Id = LocalId(saved);
            value.DetailsLoaded = true;
            Attach(value);
        }

        public void SaveMetadata(PoiParam value)
        {
            // Lists may contain masters only. Renaming must not replace the persisted points with an empty list.
            var snapshot = JsonConvert.DeserializeObject<PoiParam>(JsonConvert.SerializeObject(value))!;
            snapshot.PoiPoints = new(ReadPoints(value.Id));
            snapshot.DetailsLoaded = true;
            Save(snapshot);
            value.Id = snapshot.Id;
            Attach(value);
        }

        public void Delete(int id)
        {
            if (IsLocalId(id)) { local.Delete(Kind, LocalId(id)); return; }
            if (!isMySqlConnected()) throw new InvalidOperationException("MySQL 未连接，不能删除服务器模板。");
            using var db = openMySql();
            db.Ado.BeginTran();
            try
            {
                db.Deleteable<PoiDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                db.Deleteable<PoiMasterModel>().Where(x => x.Id == id).ExecuteCommand();
                db.Ado.CommitTran();
            }
            catch { db.Ado.RollbackTran(); throw; }
        }

        public void SwapLocalOrder(int first, int second) => local.SwapOrder(Kind, LocalId(first), LocalId(second));
    }
}
