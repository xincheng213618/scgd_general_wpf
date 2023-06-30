using ColorVision.MQTT;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    internal class BaseService<T>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseService));

        protected string _tableName;
        protected MySqlConnection connection;

        public BaseService(string tableName)
        {
            MySqlControl control = MySqlControl.GetInstance();
            connection = control.MySqlConnection;
            _tableName = tableName;
        }

        public int Save(List<T> datas)
        {
            DataTable d_info = new DataTable(_tableName);
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                d_info.Rows.Add(row);
            }

            return Save(d_info);
        }

        protected virtual DataRow GetRow(T item, DataTable d_info)
        {
            return d_info.NewRow();
        }
        public T? GetById(int id)
        {
            T? model = default(T);
            string sql = $"select * from {_tableName} where is_delete=0 and id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>();
            param.Add("id", id);
            DataTable d_info = GetData(sql, param);
            if (d_info.Rows.Count == 1)
            {
                model = GetModel(d_info.Rows[0]);
            }

            return model;
        }

        public DataTable GetTableAll()
        {
            string sql = $"select * from {_tableName} where is_delete=0";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public List<T> GetAll()
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTableAll();
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModel(item);
                if(model != null)
                {
                    list.Add(model);
                }
            }

            return list;
        }
        protected virtual T? GetModel(DataRow item)
        {
            return default(T);
        }
        public DataTable GetData(string sql)
        {
            DataTable dt = new DataTable();
            try
            {
                MySqlDataAdapter adapter = new MySqlDataAdapter(sql, connection);
                int count = adapter.Fill(dt);
            } catch (Exception ex) {
                log.Error(ex);
            }

            return dt;
        }

        public DataTable GetData(string sql, Dictionary<string, object> param)
        {
            DataTable dt = new DataTable();
            try
            {
                MySqlCommand command = new MySqlCommand(sql, connection);
                foreach (var item in param)
                {
                    command.Parameters.AddWithValue(item.Key, item.Value);
                }
                MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                int count = adapter.Fill(dt);
            }
            catch (Exception ex) { }
            return dt;
        }

        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new MySqlCommand(sql, connection);
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex) { }
            return count;
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> param)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new MySqlCommand(sql, connection);
                foreach (var item in param)
                {
                    command.Parameters.AddWithValue(item.Key, item.Value);
                }
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex) { }
            return count;
        }

        public int Save(DataTable dt)
        {
            int count = -1;
            try
            {
                MySqlDataAdapter adapter = new MySqlDataAdapter("", connection);
                count = adapter.Update(dt);
            }
            catch (Exception ex) { }

            return count;
        }

        public int DeleteAll()
        {
            string sql = $"update {_tableName} set is_delete=1";
            return ExecuteNonQuery(sql);
        }

        public int DeleteById(int id)
        {
            string sql = $"update {_tableName} set is_delete=1 where id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>();
            param.Add("id", id);
            return ExecuteNonQuery(sql, param);
        }
    }
}
