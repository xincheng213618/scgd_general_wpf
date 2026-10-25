using MySqlConnector;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotMySqlDatabaseSqlExecutor
    {
        public Task<CopilotDatabaseMutationResult> ExecuteAsync(
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            CancellationToken cancellationToken) =>
            ExecuteBoundAsync(sql, analysis, timeoutSeconds, string.Empty, cancellationToken);

        public async Task<CopilotDatabaseMutationResult> ExecuteBoundAsync(
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            string expectedTargetFingerprint,
            CancellationToken cancellationToken)
        {
            var connectionString = _connectionStringProvider();
            CopilotDatabaseSqlTarget.EnsureMatches(connectionString, expectedTargetFingerprint);
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var session = new MySqlMutationSession(connection);
            return await CopilotDatabaseMutationRunner.ExecuteAsync(session, sql, analysis, timeoutSeconds, cancellationToken);
        }

        private sealed class MySqlMutationSession(MySqlConnection connection) : ICopilotDatabaseMutationSession, IAsyncDisposable
        {
            private MySqlTransaction? _transaction;

            public async Task BeginAsync(CancellationToken cancellationToken)
            {
                _transaction = await connection.BeginTransactionAsync(cancellationToken);
            }

            public async Task<int> ExecuteAsync(string sql, int timeoutSeconds, CancellationToken cancellationToken)
            {
                await using var command = new MySqlCommand(sql, connection, _transaction) { CommandTimeout = timeoutSeconds };
                return await command.ExecuteNonQueryAsync(cancellationToken);
            }

            public Task CommitAsync(CancellationToken cancellationToken) => _transaction!.CommitAsync(cancellationToken);

            public Task RollbackAsync() => _transaction!.RollbackAsync(CancellationToken.None);

            public async Task<bool> HasRollbackWarningsAsync(int timeoutSeconds)
            {
                // Stored rows may be disabled by max_error_count=0. The count
                // still records incomplete rollback warnings in that case.
                await using var command = new MySqlCommand("SHOW COUNT(*) WARNINGS", connection) { CommandTimeout = timeoutSeconds };
                var count = await command.ExecuteScalarAsync(CancellationToken.None);
                if (count == null || count is DBNull)
                    throw new InvalidOperationException("The database did not return its rollback warning count.");
                return Convert.ToInt64(count, CultureInfo.InvariantCulture) != 0;
            }

            public ValueTask DisposeAsync() => _transaction?.DisposeAsync() ?? default;
        }
    }
}
