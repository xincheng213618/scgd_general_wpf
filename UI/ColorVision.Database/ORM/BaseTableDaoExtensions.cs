using Google.Protobuf.WellKnownTypes;
using log4net;
using log4net.Util;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Database
{
    public static class BaseTableDaoExtensions
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDaoExtensions));

        public static List<T> GetAll<T>(this BaseTableDao<T> dao, int limit = -1) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                var query = db.Queryable<T>();
                if (limit > 0)
                    query = query.Take(limit);
                return query.ToList();
            }
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                return db.Queryable<T>()
                         .Where("pid = @pid", new { pid })
                         .ToList();
            }
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid, bool isEnable = true, bool isDelete = false) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                return db.Queryable<T>()
                         .Where("pid = @pid AND is_enable = @isEnable AND is_delete = @isDelete",
                                new { pid, isEnable, isDelete })
                         .ToList();
            }
        }

        public static T? GetById<T>(this BaseTableDao<T> dao, int? id) where T : IPKModel, new()
        {
            if (id == null || id <= 0) return default;

            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                // 使用 db 进行查询
                return db.Queryable<T>()
                         .Where(x => x.Id == id)
                         .First();
            }
        }

        public static List<T> GetAllByBatchId<T>(this BaseTableDao<T> dao, int batchid) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                return db.Queryable<T>()
                         .Where("batch_id = @batchid", new { batchid })
                         .ToList();
            }
        }

        public static List<T> GetAllByParam<T>(this BaseTableDao<T> dao, Dictionary<string, object> param, int limit = -1) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                var query = db.Queryable<T>();

                if (param != null && param.Count > 0)
                {
                    foreach (var kv in param)
                    {
                        string name = kv.Key;
                        object value = kv.Value;
                        if (kv.Value == null)
                        {
                            query = query.Where($"{kv.Key} IS NULL");
                        }
                        else
                        {
                            // 枚举类型转 int
                            if (value.GetType().IsEnum)
                            {
                                value = (int)value;
                            }
                            // bool 类型转 int
                            else if (value is bool b)
                            {
                                value = b ? 1 : 0;
                            }

                            Dictionary<string, object> param1 = new Dictionary<string, object>
                    {
                        { name, value }
                    };
                            query = query.Where($"{kv.Key} = @{kv.Key}", param1);
                        }
                    }
                }

                if (limit > 0)
                {
                    query = query.OrderBy(x=>x.Id, OrderByType.Desc).Take(limit);
                }

                return query.ToList();
            }
        }

        public static T? GetByParam<T>(this BaseTableDao<T> dao, Dictionary<string, object> param) where T : IPKModel,new()
        {
            return dao.GetAllByParam(param, 1).FirstOrDefault();
        }

        public static int Save<T>(this BaseTableDao<T> dao, T item) where T : class, IPKModel, new()     
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                }))
                {
                    // 主键为0视为插入，否则按主键更新
                    int ret;
                    if (item.Id == 0)
                    {
                        var newId = db.Insertable<T>(item).ExecuteReturnIdentity(); // 返回自增主键
                        item.Id = newId;
                        ret = 1;
                    }
                    else
                    {
                        ret = db.Updateable(item).Where(x => x.Id == item.Id).ExecuteCommand(); // 返回受影响行数
                    }
                    return ret;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return -1;
            }
        }

        public static int GetNextAvailableId<T>(this BaseTableDao<T> dao) where T : IPKModel, new()
        {
            using (SqlSugarClient db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            }))
            {
                int maxId = db.Queryable<T>().Max(it => it.Id);
                return maxId > 0 ? maxId + 1 : 1;
            }
        }

    }
}
