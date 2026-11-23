using SqlSugar;
using System;

namespace ColorVision.Database;

/// <summary>A fixed database target. Use IsCurrent to guard work owned by the current connection.</summary>
public sealed class MySqlConnectedEndpoint
{
    private readonly Func<MySqlConnectedEndpoint, bool> _isCurrent;
    private readonly Func<SqlSugarClient> _createDbClient;

    internal MySqlConnectedEndpoint(Func<MySqlConnectedEndpoint, bool> isCurrent, Func<SqlSugarClient> createDbClient)
    {
        _isCurrent = isCurrent;
        _createDbClient = createDbClient;
    }

    public bool IsCurrent => _isCurrent(this);

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
