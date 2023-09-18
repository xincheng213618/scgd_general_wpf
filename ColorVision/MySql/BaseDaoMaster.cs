using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql
{

    public class BaseDao
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));

        public MySqlControl MySqlControl { get; set; }
        public string TableName { get { return _TableName; } set { _TableName = value; } }
        private string _TableName;
        public string PKField { get { return _PKField; } set { _PKField = value; } }
        private string _PKField;
        public bool IsLogicDel { get { return _IsLogicDel; } set { _IsLogicDel = value; } }
        private bool _IsLogicDel;

        public BaseDao(string tableName, string pkField, bool isLogicDel)
        {
            this.MySqlControl = MySqlControl.GetInstance();
            this._TableName = tableName;
            this._PKField = pkField;
            this._IsLogicDel = isLogicDel;
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
            string sqlStr = string.Format("SELECT * FROM {0} WHERE FALSE", TableName);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sqlStr, MySqlControl.MySqlConnection))
                {
                    MySqlDataAdapter dataAdapter = new MySqlDataAdapter(cmd);
                    dataAdapter.RowUpdated += DataAdapter_RowUpdated;
                    MySqlCommandBuilder builder = new MySqlCommandBuilder(dataAdapter);
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

        protected string GetDelSQL(bool hasAnd)
        {
            string andSQL = " ";
            string isDelSQL = " ";
            if (_IsLogicDel)
            {
                if (hasAnd) andSQL = " and ";
                isDelSQL = "is_delete=0";
            }
            return andSQL + isDelSQL;
        }

        private void DataAdapter_RowUpdated(object sender, MySqlRowUpdatedEventArgs e)
        {
            if(e.Row[_PKField] == DBNull.Value)
            {
                e.Row[_PKField] = e.Command.LastInsertedId;
            }
        }

        public DataTable selectById(int id)
        {
            string sql = $"select * from {TableName} where id=@id" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id",  id}
            };
            DataTable d_info = GetData(sql, param);
            return d_info;
        }

        public static DataRow? selectRow(int id, DataTable dInfo)
        {
            DataRow[] rows = dInfo.Select($"id={id}");

            if (rows.Length == 1) return rows[0];
            else return null;
        }
    }

    public class BaseDaoMaster<T>: BaseDao where T : IPKModel
    {
        protected string ViewName { get; set; }
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDaoMaster<T>));
        public BaseDaoMaster(string viewName, string tableName, string pkField, bool isLogicDel) :base(tableName, pkField, isLogicDel)
        {
            ViewName = viewName;
        }

        public virtual DataRow Model2Row(T item, DataRow row)
        {
            return row;
        }

        public virtual DataTable CreateColumns(DataTable dInfo) => dInfo;

        public virtual int Save(T item)
        {
            DataTable d_info = selectById(item.GetPK());
            ConvertRow(item, d_info);
            int ret = Save(d_info);
            item.SetPK(d_info.Rows[0].Field<int>(PKField));
            return ret;
        }

        public void ConvertRow(T item, DataTable dataTable)
        {
            DataRow row = GetRow(item, dataTable);
            Model2Row(item, row);
        }

        public virtual int Save(List<T> datas,int tenantId)
        {
            DeleteAll(tenantId);
            DataTable d_info = GetDataTable();
            //CreateColumns(d_info);
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                //d_info.Rows.Add(row);
                Model2Row(item, row);
            }
            return Save(d_info);
        }
        public int UpdateByPid(int pid, List<T> datas)
        {
            DataTable d_info = GetUpdateTableAllByPid(pid);
            d_info.TableName = TableName;
            //CreateColumns(d_info);
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                //d_info.AcceptChanges();
                Model2Row(item, row);
            }
            return Save(d_info);
        }

        public int SaveByPid(int pid,List<T> datas)
        {
            DeleteAllByPid(pid);
            DataTable d_info = GetDataTable();
            //CreateColumns(d_info);
            foreach (var item in datas)
            {
                //DataRow row = GetRow(item, d_info);
                DataRow row = d_info.NewRow();
                d_info.Rows.Add(row);
                Model2Row(item, row);
                row[PKField] = DBNull.Value;
            }
            return BulkInsertAsync(d_info);
        }

        public int BulkInsertAsync(DataTable dataTable)
        {
            int count = -1;
            MySqlConnector.MySqlConnection connection = new MySqlConnector.MySqlConnection(MySqlControl.GetInstance().GetCurConnectionString()+ ";SslMode = none;AllowLoadLocalInfile=True");
            dataTable.TableName = TableName;
            using (connection)
            {
                var bulkCopy = new MySqlConnector.MySqlBulkCopy(connection);
                bulkCopy.DestinationTableName = dataTable.TableName;
                bulkCopy.ColumnMappings.AddRange(GetMySqlColumnMapping(dataTable));
                try
                {

                    MySqlConnector.MySqlBulkCopyResult result = bulkCopy.WriteToServer(dataTable);
                    count = result.RowsInserted;
                    //check for problems
                    //if (result.Warnings.Count != 0)
                    //{
                    //    /* handle potential data loss warnings */
                    //}
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            return count;
        }

        private static List<MySqlConnector.MySqlBulkCopyColumnMapping> GetMySqlColumnMapping(DataTable dataTable)
        {
            List<MySqlConnector.MySqlBulkCopyColumnMapping> colMappings = new List<MySqlConnector.MySqlBulkCopyColumnMapping>();
            int i = 0;
            foreach (DataColumn col in dataTable.Columns)
            {
                colMappings.Add(new MySqlConnector.MySqlBulkCopyColumnMapping(i, col.ColumnName));
                i++;
            }
            return colMappings;
        }

        protected string GetTableName()
        {
            if (string.IsNullOrEmpty(ViewName)) return TableName;
            else return ViewName;
        }

        public T? GetByID(int id)
        {
            string sql = $"select * from {GetTableName()} where id=@id" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModel(d_info.Rows[0]) : default;
        }

        public virtual DataTable GetTableAllByTenantId(int tenantId)
        {
            string sql = $"select * from {GetTableName()} where tenant_id={tenantId}" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public virtual DataTable GetTablePidIsNullByTenantId(int tenantId)
        {
            string sql = $"select * from {GetTableName()} where tenant_id={tenantId} and ( pid is null or pid=-1)" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public virtual DataTable GetTablePidIsNotNullByTenantId(int tenantId)
        {
            string sql = $"select * from {GetTableName()} where tenant_id={tenantId} and pid > 0" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public virtual DataTable GetTableAllByPid(int pid)
        {
            string sql = $"select * from {GetTableName()} where pid={pid}" + GetDelSQL(true) + $" order by {PKField}";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public virtual DataTable GetTableAllByBatchid(int Batchid)
        {
            string sql = $"select * from {GetTableName()} where batch_id={Batchid}" + GetDelSQL(true) + $" order by {PKField}";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public virtual DataTable GetUpdateTableAllByPid(int pid)
        {
            string sql = $"select * from {TableName} where pid={pid}" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public DataTable GetTableAllByPcode(string pcode)
        {
            string sql = $"select * from {GetTableName()} where pcode='{pcode}'" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public List<T> GetAll(int tenantId)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTableAllByTenantId(tenantId);
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

        public List<T> GetPidIsNotNull(int tenantId)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTablePidIsNotNullByTenantId(tenantId);
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

        public List<T> GetPidIsNull(int tenantId)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTablePidIsNullByTenantId(tenantId);
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

        public List<T> GetAllByPid(int pid)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTableAllByPid(pid);
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

        public List<T> GetAllByBatchid(int pid)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTableAllByBatchid(pid);
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


        public List<T> GetAllByPcode(string pcode)
        {
            List<T> list = new List<T>();
            DataTable d_info = GetTableAllByPcode(pcode);
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

        public virtual T? GetModel(DataRow item) => default;

        public DataRow GetRow(T item, DataTable dataTable)
        {
            DataRow row = selectRow(item.GetPK(), dataTable);
            if (row == null)
            {
                row = dataTable.NewRow();
                dataTable.Rows.Add(row);
            }
            else
            {

            }
            return row;
        }
        public virtual DataTable GetDataTable(string? tableName = null)
        {
            DataTable d_info = new DataTable(tableName);
            CreateColumns(d_info);
            return d_info;
        }
        public int DeleteAll(int tenantId)
        {
            string sql = $"update {TableName} set is_delete=1 where tenant_id={tenantId}";
            if (!IsLogicDel)
            {
                sql = $"delete from {TableName} where tenant_id={tenantId}";
            }
            return ExecuteNonQuery(sql);
        }
        public int DeleteAllByPid(int pid)
        {
            string sql = $"update {TableName} set is_delete=1 where pid={pid}";
            if (!IsLogicDel)
            {
                sql = $"delete from {TableName} where pid={pid}";
            }
            return ExecuteNonQuery(sql);
        }
        public int DeleteById(int id)
        {
            string sql = $"update {TableName} set is_delete=1 where id=@id";
            if (!IsLogicDel)
            {
                sql = $"delete from {TableName} where id=@id";
            }
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id", id }
            };
            return ExecuteNonQuery(sql, param);
        }
    }
}
