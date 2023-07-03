using ColorVision.MQTT;
using log4net;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Relational;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace ColorVision.MySql
{

    public class BaseService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseService));

        public MySqlControl MySqlControl { get; set; }
        public string TableName { get; set; }

        public BaseService(string tableName)
        {
            MySqlControl = MySqlControl.GetInstance();
            TableName = tableName;
        }

        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new MySqlCommand(sql, MySqlControl.MySqlConnection);
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
                MySqlCommand command = new MySqlCommand(sql, MySqlControl.MySqlConnection);
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

        public DataTable GetData(string sql, Dictionary<string, object> param)
        {
            DataTable dt = new DataTable();
            try
            {
                if (param.Count ==0)
                {
                    using MySqlDataAdapter adapter = new MySqlDataAdapter(sql, MySqlControl.MySqlConnection);
                    int count = adapter.Fill(dt);
                }
                else
                {
                    MySqlCommand command = new MySqlCommand(sql, MySqlControl.MySqlConnection);
                    foreach (var item in param)
                    {
                        command.Parameters.AddWithValue(item.Key, item.Value);
                    }
                    using MySqlDataAdapter adapter = new MySqlDataAdapter(command);
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
            try
            {
                MySqlCommand command = new MySqlCommand($"select * from {TableName}", MySqlControl.MySqlConnection);

                using MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                DataTable olddt = new DataTable();
                adapter.Fill(olddt);

                MySqlCommandBuilder cmdb = new MySqlCommandBuilder(adapter);
                adapter.UpdateCommand = cmdb.GetUpdateCommand();
                adapter.InsertCommand = cmdb.GetInsertCommand();
                adapter.DeleteCommand = cmdb.GetDeleteCommand();
                olddt.Rows[0]["type"] = 1;
                count = adapter.Update(dt);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }



    }

    public class BaseServiceDetail<T> : BaseService
    {
        public BaseServiceDetail(string tableName) : base(tableName)
        {

        }

        public T? GetByID(int id)
        {
            string sql = $"select * from {TableName} where is_delete=0 and id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModel(d_info.Rows[0]) : default;
        }

        public List<T> GetByPID(int pid)
        {
            string sql = $"select * from {TableName} where is_delete=0 and pid=@pid";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "pid", pid }
            };
            DataTable d_info = GetData(sql, param);


            List<T> list = new List<T>();
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public int SavePID(int Pid,List<T> datas)
        {
            DataTable d_info = new DataTable(TableName);
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                d_info.Rows.Add(row);
            }

            return Save(d_info);
        }


        public int DeleteByPId(int pid)
        {
            string sql = $"update {TableName} set is_delete=1 where pid=@pid";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "pid", pid }
            };
            return ExecuteNonQuery(sql, param);
        }





        public int DeleteById(int id)
        {
            string sql = $"update {TableName} set is_delete=1 where id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            return ExecuteNonQuery(sql, param);
        }


        public virtual T? GetModel(DataRow item)
        {
            return default;
        }

        public virtual DataRow GetRow(T item, DataTable dataTable)
        {
            return dataTable.NewRow();
        }


    }




    public class BaseServiceMaster<T>: BaseService
    {

        public BaseServiceMaster(string tableName):base(tableName)
        {

        }

        public T? GetByID(int id)
        {
            string sql = $"select * from {TableName} where is_delete=0 and id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModel(d_info.Rows[0]) : default;
        }


        public int Save(T t)
        {
            DataTable d_info = new DataTable(TableName);
            DataRow row = GetRow(t, d_info);
            d_info.Rows.Add(row);
            return Save(d_info);
        }


        public int Save(List<T> datas)
        {
            //DeleteAll();
            DataTable d_info = GetDataTable();
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                d_info.Rows.Add(row);
                row.AcceptChanges();
                row.SetModified();
            }

            return Save(d_info);
        }



        public DataTable GetTableAll()
        {
            string sql = $"select * from {TableName} where is_delete=0";
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



        public virtual T? GetModel(DataRow item) => default;

        public virtual DataRow GetRow(T item, DataTable dataTable) => dataTable.NewRow();

        public virtual DataTable GetDataTable(string? tableName =null) => new DataTable(tableName);


        public int DeleteAll()
        {
            string sql = $"update {TableName} set is_delete=1";
            return ExecuteNonQuery(sql);
        }

        public int DeleteById(int id)
        {
            string sql = $"update {TableName} set is_delete=1 where id=@id";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            return ExecuteNonQuery(sql, param);
        }
    }
}
