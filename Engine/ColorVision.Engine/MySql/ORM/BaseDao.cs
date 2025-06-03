using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

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
                using var conn = CreateConnection();
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
            // 假设 MySqlControl.ConnectionString 有连接串
            var conn = new MySqlConnection(MySqlControl.GetConnectionString());
            conn.Open();
            return conn;
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> param)
        {
            int count = -1;
            try
            {
                using var conn = CreateConnection();
                using MySqlCommand command = new MySqlCommand(sql, conn);
                foreach (var item in param)
                {
                    command.Parameters.AddWithValue(item.Key, item.Value);
                }
                count = command.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }

        public DataTable GetData(string sql) => GetData(sql, new Dictionary<string, object>());

        public DataTable GetData(string sql, Dictionary<string, object>? param)
        {
            DataTable dt = new();
            try
            {
                if (param == null || param.Count == 0)
                {
                    using var conn = CreateConnection();
                    using MySqlDataAdapter adapter = new MySqlDataAdapter(sql, conn);
                    int count = adapter.Fill(dt);
                }
                else
                {
                    using var conn = CreateConnection();
                    using MySqlCommand command = new(sql, conn);
                    foreach (var item in param)
                    {
                        command.Parameters.AddWithValue(item.Key, item.Value);
                    }
                    using MySqlDataAdapter adapter = new(command);
                    int count = adapter.Fill(dt);
                }  
            }
            catch (Exception ex)
            {
                log.Error($"sql{sql} +{ex}");
            }
            return dt;
        }

        public int Save(DataTable dt)
        {
            int count = -1;
            string sqlStr = string.Format("SELECT * FROM {0} WHERE FALSE", TableName);
            try
            {
                using var conn = CreateConnection();
                using MySqlCommand cmd = new MySqlCommand(sqlStr, conn);
                using MySqlDataAdapter dataAdapter = new MySqlDataAdapter(cmd);
                dataAdapter.RowUpdated += DataAdapter_RowUpdated;
                using MySqlCommandBuilder builder = new(dataAdapter);
                builder.ConflictOption = ConflictOption.OverwriteChanges;
                builder.SetAllValues = true;
                dataAdapter.UpdateCommand = builder.GetUpdateCommand(true) as MySqlCommand;
                count = dataAdapter.Update(dt);
                dt.AcceptChanges();

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
