using ColorVision.Database;
using ColorVision.Engine.Services;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates;

/// <summary>Flow and POI catalogues use the configuration source selected for this workstation.</summary>
internal sealed class TemplateStorageSource
{
    internal static TemplateStorageSource Workstation { get; } = new(null, null);
    private readonly bool? useLocal;
    private readonly Func<bool>? connected;
    private readonly Func<SqlSugarClient>? openDatabase;
    private readonly Func<MySqlConnectedEndpoint, bool> ownsBatchTarget = WorkstationConfiguration.OwnsDatabaseTarget;
    internal bool UseLocal => useLocal ?? WorkstationConfiguration.UseLocal;

    internal TemplateStorageSource(Func<bool>? connected, Func<SqlSugarClient>? openDatabase, Func<MySqlConnectedEndpoint, bool>? ownsBatchTarget = null)
    {
        this.connected = connected;
        this.openDatabase = openDatabase;
        if (connected != null) useLocal = !connected();
        if (ownsBatchTarget != null) this.ownsBatchTarget = ownsBatchTarget;
    }

    internal SqlSugarClient OpenTemplateDatabase(MySqlConnectedEndpoint? batchEndpoint) =>
        batchEndpoint != null && ownsBatchTarget(batchEndpoint)
            ? batchEndpoint.CreateDbClient()
            : CaptureDatabase().Open();

    internal TemplateDatabase CaptureDatabase()
    {
        if (UseLocal) throw new InvalidOperationException("当前工位使用本地配置，无法读写 MySQL 模板。");
        if (connected != null && openDatabase != null) return new(connected, openDatabase);
        var endpoint = WorkstationConfiguration.ConnectedEndpoint ?? throw TemplateDatabase.Unavailable();
        return new(() => (connected?.Invoke() ?? true) && ReferenceEquals(endpoint, WorkstationConfiguration.ConnectedEndpoint),
            openDatabase ?? endpoint.CreateDbClient);
    }
}

/// <summary>Retain the endpoint that supplied a template. A stale editor must re-open before writing.</summary>
internal sealed class TemplateDatabase(Func<bool> isCurrent, Func<SqlSugarClient> openDatabase)
{
    internal bool IsCurrent => isCurrent();
    internal SqlSugarClient Open() => IsCurrent ? openDatabase() : throw Unavailable();
    internal static InvalidOperationException Unavailable() =>
        new("工位模板的原 MySQL 连接不可用，请恢复连接并重新打开模板。");
}
