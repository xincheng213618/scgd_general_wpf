using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services;

internal sealed class ServiceResourceTarget
{
    private readonly SysResourceModel resource;
    private readonly SysResourceModel expected;
    private readonly MySqlConnectedEndpoint? endpoint;
    private readonly SysResourceDao? localResources;

    internal ServiceResourceTarget(SysResourceModel resource, MySqlConnectedEndpoint? endpoint, SysResourceDao? localResources)
    {
        this.resource = resource;
        this.endpoint = endpoint;
        this.localResources = localResources;
        expected = new SysResourceModel { Id = resource.Id, Code = resource.Code, Type = resource.Type, TenantId = resource.TenantId, Pid = resource.Pid };
    }

    internal int ResourceId => expected.Id;
    internal MySqlConnectedEndpoint? Endpoint => endpoint;
    internal SysResourceDao? LocalResources => localResources;
    internal SysResourceModel ResourceSnapshot => new()
    {
        Id = expected.Id, Code = expected.Code, Type = expected.Type, TenantId = expected.TenantId, Pid = expected.Pid
    };

    internal bool Matches(SysResourceModel row) => !row.IsDelete && row.Id == expected.Id && row.Type == expected.Type
        && row.TenantId == expected.TenantId && row.Pid == expected.Pid && string.Equals(row.Code, expected.Code, StringComparison.Ordinal);

    internal bool IsCurrent(SysResourceModel current, MySqlConnectedEndpoint? currentEndpoint, SysResourceDao? currentLocalResources)
        => ReferenceEquals(resource, current) && Matches(current)
            && (expected.Id > 0 ? endpoint is { IsCurrent: true } && ReferenceEquals(endpoint, currentEndpoint)
                : SysResourceDao.IsLocalId(expected.Id) && localResources != null && ReferenceEquals(localResources, currentLocalResources));

    internal bool TryDelete(bool includeDirectChildren, Func<bool> isCurrent)
    {
        if (!isCurrent()) return false;
        return expected.Id > 0 && endpoint != null
            ? ServiceResourceRemoval.TryDeleteRemote(endpoint, expected, includeDirectChildren, isCurrent)
            : localResources?.TryDeleteResource(expected, includeDirectChildren, isCurrent) == true;
    }

    internal SysResourceModel? UpdateConfiguration(Action<SysResourceModel> update, Func<bool> isCurrent)
    {
        if (!isCurrent()) return null;
        if (expected.Id <= 0)
        {
            SysResourceModel? saved = null;
            bool updated = localResources?.TryUpdateLocal(expected.Id, row =>
            {
                if (!Matches(row) || !isCurrent()) return false;
                update(row);
                saved = row;
                return isCurrent();
            }) == true;
            return updated ? saved : null;
        }

        if (endpoint == null) return null;
        using var db = endpoint.CreateDbClient();
        if (!isCurrent()) return null;
        var row = db.Queryable<SysResourceModel>().InSingle(expected.Id);
        if (row == null || !Matches(row) || !isCurrent()) return null;

        string? previousCode = row.Code;
        string? previousName = row.Name;
        string? previousValue = row.Value;
        update(row);
        if (!isCurrent()) return null;
        if (string.Equals(previousCode, row.Code, StringComparison.Ordinal)
            && string.Equals(previousName, row.Name, StringComparison.Ordinal)
            && string.Equals(previousValue, row.Value, StringComparison.Ordinal)) return row;

        int id = expected.Id, type = expected.Type, tenant = expected.TenantId;
        int? parentId = expected.Pid;
        var query = db.Updateable<SysResourceModel>()
            .SetColumns(item => new SysResourceModel { Code = row.Code, Name = row.Name, Value = row.Value })
            .Where(item => item.Id == id && item.Type == type && item.TenantId == tenant && item.Pid == parentId && !item.IsDelete);
        if (db.CurrentConnectionConfig.DbType == DbType.MySql)
            query = query.Where("CAST(`code` AS BINARY) <=> CAST(@originalCode AS BINARY) AND "
                + "CAST(`name` AS BINARY) <=> CAST(@originalName AS BINARY) AND CAST(`txt_value` AS BINARY) <=> CAST(@originalValue AS BINARY)",
                new { originalCode = previousCode, originalName = previousName, originalValue = previousValue });
        else query = query.Where(item => item.Code == previousCode && item.Name == previousName && item.Value == previousValue);
        return isCurrent() && query.ExecuteCommand() == 1 ? row : null;
    }
}
