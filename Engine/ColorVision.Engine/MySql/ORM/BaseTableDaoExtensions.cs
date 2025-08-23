using MySql.Data.MySqlClient;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.MySql.ORM
{
    public static class BaseTableDaoExtensions
    {
        public static List<T> GetAll<T>(this BaseTableDao<T> dao,int limit = -1) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>(), limit);
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "pid", pid } });
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid,bool isEnable = true,bool isDelete = false) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "pid", pid }, { "is_enable", isEnable }, { "is_delete", isDelete } });
        }
         

        public static T? GetById<T>(this BaseTableDao<T> dao, int? id) where T : IPKModel, new()
        {
            if (id == null) return default;
            return dao.GetByParam(new Dictionary<string, object> { { "id", id } });
        }

        public static List<T> GetAllByBatchId<T>(this BaseTableDao<T> dao, int batchid) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object> { { "batch_id", batchid } });
        }

        public static int GetNextAvailableId<T>(this BaseTableDao<T> dao) where T : IPKModel, new()
        {   
            int maxId = MySqlControl.GetInstance().DB.Queryable<T>().Max(it => it.Id); // 这里假设 IPKModel 有 Id 属性
            return maxId > 0 ? maxId + 1 : 1;
        }

    }
}
