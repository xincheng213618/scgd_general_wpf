using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.MySql.ORM
{
    public class BaseTableDao<T> : BaseDao where T : IPKModel ,new()
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDao<T>));

        public BaseTableDao(string tableName, string pkField ="id") : base(tableName, pkField)
        {

        }

        public virtual T? GetModelFromDataRow(DataRow item) => ReflectionHelper.GetModelFromDataRow<T>(item);
        public virtual DataRow Model2Row(T item, DataRow row) => ReflectionHelper.Model2RowAuto(item, row);
        public virtual DataTable CreateColumns(DataTable dataTable) => ReflectionHelper.CreateColumns<T>(dataTable);

        public DataTable SelectById(int id)
        {
            string sql = $"select * from {TableName} where id=@id";
            return GetData(sql, new Dictionary<string, object> { { "id", id } });
        }

        public virtual int Save(T item)
        {
            DataTable dataTable = SelectById(item.Id);
            DataRow row = dataTable.GetRow(item);
            try
            {
                Model2Row(item, row);
                int ret = Save(dataTable);

                item.Id = dataTable.Rows[0].Field<int>(PKField);
                return ret;
            }
            catch (Exception ex)
            {
                log.Debug(ex);
                return -1;
            }
        }


        public int UpdateByPid(int pid, List<T> datas)
        {
            string sql = $"select * from {TableName} where pid={pid}";
            DataTable dataTable = GetData(sql);
            dataTable.TableName = TableName;
            foreach (var item in datas)
            {
                DataRow row = dataTable.GetRow(item);
                Model2Row(item, row);
            }
            return Save(dataTable);
        }

        public int SaveByPid(int pid, IEnumerable<T> datas)
        {
            DeleteAllByPid(pid, false);
            DataTable dataTable = new DataTable(TableName);
            CreateColumns(dataTable);
            foreach (var item in datas)
            {
                DataRow row = dataTable.NewRow();
                dataTable.Rows.Add(row);
                Model2Row(item, row);
                if (item.Id <= 0)
                    row[PKField] = DBNull.Value;
            }
            return BulkInsertAsync(dataTable);
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


                int i = 0;
                foreach (DataColumn col in dataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(new MySqlConnector.MySqlBulkCopyColumnMapping(i, col.ColumnName));
                    i++;
                }

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




        public T? GetByParam(Dictionary<string, object> param) => GetAllByParam(param).FirstOrDefault();

        public List<T> GetAllByParam(Dictionary<string, object> param,int limit = -1)
        {
            string whereClause = string.Empty;
            Dictionary<string, object> dbParams = new Dictionary<string, object>();

            if (param != null && param.Count > 0)
            {
                var conditions = new List<string>();
                foreach (var p in param)
                {
                    if (p.Value == null)
                    {
                        conditions.Add($"{p.Key} IS NULL");
                    }
                    else
                    {
                        conditions.Add($"{p.Key} = @{p.Key}");
                        dbParams.Add(p.Key, p.Value);
                    }
                }
                whereClause = "WHERE " + string.Join(" AND ", conditions);
            }
            else
            {
                dbParams = param; // 为空也可以传回去
            }

            string sql = $"SELECT * FROM {TableName} {whereClause} ";
            if (limit >= 1)
                sql += $" ORDER BY id DESC LIMIT {limit}";

            DataTable d_info = GetData(sql, dbParams);

            List<T> list = new List<T>(d_info.Rows.Count);
            try
            {
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
            catch (Exception ex)
            {
                log.Warn(ex);
                return list;
            }

        }

        public List<T> ConditionalQuery(Dictionary<string, object> param, int limit = -1)
        {
            string sql = $"select * from {TableName} where 1=1";
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
            if (limit >= 1)
            {
                sql += $" ORDER BY id DESC LIMIT {limit}";
            }
            DataTable d_info = GetData(sql, param);
            List<T> list = new List<T>(d_info.Rows.Count);
            try
            {
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
            catch (Exception ex)
            {
                log.Debug(ex);
                return list;
            }
        }

        public int DeleteAllByPid(int pid, bool IsLogicDel = true) => DeleteAllByParam(new Dictionary<string, object>() { { "pid", pid } }, IsLogicDel);

        public int DeleteById(int id, bool IsLogicDel = true) => DeleteAllByParam(new Dictionary<string, object>() { { "id", id } }, IsLogicDel);


        public T? GetLatestResult()
        {
            return GetByCreateDate(limit: 1).FirstOrDefault();
        }

        public List<T> GetByCreateDate(int limit = 1)
        {
            List<T> list = new();
            string sql = $"select * from {TableName} ORDER BY create_date DESC LIMIT @Limit";
            var parameters = new Dictionary<string, object>
            {
                {"@Limit", limit}
            };
            DataTable d_info = GetData(sql, parameters);
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
    }
}
