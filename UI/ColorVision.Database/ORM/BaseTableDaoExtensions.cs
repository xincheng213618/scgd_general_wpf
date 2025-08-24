using System.Collections.Generic;

namespace ColorVision.Database
{
    public static class BaseTableDaoExtensions
    {
        public static List<T> GetAll<T>(this BaseTableDao<T> dao, int limit = -1) where T : IPKModel, new()
        {
            var db = MySqlControl.GetInstance().DB.Queryable<T>();
            if (limit > 0)
                db = db.Take(limit);
            return db.ToList();
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid) where T : IPKModel, new()
        {
            // 这里假设表里字段名为 pid（小写），可根据实际字段名调整
            var db = MySqlControl.GetInstance().DB.Queryable<T>()
                .Where($"pid = @pid", new { pid });
            return db.ToList();
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid, bool isEnable = true, bool isDelete = false) where T : IPKModel, new()
        {
            var db = MySqlControl.GetInstance().DB.Queryable<T>()
                .Where("pid = @pid AND is_enable = @isEnable AND is_delete = @isDelete",
                    new { pid, isEnable, isDelete });
            return db.ToList();
        }

        public static T? GetById<T>(this BaseTableDao<T> dao, int? id) where T : IPKModel, new()
        {
            if (id == null || id <= 0) return default;
            var db = MySqlControl.GetInstance().DB.Queryable<T>()
                .Where("id = @id", new { id });
            return db.First();
        }

        public static List<T> GetAllByBatchId<T>(this BaseTableDao<T> dao, int batchid) where T : IPKModel, new()
        {
            var db = MySqlControl.GetInstance().DB.Queryable<T>()
                .Where("batch_id = @batchid", new { batchid });
            return db.ToList();
        }

        public static int GetNextAvailableId<T>(this BaseTableDao<T> dao) where T : IPKModel, new()
        {   
            int maxId = MySqlControl.GetInstance().DB.Queryable<T>().Max(it => it.Id); // 这里假设 IPKModel 有 Id 属性
            return maxId > 0 ? maxId + 1 : 1;
        }

    }
}
