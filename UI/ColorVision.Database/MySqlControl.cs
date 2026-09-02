using ColorVision.Common.MVVM;
using ColorVision.Database.Properties;
using log4net;
using MySqlConnector;
using SqlSugar;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Database
{

    public enum BatchExecutionStage
    {
        CreateExecutor,
        PrepareBatch,
        BeginTransaction,
        ExecuteStatement,
        CommitTransaction,
        DisposeExecutor
    }

    public sealed class BatchExecuteNonQueryException : InvalidOperationException
    {
        internal BatchExecuteNonQueryException(
            BatchExecutionStage stage,
            int? statementIndex,
            Exception primaryException,
            Exception? rollbackException)
            : base(CreateMessage(stage, statementIndex, primaryException, rollbackException))
        {
            Stage = stage;
            StatementIndex = statementIndex;
            FailureType = GetFailureType(primaryException);
            ErrorCode = GetErrorCode(primaryException);
            if (rollbackException != null)
            {
                RollbackFailureType = GetFailureType(rollbackException);
                RollbackErrorCode = GetErrorCode(rollbackException);
            }
        }

        public BatchExecutionStage Stage { get; }

        /// <summary>
        /// Gets the one-based index of the failed non-empty SQL statement.
        /// </summary>
        public int? StatementIndex { get; }

        public string FailureType { get; }

        public int ErrorCode { get; }

        public string? RollbackFailureType { get; }

        public int? RollbackErrorCode { get; }

        public string? DisposeFailureType { get; private set; }

        public int? DisposeErrorCode { get; private set; }

        internal void RecordDisposeFailure(Exception exception)
        {
            DisposeFailureType = GetFailureType(exception);
            DisposeErrorCode = GetErrorCode(exception);
        }

        internal string GetDiagnosticSummary()
        {
            string statement = StatementIndex.HasValue ? $"; StatementIndex={StatementIndex.Value}" : string.Empty;
            string rollback = RollbackFailureType == null ? string.Empty : $"; RollbackFailureType={RollbackFailureType}; RollbackErrorCode={RollbackErrorCode}";
            string dispose = DisposeFailureType == null ? string.Empty : $"; DisposeFailureType={DisposeFailureType}; DisposeErrorCode={DisposeErrorCode}";
            return $"Stage={Stage}{statement}; FailureType={FailureType}; ErrorCode={ErrorCode}{rollback}{dispose}";
        }

        private static string CreateMessage(BatchExecutionStage stage, int? statementIndex, Exception primaryException, Exception? rollbackException)
        {
            string statement = statementIndex.HasValue ? $"; StatementIndex={statementIndex.Value}" : string.Empty;
            string rollback = rollbackException == null
                ? string.Empty
                : $"; RollbackFailureType={GetFailureType(rollbackException)}; RollbackErrorCode={GetErrorCode(rollbackException)}";
            return $"SQL batch failed. Stage={stage}{statement}; FailureType={GetFailureType(primaryException)}; ErrorCode={GetErrorCode(primaryException)}{rollback}.";
        }

        internal static string GetFailureType(Exception exception)
        {
            return exception.GetType().Name;
        }

        internal static int GetErrorCode(Exception exception)
        {
            return exception is MySqlException mySqlException ? mySqlException.Number : exception.HResult;
        }
    }

    internal sealed class BatchCommittedCleanupWarning
    {
        public BatchCommittedCleanupWarning(Exception exception)
        {
            Stage = BatchExecutionStage.DisposeExecutor;
            FailureType = BatchExecuteNonQueryException.GetFailureType(exception);
            ErrorCode = BatchExecuteNonQueryException.GetErrorCode(exception);
        }

        public BatchExecutionStage Stage { get; }

        public string FailureType { get; }

        public int ErrorCode { get; }

        public string GetDiagnosticSummary()
        {
            return $"Stage={Stage}; FailureType={FailureType}; ErrorCode={ErrorCode}";
        }
    }

    internal interface IBatchSqlExecutor : IDisposable
    {
        void BeginTransaction();

        int ExecuteNonQuery(string sql);

        void CommitTransaction();

        void RollbackTransaction();
    }

    internal sealed class SqlSugarBatchSqlExecutor : IBatchSqlExecutor
    {
        private readonly SqlSugarClient _db = new(new ConnectionConfig
        {
            ConnectionString = MySqlControl.GetConnectionString(),
            DbType = SqlSugar.DbType.MySql,
            IsAutoCloseConnection = true
        });

        public void BeginTransaction() => _db.Ado.BeginTran();

        public int ExecuteNonQuery(string sql) => _db.Ado.ExecuteCommand(sql);

        public void CommitTransaction() => _db.Ado.CommitTran();

        public void RollbackTransaction() => _db.Ado.RollbackTran();

        public void Dispose() => _db.Dispose();
    }

    public class MySqlControl: ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }

        public static MySqlConfig Config => MySqlSetting.Instance.MySqlConfig;


        public MySqlControl()
        {
            StaticConfig.BulkCopy_MySqlCsvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "bulkcopyfiles");
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect
        {
            get => _IsConnect;
            private set
            {
                _IsConnect = value;
                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    OnPropertyChanged();
                    MySqlConnectChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged();
                        MySqlConnectChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }
        private bool _IsConnect;

        private static readonly char[] separator = new[] { ';' };

        public Task<bool> Connect()
        {
            string connStr = GetConnectionString(Config);
            try
            {
                IsConnect = false;
                var newConn = new MySqlConnection() { ConnectionString = connStr };
                newConn.Open();

                log.Info($"数据库连接成功:{GetConnectionSummary(Config)}");
                using var  _DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = GetConnectionString(Config), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                // 检查数据库名是否为空
                // 检查当前 local_infile 的值
                int localInfile = _DB.Ado.GetInt("SELECT @@global.local_infile;");

                if (localInfile == 0)
                {
                    // 不支持则设置为 1
                    _DB.Ado.ExecuteCommand("SET GLOBAL local_infile = 1;");
                    log.Info("local_infile 已设置为 1");
                }
                else
                {
                    log.Info("local_infile 已经支持");
                }
                IsConnect = true;
                newConn.Close();
                newConn.Dispose();

                return Task.FromResult(true);
            }
            catch (MySqlException ex)
            {
                IsConnect = false;
                string detailMsg = ex.Number switch
                {
                    1045 => "账号或密码错误",
                    1049 => "指定的数据库不存在",
                    2003 => "无法连接到MySQL服务器，可能是端口未打开或网络不可达",
                    _ => $"MySqlException 错误码: {ex.Number}，错误信息: {ex.Message}"
                };
                log.Error($"数据库连接失败: {detailMsg}. 连接: {GetConnectionSummary(Config)}");
                log.Error(ex);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                IsConnect = false;
                log.Error($"数据库连接发生未知异常: {ex.Message}. 连接: {GetConnectionSummary(Config)}");
                log.Error(ex);
                return Task.FromResult(false);
            }
        }
        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        public static string GetConnectionString() => GetConnectionString(Config);

        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout = 1)
        {
            return GetConnectionString(MySqlConfig, timeout, MySqlConfig.Database);
        }

        public static string GetConnectionString(MySqlConfig MySqlConfig, int timeout, string? databaseName)
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={databaseName ?? string.Empty};charset={MySqlProtocolDefaults.CharacterSet};Connect Timeout={timeout};SSL Mode =None;Pooling=true;AllowLoadLocalInfile=True";
            return connStr;
        }

        private static string GetConnectionSummary(MySqlConfig mySqlConfig)
        {
            string database = string.IsNullOrWhiteSpace(mySqlConfig.Database) ? "<empty>" : mySqlConfig.Database;
            string user = string.IsNullOrWhiteSpace(mySqlConfig.UserName) ? "<empty>" : "***";
            return $"server={mySqlConfig.Host};port={mySqlConfig.Port};uid={user};database={database}";
        }

        public static void TestConnect(MySqlConfig MySqlConfig)
        {
            string connStr = GetConnectionString(MySqlConfig, 2);
            try
            {
                log.Info($"Test数据库连接信息:{GetConnectionSummary(MySqlConfig)}");
                using (var mySqlConnection = new MySqlConnection(connStr))
                {
                    mySqlConnection.Open();

                    if (string.IsNullOrEmpty(MySqlConfig.Database))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_DbNameEmpty);
                        });
                    }

                    // 查询数据库是否存在
                    string query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName";
                    using (var command = new MySqlCommand(query, mySqlConnection))
                    {
                        command.Parameters.AddWithValue("@dbName", MySqlConfig.Database);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                log.Info("Database exists.");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_ConnectSuccess);
                                });
                            }
                            else
                            {
                                log.Warn("Database does not exist.");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_DbNotExist);
                                });
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (ex.Number)
                    {
                        case 1045:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_AuthError);
                            break;
                        case 1049:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_SpecifiedDbNotExist);
                            break;
                        case 2003:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_ConnectFailed);
                            break;
                        default:
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库连接失败，错误码：{ex.Number}");
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_UnknownError);
                });
            }
        }

        /// <summary>
        /// Executes a SQL batch and returns the affected-row count only after commit succeeds.
        /// MySQL statements that implicitly commit, including DDL, are not made rollback-safe by this transaction wrapper.
        /// A disposal failure after commit is logged as a cleanup warning and does not change the committed success result.
        /// </summary>
        public static int BatchExecuteNonQuery(string sqlBatch)
        {
            return BatchExecuteNonQuery(sqlBatch, static () => new SqlSugarBatchSqlExecutor());
        }

        internal static int BatchExecuteNonQuery(string sqlBatch, Func<IBatchSqlExecutor> executorFactory)
        {
            int affectedRows = BatchExecuteNonQuery(sqlBatch, executorFactory, out BatchCommittedCleanupWarning? cleanupWarning);
            if (cleanupWarning != null)
            {
                LogBatchCleanupWarning(cleanupWarning);
            }

            LogBatchSuccess(affectedRows);
            return affectedRows;
        }

        internal static int BatchExecuteNonQuery(
            string sqlBatch,
            Func<IBatchSqlExecutor> executorFactory,
            out BatchCommittedCleanupWarning? cleanupWarning)
        {
            ArgumentNullException.ThrowIfNull(sqlBatch);
            ArgumentNullException.ThrowIfNull(executorFactory);
            cleanupWarning = null;

            IBatchSqlExecutor executor;
            try
            {
                executor = executorFactory() ?? throw new InvalidOperationException("The batch SQL executor factory returned null.");
            }
            catch (Exception ex)
            {
                var createException = new BatchExecuteNonQueryException(BatchExecutionStage.CreateExecutor, null, ex, null);
                LogBatchFailure(createException);
                throw createException;
            }

            int affectedRows = 0;
            BatchExecuteNonQueryException? primaryException = null;
            try
            {
                affectedRows = ExecuteBatch(sqlBatch, executor);
            }
            catch (BatchExecuteNonQueryException ex)
            {
                primaryException = ex;
            }

            Exception? disposeException = null;
            try
            {
                executor.Dispose();
            }
            catch (Exception ex)
            {
                disposeException = ex;
            }

            if (primaryException != null)
            {
                if (disposeException != null)
                {
                    primaryException.RecordDisposeFailure(disposeException);
                    LogBatchFailure(primaryException);
                }

                ExceptionDispatchInfo.Capture(primaryException).Throw();
            }

            if (disposeException != null)
                cleanupWarning = new BatchCommittedCleanupWarning(disposeException);

            return affectedRows;
        }

        private static int ExecuteBatch(string sqlBatch, IBatchSqlExecutor executor)
        {
            int totalCount = 0;
            int executedStatementCount = 0;
            int? statementIndex = null;
            BatchExecutionStage stage = BatchExecutionStage.PrepareBatch;
            try
            {
                var statements = sqlBatch.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                stage = BatchExecutionStage.BeginTransaction;
                executor.BeginTransaction();
                foreach (var sql in statements)
                {
                    var trimmedSql = sql.Trim();
                    if (string.IsNullOrEmpty(trimmedSql))
                        continue;

                    stage = BatchExecutionStage.ExecuteStatement;
                    statementIndex = executedStatementCount + 1;
                    int count = executor.ExecuteNonQuery(trimmedSql);
                    totalCount += count;
                    executedStatementCount++;
                }

                stage = BatchExecutionStage.CommitTransaction;
                statementIndex = null;
                executor.CommitTransaction();
            }
            catch (Exception ex)
            {
                Exception? rollbackException = null;
                if (stage != BatchExecutionStage.PrepareBatch)
                {
                    try
                    {
                        executor.RollbackTransaction();
                    }
                    catch (Exception rollbackEx)
                    {
                        rollbackException = rollbackEx;
                    }
                }

                var batchException = new BatchExecuteNonQueryException(
                    stage,
                    statementIndex,
                    ex,
                    rollbackException);
                LogBatchFailure(batchException);
                throw batchException;
            }

            return totalCount;
        }

        private static void LogBatchFailure(BatchExecuteNonQueryException exception)
        {
            try
            {
                log.Error("SQL批量执行失败。" + exception.GetDiagnosticSummary());
            }
            catch
            {
                // Logging must never replace the transaction failure contract.
            }
        }

        private static void LogBatchCleanupWarning(BatchCommittedCleanupWarning warning)
        {
            try
            {
                log.Warn("SQL批量执行已提交，但执行器资源释放失败；提交结果保持成功。" + warning.GetDiagnosticSummary());
            }
            catch
            {
                // Logging must never turn a committed transaction into a reported failure.
            }
        }

        private static void LogBatchSuccess(int affectedRows)
        {
            try
            {
                log.Info($"总共受影响的行数: {affectedRows}");
            }
            catch
            {
                // Logging must never turn a committed transaction into a reported failure.
            }
        }



        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
