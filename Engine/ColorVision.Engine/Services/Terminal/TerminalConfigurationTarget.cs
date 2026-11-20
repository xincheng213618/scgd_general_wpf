using ColorVision.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Terminal;

internal sealed record TerminalRemoteRoute(string TypeCode, string ServiceCode, IReadOnlyList<string> DeviceCodes);

internal sealed class TerminalConfigurationTarget
{
    private readonly TerminalService terminal;
    private readonly TerminalServiceConfig config;
    private readonly ServiceObjectBase parent;

    internal TerminalConfigurationTarget(TerminalService terminal)
    {
        this.terminal = terminal;
        config = terminal.Config;
        parent = terminal.Parent;
        Resource = new ServiceResourceTarget(terminal.SysResourceModel, terminal.ResourceDatabaseEndpoint, terminal.LocalConfigurationResourceDao);
    }

    internal ServiceResourceTarget Resource { get; }
    internal MySqlConnectedEndpoint? Endpoint => Resource.Endpoint;
    internal SysResourceDao? LocalResources => Resource.LocalResources;
    internal int ResourceId => Resource.ResourceId;
    internal SysResourceModel ResourceSnapshot => Resource.ResourceSnapshot;

    internal bool Matches(SysResourceModel row) => Resource.Matches(row);

    internal bool IsCurrent(TerminalService current) => ReferenceEquals(terminal, current) && !current.IsConfigurationRetired
        && ReferenceEquals(parent, current.Parent) && ReferenceEquals(config, current.Config)
        && Resource.IsCurrent(current.SysResourceModel, current.ResourceDatabaseEndpoint, current.LocalConfigurationResourceDao);

    internal TerminalRemoteRoute? ReadRemoteRoute(TerminalService current, bool includeDevices = false)
    {
        if (Endpoint is not { } endpoint || !IsCurrent(current)) return null;
        using var db = endpoint.CreateDbClient();
        if (!IsCurrent(current)) return null;

        var row = db.Queryable<SysResourceModel>().InSingle(ResourceId);
        if (row == null || !Matches(row) || string.IsNullOrWhiteSpace(row.Code)) return null;
        int id = row.Id;
        int type = row.Type;
        int tenant = row.TenantId;
        string? typeCode = db.Queryable<SysDictionaryModel>().Where(item => item.Pid == 1 && item.Value == type).Select(item => item.Key).First();
        if (string.IsNullOrWhiteSpace(typeCode) || !IsCurrent(current)) return null;

        string[] deviceCodes = includeDevices
            ? db.Queryable<SysResourceModel>().Where(item => item.Pid == id && item.Type == type && item.TenantId == tenant && item.IsEnable && !item.IsDelete)
                .OrderBy(item => item.Id).Select(item => item.Code).ToList()
                .OfType<string>().Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.Ordinal).ToArray()
            : [];
        return IsCurrent(current) ? new TerminalRemoteRoute(typeCode, row.Code, deviceCodes) : null;
    }
}
