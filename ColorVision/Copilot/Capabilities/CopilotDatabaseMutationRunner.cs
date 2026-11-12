using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotDatabaseMutationSession
    {
        Task BeginAsync(CancellationToken cancellationToken);

        Task<int> ExecuteAsync(string sql, int timeoutSeconds, CancellationToken cancellationToken);

        Task CommitAsync(CancellationToken cancellationToken);

        Task RollbackAsync();

        Task<bool> HasRollbackWarningsAsync(int timeoutSeconds);
    }

    internal static class CopilotDatabaseMutationRunner
    {
        public static async Task<CopilotDatabaseMutationResult> ExecuteAsync(
            ICopilotDatabaseMutationSession session,
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            var transactionStarted = false;
            var commandStarted = false;
            var commitStarted = false;
            try
            {
                if (analysis.IsTransactional)
                {
                    await session.BeginAsync(cancellationToken);
                    transactionStarted = true;
                }
                commandStarted = true;
                var affectedRows = await session.ExecuteAsync(sql, timeoutSeconds, cancellationToken);
                if (transactionStarted)
                {
                    commitStarted = true;
                    await session.CommitAsync(cancellationToken);
                }
                return new CopilotDatabaseMutationResult(affectedRows, transactionStarted);
            }
            catch (Exception ex)
            {
                var rollbackConfirmed = false;
                if (transactionStarted && !commitStarted)
                {
                    try
                    {
                        await session.RollbackAsync();
                        rollbackConfirmed = !await session.HasRollbackWarningsAsync(timeoutSeconds);
                    }
                    catch
                    {
                        // A failed rollback or warning check cannot prove that all changes were undone.
                    }
                }

                if (commandStarted && (!analysis.IsTransactional || commitStarted || !rollbackConfirmed))
                    throw new CopilotDatabaseMutationOutcomeUnknownException(ex);
                throw;
            }
        }
    }
}
