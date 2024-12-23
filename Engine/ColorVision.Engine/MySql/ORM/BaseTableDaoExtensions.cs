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
         
        public static List<T> GetAllByTenantId<T>(this BaseTableDao<T> dao, int tenantId) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", tenantId } });
        }

        public static T? GetById<T>(this BaseTableDao<T> dao, int? id) where T : IPKModel, new()
        {
            if (id == null) return default;
            return dao.GetByParam(new Dictionary<string, object> { { "id", id } });
        }

        public static T? GetByCode<T>(this BaseTableDao<T> dao, string? code) where T : IPKModel, new()
        {
            if (code == null) return default;
            return dao.GetByParam(new Dictionary<string, object> { { "code", code } });
        }

        public static List<T> GetAllByBatchId<T>(this BaseTableDao<T> dao, int batchid) where T : IPKModel, new()
        {
            return dao.GetAllByParam(new Dictionary<string, object> { { "batch_id", batchid } });
        }

    }
}
