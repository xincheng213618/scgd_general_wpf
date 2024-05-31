using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.MySql.ORM
{
    public class BaseTableDao<T> : BaseDao where T : IPKModel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDao<T>));

        public BaseTableDao(string tableName, string pkField) : base(tableName, pkField)
        {

        }

        public virtual T? GetModelFromDataRow(DataRow item) => default;
        public virtual DataRow Model2Row(T item, DataRow row) => row;
        public virtual DataTable CreateColumns(DataTable dataTable) => dataTable;

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



        public T? GetById(int id) => GetByParam(new Dictionary<string, object> { { "id", id } });

        public T? GetByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {TableName} {whereClause}";

            try
            {
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
            catch (Exception ex)
            {
                log.Debug(ex);
                return default;
            }

        }


        public List<T> GetAll() => GetAllByParam(new Dictionary<string, object>());
        public List<T> GetAllById(int id) => GetAllByParam(new Dictionary<string, object>() { { "id", id } });
        public List<T> GetAllByPid(int pid) => GetAllByParam(new Dictionary<string, object>() { { "pid", pid } });
        public List<T> GetAllByTenantId(int tenantId) => GetAllByParam(new Dictionary<string, object>() { { "tenant_id", tenantId } });

        public List<T> GetAllByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {TableName} {whereClause}";
            DataTable d_info = GetData(sql, param);

            List<T> list = new();
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

        public List<T> ConditionalQuery(Dictionary<string, object> param)
        {
            List<T> list = new();
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
            try
            {
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
            catch (Exception ex)
            {
                log.Debug(ex);
                return list;
            }
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
