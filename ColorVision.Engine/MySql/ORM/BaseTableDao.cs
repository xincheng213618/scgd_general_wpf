﻿using ColorVision.Engine.Templates;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.MySql.ORM
{

    public static class BaseTableDaoExtensions
    {
        public static List<T> GetAll<T>(this BaseTableDao<T> dao) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>());
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "pid", pid } });
        }

        public static List<T> GetAllByTenantId<T>(this BaseTableDao<T> dao, int tenantId) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", tenantId } });
        }


        public static List<T> GetAllById<T>(this BaseTableDao<T> dao, int id) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "id", id } });
        }

        public static T? GetById<T>(this BaseTableDao<T> dao, int id) where T : IPKModel, new()
        {
            return dao.GetByParam(new Dictionary<string, object> { { "id", id } });
        }

    }

    public class BaseTableDao<T> : BaseDao where T : IPKModel ,new()
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDao<T>));

        public BaseTableDao(string tableName, string pkField ="id") : base(tableName, pkField)
        {

        }

        public virtual T? GetModelFromDataRow(DataRow item) => ReflectionHelper.GetModelFromDataRow<T>(item);
        public virtual DataRow Model2Row(T item, DataRow row) => ReflectionHelper.Model2RowAuto(item, row);
        public virtual DataTable CreateColumns(DataTable dataTable) => ReflectionHelper.CreateColumns<ModDetailModel>(dataTable);

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

        public T? GetByParam(Dictionary<string, object> param) => GetAllByParam(param).FirstOrDefault();

        public List<T> GetAllByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {TableName} {whereClause}";
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
                log.Warn(ex);
                return list;
            }

        }

        public List<T> ConditionalQuery(Dictionary<string, object> param)
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


        public int GetNextAvailableId()
        {
            int nextId = 1;
            string query = $"SELECT MAX(id) FROM {TableName}";
            MySqlCommand cmd = new MySqlCommand(query, MySqlControl.MySqlConnection);

            object result = cmd.ExecuteScalar();
            if (result != DBNull.Value && result != null)
            {
                int maxId = Convert.ToInt32(result);
                nextId = maxId + 1;
            }

            return nextId;
        }
    }
}
