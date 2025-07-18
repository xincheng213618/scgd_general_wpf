using log4net;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.MySql.ORM
{
    /// <summary>
    /// 因为项目中本身包含Service,所以这里取消Service层的设置，直接从Dao层
    /// </summary>
    public class BaseDao
    {  
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));

        public string TableName { get { return _TableName; } set { _TableName = value; } }
        private string _TableName;

        public string PKField { get { return _PKField; } set { _PKField = value; } }
        private string _PKField;

        public BaseDao(string tableName, string pkField)
        {
            _TableName = tableName;
            _PKField = pkField;
        }

        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                var conn = MySqlControl.GetInstance().MySqlConnection;
                using MySqlCommand command = new(sql, conn);
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }
        public virtual MySqlConnection CreateConnection()
        {
            const int maxRetry = 2;
            int attempt = 0;
            Exception? lastException = null;
            while (attempt < maxRetry)
            {
                try
                {
                    var conn = new MySqlConnection(MySqlControl.GetConnectionString());
                    conn.Open();
                    return conn;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    log.Warn($"数据库连接第{attempt + 1}次失败: {ex.Message}");
                    attempt++;
                    if (attempt < maxRetry)
                    {
                        System.Threading.Thread.Sleep(100); // 间隔100ms再试
                    }
                }
            }
            log.Error("数据库连接重试2次均失败", lastException);
            return null;
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object>? param)
        {
            int count = -1;
            try
            {
                var conn = MySqlControl.GetInstance().MySqlConnection;
                using MySqlCommand command = new MySqlCommand(sql, conn);
                if (param != null)
                {
                    foreach (var item in param)
                    {
                        command.Parameters.AddWithValue(item.Key, item.Value);
                    }
                }
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error($"SQL: {sql}, Params: {(param == null ? "null" : JsonConvert.SerializeObject(param))}, Ex: {ex}");
            }
            return count;
        }

        public DataTable GetData(string sql) => GetData(sql, new Dictionary<string, object>());
        private static readonly object _dbLock = new object();

        //https://stackoverflow.com/questions/5440168/exception-there-is-already-an-open-datareader-associated-with-this-connection-w
        public DataTable GetData(string sql, Dictionary<string, object>? param)
        {
            DataTable dt = new();
            lock (_dbLock)
            {
                try
                {
                    if (param == null || param.Count == 0)
                    {
                        var conn = MySqlControl.GetInstance().MySqlConnection;
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(sql, conn))
                        {
                            int count = adapter.Fill(dt);
                        }
                    }
                    else
                    {
                        var conn = MySqlControl.GetInstance().MySqlConnection;
                        using (MySqlCommand command = new(sql, conn))
                        {
                            foreach (var item in param)
                            {
                                command.Parameters.AddWithValue(item.Key, item.Value);
                            }
                            using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                            {
                                int count = adapter.Fill(dt);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"sql{sql} +{ex}");
                }
            }

            return dt;
        }

        public int Save(DataTable dt)
        {
            int count = -1;
            string sqlStr = string.Format("SELECT * FROM {0} WHERE FALSE", TableName);
            try
            {
                var conn = MySqlControl.GetInstance().MySqlConnection;
                using (MySqlCommand cmd = new MySqlCommand(sqlStr, conn))
                {
                    using (MySqlDataAdapter dataAdapter = new MySqlDataAdapter(cmd))
                    {
                        dataAdapter.RowUpdated += DataAdapter_RowUpdated;
                        using (MySqlCommandBuilder builder = new(dataAdapter))
                        {
                            builder.ConflictOption = ConflictOption.OverwriteChanges;
                            builder.SetAllValues = true;
                            dataAdapter.UpdateCommand = builder.GetUpdateCommand(true) as MySqlCommand;
                            count = dataAdapter.Update(dt);
                            dt.AcceptChanges();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }

        private void DataAdapter_RowUpdated(object sender, MySqlRowUpdatedEventArgs e)
        {
            if (e.Row[_PKField] == DBNull.Value)
            {
                e.Row[_PKField] = e.Command.LastInsertedId;
            }
        }


        public int DeleteAllByParam(Dictionary<string, object> param, bool IsLogicDel = true)
        {
            if (param == null || param.Count == 0)
            {
                throw new ArgumentException("Parameter dictionary cannot be null or empty", nameof(param));
            }

            // Build the WHERE clause from the parameters
            var whereClauses = param.Select(kvp => $"{kvp.Key} = @{kvp.Key}");
            string whereClause = string.Join(" AND ", whereClauses);

            string sql = IsLogicDel
                ? $"UPDATE {TableName} SET is_delete = 1 WHERE {whereClause}"
                : $"DELETE FROM {TableName} WHERE {whereClause}";

            return ExecuteNonQuery(sql, param);
        }
    }
}
