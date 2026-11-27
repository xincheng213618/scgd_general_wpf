using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>Routes POI templates only; other Engine/MySQL resources keep their own persistence contracts.</summary>
    public sealed partial class PoiTemplateStorage
    {
        private const string Kind = "poi";
        public static PoiTemplateStorage Default { get; } = new(new LocalTemplateStore(LocalTemplateStore.DefaultDatabasePath), TemplateStorageSource.Workstation);
        private readonly LocalTemplateStore local;
        private readonly TemplateStorageSource source;
        private readonly TemplateDatabase? databaseOwner;
        public bool IsLocal => source.UseLocal;
        public string Location => IsLocal ? local.DatabasePath : "MySQL";

        public PoiTemplateStorage(LocalTemplateStore local, Func<bool> isMySqlConnected, Func<SqlSugarClient>? openMySql = null)
            : this(local, new TemplateStorageSource(isMySqlConnected, openMySql)) { }

        internal PoiTemplateStorage(LocalTemplateStore local, TemplateStorageSource source, TemplateDatabase? databaseOwner = null)
        {
            this.local = local;
            this.source = source;
            this.databaseOwner = databaseOwner;
        }

        internal TemplateDatabase CaptureDatabase() => databaseOwner ?? source.CaptureDatabase();

        private PoiTemplateStorage BindDatabase() => databaseOwner == null ? new(local, source, source.CaptureDatabase()) : this;

        // Local IDs are <= -2; -1 remains the empty/new sentinel. MySQL identities stay positive.
        public static bool IsLocalId(int id) => id < -1;
        private static int LocalId(int id) => checked(-id - 1);

        public IReadOnlyList<PoiParam> Load()
        {
            if (IsLocal) return local.List(Kind).Select(Decode).ToList();
            var owner = BindDatabase();
            using var db = owner.CaptureDatabase().Open();
            var values = db.Queryable<PoiMasterModel>().Where(x => x.TenantId == 0 && x.IsDelete == false).OrderBy(x => x.Id).ToList()
                .Select(x => owner.Attach(new PoiParam(x))).ToList();
            if (!owner.CaptureDatabase().IsCurrent) throw TemplateDatabase.Unavailable();
            return values;
        }

        private PoiParam Attach(PoiParam value) { value.Storage = this; return value; }

        private PoiParam Decode(LocalTemplateDocument document)
        {
            if (document.SchemaVersion != 1) throw new InvalidDataException($"不支持的 POI 模板版本：{document.SchemaVersion}");
            var value = JsonConvert.DeserializeObject<PoiParam>(document.Payload) ?? throw new InvalidDataException("本地 POI 模板内容无效。");
            value.Id = LocalId(document.Id);
            value.Name = document.Name;
            value.TemplateDate = document.CreatedUtc?.ToLocalTime();
            value.DetailsLoaded = true;
            return Attach(value);
        }

        internal PoiParam ReadSnapshot(PoiParam selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            var owner = selected.Storage ?? this;
            if (IsLocalId(selected.Id)) return owner.Decode(owner.local.Read(Kind, LocalId(selected.Id)));
            if (selected.Id <= 0) throw new InvalidOperationException("POI 模板尚未保存。");
            owner = owner.BindDatabase();
            var database = owner.CaptureDatabase();
            using var db = database.Open();
            db.Ado.BeginTran();
            try
            {
                var master = db.Queryable<PoiMasterModel>().First(row => row.Id == selected.Id && row.TenantId == 0 && row.IsDelete == false)
                    ?? throw new InvalidDataException($"原数据库中的 POI 模板已不存在：{selected.Id}。");
                var snapshot = new PoiParam(master) { PoiPoints = new(ReadPoints(db, selected.Id)), DetailsLoaded = true };
                if (!database.IsCurrent) throw TemplateDatabase.Unavailable();
                db.Ado.CommitTran();
                return owner.Attach(snapshot);
            }
            catch { db.Ado.RollbackTran(); throw; }
        }

        public List<PoiPoint> ReadPoints(int id)
        {
            if (IsLocalId(id)) return Decode(local.Read(Kind, LocalId(id))).PoiPoints.ToList();
            if (id <= 0) return new();
            using var db = CaptureDatabase().Open();
            return ReadPoints(db, id);
        }

        private static List<PoiPoint> ReadPoints(SqlSugarClient db, int id) =>
            db.Queryable<PoiDetailModel>().Where(x => x.Pid == id).OrderBy(x => x.Id).ToList().Select(x => new PoiPoint(x)).ToList();

        public void Save(PoiParam value)
        {
            var storage = value.Storage ?? this;
            if (IsLocalId(value.Id) || (value.Id <= 0 && storage.IsLocal))
            {
                storage.SaveLocal(value);
                return;
            }
            var owner = storage.BindDatabase();
            using var db = owner.CaptureDatabase().Open();
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
                value.TemplateDate = master.CreateDate;
                value.DetailsLoaded = true;
                owner.Attach(value);
            }
            catch { db.Ado.RollbackTran(); throw; }
        }

        private void SaveLocal(PoiParam value)
        {
            int? id = IsLocalId(value.Id) ? LocalId(value.Id) : null;
            var snapshot = TemplatePoi.CreatePortableSnapshot(value);
            for (int i = 0; i < snapshot.PoiPoints.Count; i++) snapshot.PoiPoints[i].Id = i + 1;
            int saved = local.Save(Kind, id, value.Name, 1, JsonConvert.SerializeObject(snapshot));
            value.Id = LocalId(saved);
            value.TemplateDate = local.Read(Kind, saved).CreatedUtc?.ToLocalTime();
            value.DetailsLoaded = true;
            Attach(value);
        }

        public void SaveMetadata(PoiParam value)
        {
            // Lists may contain masters only. Renaming must not replace the persisted points with an empty list.
            var snapshot = JsonConvert.DeserializeObject<PoiParam>(JsonConvert.SerializeObject(value))!;
            var owner = value.Storage ?? this;
            snapshot.PoiPoints = new(owner.ReadPoints(value.Id));
            snapshot.DetailsLoaded = true;
            owner.Save(snapshot);
            value.Id = snapshot.Id;
            value.TemplateDate = snapshot.TemplateDate;
            value.Storage = snapshot.Storage;
        }

        public void Delete(int id)
        {
            if (IsLocalId(id)) { local.Delete(Kind, LocalId(id)); return; }
            using var db = CaptureDatabase().Open();
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
