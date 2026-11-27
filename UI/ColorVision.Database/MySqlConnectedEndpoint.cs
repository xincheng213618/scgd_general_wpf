using SqlSugar;
using System;

namespace ColorVision.Database;

public sealed record MySqlDatabaseTarget(string Host, int Port, string Database)
{
    public static MySqlDatabaseTarget From(MySqlConfig config) =>
        new((config.Host ?? string.Empty).Trim().ToUpperInvariant(), config.Port, config.Database ?? string.Empty);
}

/// <summary>A fixed database target. Use IsCurrent to guard work owned by the current connection.</summary>
public sealed class MySqlConnectedEndpoint
{
    private readonly Func<MySqlConnectedEndpoint, bool> _isCurrent;
    private readonly Func<SqlSugarClient> _createDbClient;

    internal MySqlConnectedEndpoint(Func<MySqlConnectedEndpoint, bool> isCurrent, Func<SqlSugarClient> createDbClient,
        MySqlDatabaseTarget? databaseTarget = null)
    {
        _isCurrent = isCurrent;
        _createDbClient = createDbClient;
        DatabaseTarget = databaseTarget;
    }

    public bool IsCurrent => _isCurrent(this);
    public MySqlDatabaseTarget? DatabaseTarget { get; }

    /// <summary>Creates a short-lived client for this endpoint, even if the configured target has changed.</summary>
    public SqlSugarClient CreateDbClient() => _createDbClient();
}

public enum MySqlConnectOutcome
{
    Connected,
    Failed,
    Superseded,
}

public sealed class MySqlConnectionAttempt
{
    private readonly Func<bool> _isCurrent;

    internal MySqlConnectionAttempt(MySqlConnectOutcome outcome, string host, Func<bool> isCurrent)
    {
        Outcome = outcome;
        Host = host;
        _isCurrent = isCurrent;
    }

    public MySqlConnectOutcome Outcome { get; }
    public string Host { get; }
    public bool IsCurrent => _isCurrent();
}
