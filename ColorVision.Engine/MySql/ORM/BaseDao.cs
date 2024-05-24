using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.MySql.ORM
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

        public BaseDao(string tableName, string pkField)
        {
            MySqlControl = MySqlControl.GetInstance();
            _TableName = tableName;
            _PKField = pkField;
        }

        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new(sql, MySqlControl.MySqlConnection);
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
                MySqlCommand command = new(sql, MySqlControl.MySqlConnection);
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
                    using MySqlDataAdapter adapter = new(sql, MySqlControl.MySqlConnection);
                    int count = adapter.Fill(dt);
                }
                else
                {
                    MySqlCommand command = new(sql, MySqlControl.MySqlConnection);
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
                log.Error(ex);
            }
            return dt;
        }

        public int Save(DataTable dt)
        {
            int count = -1;
            string sqlStr = string.Format("SELECT * FROM {0} WHERE FALSE", TableName);
            try
            {
                using (MySqlCommand cmd = new(sqlStr, MySqlControl.MySqlConnection))
                {
                    MySqlDataAdapter dataAdapter = new(cmd);
                    dataAdapter.RowUpdated += DataAdapter_RowUpdated;
                    MySqlCommandBuilder builder = new(dataAdapter);
                    builder.ConflictOption = ConflictOption.OverwriteChanges;
                    builder.SetAllValues = true;
                    dataAdapter.UpdateCommand = builder.GetUpdateCommand(true) as MySqlCommand;
                    count = dataAdapter.Update(dt);

                    dt.AcceptChanges();
                    dataAdapter.Dispose();
                    builder.Dispose();
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
    }
}
