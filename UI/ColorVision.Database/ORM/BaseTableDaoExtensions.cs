using SqlSugar;
using System.Collections.Generic;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace ColorVision.Database
{
    public static class BaseTableDaoExtensions
    {
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
