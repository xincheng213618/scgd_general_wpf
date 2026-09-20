#pragma warning disable CA1822
using ColorVision.Database;
using SqlSugar;
using System.Collections.Generic;
using ColorVision.Engine.Services;
using System;
using System.Linq;
using Newtonsoft.Json;

namespace ColorVision.Engine
{
    public class SysResourceDao : LocalConfigurationDao<SysResourceModel>
    {
        public static SysResourceDao Instance { get; set; } =  new SysResourceDao();
        public SysResourceDao(LocalTemplateStore? store = null, Func<bool>? isConnected = null) : base("device-resource", store, isConnected)
        {
        }

        public List<SysResourceModel> GetGroupResourceItems(int groupId)
        {
            if (IsLocalId(groupId))
            {
                var ids = GetLocalGroupLinks().Where(link => link.GroupId == groupId).Select(link => link.ResourceId).ToHashSet();
                return GetLocal().Where(resource => ids.Contains(resource.Id)).ToList();
            }
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            return Db.Queryable<SysResourceGoupModel, SysResourceModel>((rg, r) => rg.ResourceId == r.Id).Where((rg, r) => rg.GroupId == groupId).Select((rg, r) => r).ToList();
        }


        public List<SysResourceModel> GetAllType(int type) => this.GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });

        public List<SysResourceGoupModel> GetLocalGroupLinks() => LocalStore.List("device-group-links")
            .SelectMany(document => JsonConvert.DeserializeObject<List<SysResourceGoupModel>>(document.Payload) ?? new()).ToList();

        public void ReplaceGroupResources(int groupId, IEnumerable<int> resourceIds)
        {
            var ids = resourceIds.Distinct().ToArray();
            if (IsLocalId(groupId))
            {
                if (GetById(groupId) == null || ids.Any(id => !IsLocalId(id) || GetById(id) == null))
                    throw new InvalidOperationException("本地资源组只能引用存在的本地资源。");
                var document = LocalStore.List("device-group-links").FirstOrDefault(row => row.Name == groupId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var links = ids.Select(id => new SysResourceGoupModel { GroupId = groupId, ResourceId = id });
                LocalStore.Save("device-group-links", document?.Id, groupId.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, JsonConvert.SerializeObject(links));
                return;
            }
            if (UseLocal) throw new InvalidOperationException("MySQL 未连接，不能修改服务器资源组。");
            using var db = MySqlControl.CreateDbClient();
            db.Ado.BeginTran();
            try
            {
                db.Deleteable<SysResourceGoupModel>().Where(link => link.GroupId == groupId).ExecuteCommand();
                if (ids.Length > 0) db.Insertable(ids.Select(id => new SysResourceGoupModel { GroupId = groupId, ResourceId = id }).ToList()).ExecuteCommand();
                db.Ado.CommitTran();
            }
            catch { db.Ado.RollbackTran(); throw; }
        }
    }

}
