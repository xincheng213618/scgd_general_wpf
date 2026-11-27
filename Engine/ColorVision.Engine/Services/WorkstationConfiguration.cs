using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using System;
using System.Threading;

namespace ColorVision.Engine.Services;

public enum WorkstationConfigurationSource
{
    AutomaticAtStartup,
    Local,
    MySql
}

public sealed class WorkstationConfigurationSettings : ViewModelBase, IConfig
{
    public static WorkstationConfigurationSettings Instance => ConfigService.Instance.GetRequiredService<WorkstationConfigurationSettings>();

    public WorkstationConfigurationSource Source { get => source; set => SetProperty(ref source, value); }
    private WorkstationConfigurationSource source;
}

internal sealed record WorkstationConfigurationSelection(WorkstationConfigurationSource Source, MySqlDatabaseTarget? DatabaseTarget)
{
    internal static WorkstationConfigurationSelection Capture(WorkstationConfigurationSource preference,
        MySqlDatabaseTarget? configuredTarget, MySqlConnectedEndpoint? connected)
    {
        bool local = preference switch
        {
            WorkstationConfigurationSource.AutomaticAtStartup => connected is not { IsCurrent: true },
            WorkstationConfigurationSource.Local => true,
            WorkstationConfigurationSource.MySql => false,
            _ => throw new ArgumentOutOfRangeException(nameof(preference))
        };
        return local ? new(WorkstationConfigurationSource.Local, null)
            : new(WorkstationConfigurationSource.MySql, configuredTarget);
    }

    internal bool OwnsDatabaseTarget(MySqlConnectedEndpoint endpoint) =>
        Source == WorkstationConfigurationSource.MySql && DatabaseTarget != null && endpoint.DatabaseTarget == DatabaseTarget;

    internal MySqlConnectedEndpoint? Accept(MySqlConnectedEndpoint? endpoint) =>
        endpoint is { IsCurrent: true } && OwnsDatabaseTarget(endpoint) ? endpoint : null;
}

internal sealed class WorkstationConfigurationSession(Func<WorkstationConfigurationSelection> read)
{
    private readonly Lazy<WorkstationConfigurationSelection> selection = new(read);
    private int validatingAtStartup;

    internal WorkstationConfigurationSelection Preview => selection.IsValueCreated ? selection.Value : read();
    // File viewers and hosts that skip database startup pin on their first catalog access.
    // In a normal startup the initializer pins after validation and any local repair.
    internal WorkstationConfigurationSelection ForUse => Volatile.Read(ref validatingAtStartup) == 0 ? selection.Value : Preview;
    internal void BeginStartup() => Volatile.Write(ref validatingAtStartup, 1);

    internal void CompleteStartup()
    {
        _ = selection.Value;
        Volatile.Write(ref validatingAtStartup, 0);
    }
}

public static class WorkstationConfiguration
{
    private static readonly WorkstationConfigurationSession Session = new(ReadSelection);

    internal static void BeginStartup() => Session.BeginStartup();
    internal static void CompleteStartup() => Session.CompleteStartup();

    private static WorkstationConfigurationSelection ReadSelection()
    {
        var config = ConfigService.Instance;
        MySqlConfig? database = config?.GetRequiredService<MySqlSetting>().MySqlConfig;
        return WorkstationConfigurationSelection.Capture(
            config?.GetRequiredService<WorkstationConfigurationSettings>().Source ?? WorkstationConfigurationSource.AutomaticAtStartup,
            database == null ? null : MySqlDatabaseTarget.From(database), MySqlControl.GetInstance().ConnectedEndpoint);
    }

    public static WorkstationConfigurationSource ActiveSource => Session.Preview.Source;
    public static bool UseLocal => Session.ForUse.Source == WorkstationConfigurationSource.Local;
    public static MySqlConnectedEndpoint? ConnectedEndpoint => Session.ForUse.Accept(MySqlControl.GetInstance().ConnectedEndpoint);
    public static bool OwnsDatabaseTarget(MySqlConnectedEndpoint endpoint) => Session.ForUse.OwnsDatabaseTarget(endpoint);

    internal static MySqlConnectedEndpoint RequireEndpoint() => ConnectedEndpoint
        ?? throw new InvalidOperationException("当前工位的配置数据库未连接，请连接原数据库后重试。");
}
