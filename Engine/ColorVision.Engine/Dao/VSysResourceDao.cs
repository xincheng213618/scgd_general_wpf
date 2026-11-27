#pragma warning disable CA1822
using ColorVision.Database;
using SqlSugar;
using System.Collections.Generic;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Types;
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

        internal LocalTemplateStore LocalResourceStore => LocalStore;

        internal bool TryDeleteResource(SysResourceModel expected, bool includeDirectChildren, Func<bool> isCurrent)
        {
            if (expected.IsDelete) return false;
            int id = expected.Id;
            string? code = expected.Code;
            int type = expected.Type;
            int tenantId = expected.TenantId;
            int? parentId = expected.Pid;

            return TryDeleteLocal(id, row => row.Id == id && string.Equals(row.Code, code, StringComparison.Ordinal)
                && row.Type == type && row.TenantId == tenantId && row.Pid == parentId && !row.IsDelete,
                includeDirectChildren ? row => row.Pid == id : null, isCurrent);
        }

        internal int TryInsertChildResource(SysResourceModel parent, SysResourceModel child, Func<bool> isCurrent)
        {
            int parentId = parent.Id;
            string? parentCode = parent.Code;
            int type = parent.Type;
            int tenantId = parent.TenantId;
            int? grandparentId = parent.Pid;
            string? childCode = child.Code;
            if (!IsLocalId(parentId) || parent.IsDelete || child.Id != 0 || child.Pid != parentId
                || child.Type != type || child.TenantId != tenantId || child.IsDelete || string.IsNullOrWhiteSpace(childCode)) return 0;

            return TryInsertLocalChild(parentId, child,
                row => row.Id == parentId && row.Type == type && row.TenantId == tenantId && row.Pid == grandparentId
                    && !row.IsDelete && string.Equals(row.Code, parentCode, StringComparison.Ordinal),
                row => row.Pid != null && row.TenantId == tenantId && row.IsEnable && !row.IsDelete
                    && string.Equals(row.Code, childCode, StringComparison.Ordinal)
                    && DeviceServiceFactoryRegistry.TryGetFactory((ServiceTypes)row.Type, out _),
                isCurrent);
        }

        public List<SysResourceModel> GetGroupResourceItems(int groupId)
        {
            if (IsLocalId(groupId))
            {
                var ids = GetLocalGroupLinks().Where(link => link.GroupId == groupId).Select(link => link.ResourceId).ToHashSet();
                return GetLocal().Where(resource => ids.Contains(resource.Id)).ToList();
            }
            using var Db = OpenDatabase();
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
            using var db = OpenDatabase();
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
