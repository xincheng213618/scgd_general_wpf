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

        public MySqlControl MySqlControl { get; set; }
        public string TableName { get { return _TableName; } set { _TableName = value; } }
        private string _TableName;
        public string PKField { get { return _PKField; } set { _PKField = value; } }
        private string _PKField;
        public MySqlConnection MySqlConnection { get; set; } 

        public BaseDao(string tableName, string pkField)
        {
            MySqlControl = MySqlControl.GetInstance();
            MySqlControl.MySqlConnectChanged += (s, e) => MySqlConnection = MySqlControl.MySqlConnection;
            MySqlConnection = MySqlControl.MySqlConnection;

            _TableName = tableName;
            _PKField = pkField;
        }

        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new(sql, MySqlConnection);
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> param)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new MySqlCommand(sql, MySqlConnection);
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
            if (!MySqlControl.IsConnect) return dt;
            try
            {
                if (param == null || param.Count == 0)
                {
                    using MySqlDataAdapter adapter = new MySqlDataAdapter(sql, MySqlConnection);
                    int count = adapter.Fill(dt);
                }
                else
                {
                    MySqlCommand command = new(sql, MySqlConnection);
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
                using MySqlCommand cmd = new MySqlCommand(sqlStr, MySqlConnection);
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
