using log4net;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Database
{
    /// <summary>
    /// 因为项目中本身包含Service,所以这里取消Service层的设置，直接从Dao层
    /// </summary>
    public class BaseDao
    {  
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));

        public string TableName { get { return _TableName; } set { _TableName = value; } }
        private string _TableName;

        public BaseDao(string tableName, string pkField)
        {
            _TableName = tableName;
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
    }
}
