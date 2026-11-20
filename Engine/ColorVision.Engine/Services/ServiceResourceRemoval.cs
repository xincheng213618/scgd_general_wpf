using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services;

internal static class ServiceResourceRemoval
{
    internal static bool TryDeleteRemote(MySqlConnectedEndpoint endpoint, SysResourceModel expected,
        bool includeDirectChildren, Func<bool> isCurrent)
    {
        int id = expected.Id;
        string? code = expected.Code;
        int type = expected.Type;
        int tenantId = expected.TenantId;
        int? parentId = expected.Pid;
        if (id <= 0 || expected.IsDelete || !OwnsSource()) return false;

        using var db = endpoint.CreateDbClient();
        if (!OwnsSource()) return false;

        bool mysql = db.CurrentConnectionConfig.DbType == DbType.MySql;
        if (mysql) db.Ado.BeginTran(System.Data.IsolationLevel.RepeatableRead);
        else db.Ado.BeginTran();

        bool committed = false;
        try
        {
            // Lock the root before touching children. The MySQL child range stays locked until commit.
            string locking = mysql ? " FOR UPDATE" : string.Empty;
            var roots = db.Ado.SqlQuery<ResourceIdentity>(
                "SELECT id AS Id, code AS Code, `type` AS Type, tenant_id AS TenantId, pid AS Pid, is_delete AS IsDelete "
                + "FROM t_scgd_sys_resource WHERE id = @id" + locking, new SugarParameter("@id", id));
            if (roots.Count != 1) return false;
            var row = roots[0];
            if (row.Id != id || row.IsDelete || row.Type != type || row.TenantId != tenantId || row.Pid != parentId
                || !string.Equals(row.Code, code, StringComparison.Ordinal) || !OwnsSource()) return false;

            if (includeDirectChildren)
            {
                var children = db.Ado.SqlQuery<int>(
                    "SELECT id FROM t_scgd_sys_resource WHERE pid = @id AND id <> @id" + locking,
                    new SugarParameter("@id", id));
                if (!OwnsSource()) return false;
                if (children.Count != 0 && db.Deleteable<SysResourceModel>()
                    .Where(child => child.Pid == id && child.Id != id).ExecuteCommand() != children.Count) return false;
            }

            if (!OwnsSource() || db.Deleteable<SysResourceModel>().Where(resource => resource.Id == id).ExecuteCommand() != 1
                || !OwnsSource()) return false;

            db.Ado.CommitTran();
            committed = true;
            return true;
        }
        finally
        {
            if (!committed) db.Ado.RollbackTran();
        }

        bool OwnsSource() => endpoint.IsCurrent && isCurrent();
    }

    private sealed class ResourceIdentity
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public int Type { get; set; }
        public int TenantId { get; set; }
        public int? Pid { get; set; }
        public bool IsDelete { get; set; }
    }
}
