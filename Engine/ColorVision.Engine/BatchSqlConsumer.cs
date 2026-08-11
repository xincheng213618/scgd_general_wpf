using ColorVision.Common.Utilities;
using ColorVision.Database;
using log4net;
using System;
using System.Windows;

namespace ColorVision.Engine
{
    internal static class BatchSqlConsumer
    {
        public static int ExecuteAfterCommit(string sqlBatch, Action afterCommit)
        {
            return ExecuteAfterCommit(sqlBatch, MySqlControl.BatchExecuteNonQuery, afterCommit);
        }

        internal static int ExecuteAfterCommit(string sqlBatch, Func<string, int> executeBatch, Action afterCommit)
        {
            ArgumentNullException.ThrowIfNull(executeBatch);
            ArgumentNullException.ThrowIfNull(afterCommit);

            int affectedRows = executeBatch(sqlBatch);
            afterCommit();
            return affectedRows;
        }

        public static T ExecuteAfterCommit<T>(string sqlBatch, Func<T> afterCommit)
        {
            return ExecuteAfterCommit(sqlBatch, MySqlControl.BatchExecuteNonQuery, afterCommit);
        }

        internal static T ExecuteAfterCommit<T>(string sqlBatch, Func<string, int> executeBatch, Func<T> afterCommit)
        {
            ArgumentNullException.ThrowIfNull(executeBatch);
            ArgumentNullException.ThrowIfNull(afterCommit);

            executeBatch(sqlBatch);
            return afterCommit();
        }

        public static void ReportUiFailure(ILog logger, string operation, BatchExecuteNonQueryException exception)
        {
            ArgumentNullException.ThrowIfNull(logger);
            logger.Error(FormatDiagnosticSummary(operation, exception));
            MessageBox.Show(WindowHelpers.GetActiveWindow(), FormatFailureMessage(operation, exception), "ColorVision");
        }

        internal static string FormatFailureMessage(string operation, BatchExecuteNonQueryException exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentNullException.ThrowIfNull(exception);

            string location = exception.StatementIndex.HasValue
                ? $"，语句序号 {exception.StatementIndex.Value}"
                : string.Empty;
            return $"{operation}失败，后续操作已停止。\r\n错误标识：{exception.Stage}{location} / {exception.FailureType} ({exception.ErrorCode})。\r\n请检查日志或联系管理员。";
        }

        internal static string FormatDiagnosticSummary(string operation, BatchExecuteNonQueryException exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentNullException.ThrowIfNull(exception);

            string statement = exception.StatementIndex.HasValue ? $"; StatementIndex={exception.StatementIndex.Value}" : string.Empty;
            string rollback = exception.RollbackFailureType == null ? string.Empty : $"; RollbackFailureType={exception.RollbackFailureType}; RollbackErrorCode={exception.RollbackErrorCode}";
            string dispose = exception.DisposeFailureType == null ? string.Empty : $"; DisposeFailureType={exception.DisposeFailureType}; DisposeErrorCode={exception.DisposeErrorCode}";
            return $"{operation}失败。Stage={exception.Stage}{statement}; FailureType={exception.FailureType}; ErrorCode={exception.ErrorCode}{rollback}{dispose}";
        }
    }
}
