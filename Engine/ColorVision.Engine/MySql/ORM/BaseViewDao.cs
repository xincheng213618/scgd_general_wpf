using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.MySql.ORM
{

    public class BaseViewDao<T> : BaseDao1 where T : IPKModel, new()
    {
        public string ViewName { get; set; }
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseViewDao<T>));
        public BaseViewDao(string viewName, string tableName, string pkField, bool isLogicDel) : base(tableName, pkField, isLogicDel)
        {
            ViewName = viewName;
        }

        public virtual T? GetModelFromDataRow(DataRow item) => ReflectionHelper.GetModelFromDataRow<T>(item);
        public virtual DataRow Model2Row(T item, DataRow row) => ReflectionHelper.Model2RowAuto(item, row);
        public virtual DataTable CreateColumns(DataTable dataTable) => ReflectionHelper.CreateColumns<T>(dataTable);


        public virtual int Save(T item)
        {
            DataTable d_info = SelectById(item.Id);
            ConvertRow(item, d_info);
            int ret = Save(d_info);
            item.Id = d_info.Rows[0].Field<int>(PKField);
            return ret;
        }

        public void ConvertRow(T item, DataTable dataTable)
        {
            DataRow row = GetRow(item, dataTable);
            Model2Row(item, row);
        }


        public int UpdateByPid(int pid, List<T> datas)
        {
            string sql = $"select * from {TableName} where pid={pid}" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            d_info.TableName = TableName;
            foreach (var item in datas)
            {
                DataRow row = GetRow(item, d_info);
                Model2Row(item, row);
            }
            return Save(d_info);
        }

        public int SaveByPid(int pid, List<T> datas)
        {
            DeleteAllByPid(pid,false);
            DataTable d_info = GetDataTable();
            foreach (var item in datas)
            {
                DataRow row = d_info.NewRow();
                d_info.Rows.Add(row);
                Model2Row(item, row);
                if (item.Id<=0)
                    row[PKField] = DBNull.Value;
            }
            return BulkInsertAsync(d_info);
        }

        //如果检索代码看到了这里，应该是数据库的local_infile没有启用，这里设置即可  SET GLOBAL local_infile=1;
        public int BulkInsertAsync(DataTable dataTable)
        {
            int count = -1;
            MySqlConnector.MySqlConnection connection = new(MySqlControl.GetConnectionString() + ";SslMode = none;AllowLoadLocalInfile=True");
            dataTable.TableName = TableName;
            using (connection)
            {
                var bulkCopy = new MySqlConnector.MySqlBulkCopy(connection)
                {
                    DestinationTableName = dataTable.TableName
                };
                bulkCopy.ColumnMappings.AddRange(GetMySqlColumnMapping(dataTable));
                try
                {

                    MySqlConnector.MySqlBulkCopyResult result = bulkCopy.WriteToServer(dataTable);
                    count = result.RowsInserted;
                    //check for problems
                    //if (result.Warnings.Count != 0)
                    //{
                    //    /* handle potential Data loss warnings */
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
            List<MySqlConnector.MySqlBulkCopyColumnMapping> colMappings = new();
            int i = 0;
            foreach (DataColumn col in dataTable.Columns)
            {
                colMappings.Add(new MySqlConnector.MySqlBulkCopyColumnMapping(i, col.ColumnName));
                i++;
            }
            return colMappings;
        }


        protected string GetTableName() => string.IsNullOrWhiteSpace(ViewName) ? TableName : ViewName;

        public T? GetById(int id) => GetByParam(new Dictionary<string, object> { { "id", id } });

        public T? GetByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {GetTableName()} {whereClause}";

            DataTable dataTable = GetData(sql, param);
            if (dataTable.Rows.Count == 1)
            {
                return GetModelFromDataRow(dataTable.Rows[0]);
            }
            else if (dataTable.Rows.Count >= 1)
            {
                return GetModelFromDataRow(dataTable.Rows[0]);
            }
            return default;
        }

        public virtual DataTable GetTableAllByTenantId(int tenantId)
        {
            string sql = $"select * from {GetTableName()} where tenant_id={tenantId}" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public List<T> GetAll() => GetAllByParam(new Dictionary<string, object>());

        public List<T> GetAllByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {GetTableName()} {whereClause}";
            DataTable d_info = GetData(sql, param);

            List<T> list = new();
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public List<T> ConditionalQuery(Dictionary<string, object> param)
        {
            List<T> list = new();
            string sql = $"select * from {GetTableName()} where 1=1";
            // 遍历字典，为每个键值对构建查询条件
            foreach (var pair in param)
            {
                // 这假设字典的键是数据库列的名称
                // 并且值是你想要匹配的模式
                if (pair.Value != null && !string.IsNullOrEmpty(pair.Value.ToString()))
                {
                    // 对于安全起见，应该使用参数化查询来避免SQL注入

                    if (pair.Key.StartsWith(">", StringComparison.CurrentCulture))
                    {
                        sql += $" AND `{pair.Key[1..]}` > '{pair.Value.ToString()}'";
                    }
                    else if (pair.Key.StartsWith("<", StringComparison.CurrentCulture))
                    {
                        sql += $" AND `{pair.Key.Substring(1)}` < '{pair.Value.ToString()}'";
                    }
                    else
                    {
                        sql += $" AND `{pair.Key}` LIKE '%{pair.Value}%'";
                    }


                }
            }

            DataTable d_info = GetData(sql, param);
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


        public List<T> GetAll(int tenantId)
        {
            List<T> list = new();
            DataTable d_info = GetTableAllByTenantId(tenantId);
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


        public List<T> GetAllByPid(int pid)
        {
            List<T> list = new();
            string sql = $"select * from {GetTableName()} where pid={pid}" + GetDelSQL(true) + $" order by {PKField}";
            DataTable d_info = GetData(sql);
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }




        public List<T> GetAllByBatchid(int pid)
        {
            List<T> list = new();
            string sql = $"select * from {GetTableName()} where batch_id={pid}" + GetDelSQL(true) + $" order by {PKField}";
            DataTable d_info = GetData(sql);

            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public List<T> GetAllByBatchCode(string code)
        {
            List<T> list = new();
            DataTable d_info = GetTableAllByBatchCode(code);
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public virtual DataTable GetTableAllByBatchCode(string bcode)
        {
            string sql = $"select * from {GetTableName()} where batch_code='{bcode}'" + GetDelSQL(true) + $" order by {PKField}";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public List<T> GetAllByPcode(string pcode)
        {
            List<T> list = new();
            string sql = $"select * from {GetTableName()} where pcode='{pcode}'" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            foreach (var item in d_info.AsEnumerable())
            {
                T? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


        public DataRow GetRow(T item, DataTable dataTable)
        {
            DataRow row = dataTable.SelectRow(item.Id);
            if (row == null)
            {
                row = dataTable.NewRow();
                dataTable.Rows.Add(row);
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
            string sql = IsLogicDel ? $"UPDATE {TableName} SET is_delete = 1 WHERE tenant_id = @tenant_id" : $"DELETE FROM {TableName} WHERE tenant_id = @tenant_id";
            return ExecuteNonQuery(sql, new Dictionary<string, object> { { "tenant_id", tenantId } });
        }
        public int DeleteAllByPid(int pid, bool IsLogicDel = true)
        {
            string sql = IsLogicDel ? $"UPDATE {TableName} SET is_delete = 1 WHERE pid = @pid" : $"DELETE FROM {TableName} WHERE pid = @pid";
            return ExecuteNonQuery(sql, new Dictionary<string, object> { { "pid", pid } });
        }
        public int DeleteById(int id, bool IsLogicDel = true)
        {
            string sql = IsLogicDel ? $"UPDATE {TableName} SET is_delete = 1 WHERE id = @id" : $"DELETE FROM {TableName} WHERE id = @id";
            return ExecuteNonQuery(sql, new Dictionary<string, object> { { "id", id } });
        }
    }
}
