using log4net;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql
{
    public class BaseTableDao<T>:BaseDao where T : IPKModel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDao<T>));

        public BaseTableDao(string tableName, string pkField) : base(tableName, pkField)
        {

        }

        public virtual T? GetModelFromDataRow(DataRow item) => default;
        public virtual DataRow Model2Row(T item, DataRow row) => row;
        public virtual DataTable CreateColumns(DataTable dInfo) => dInfo;

        public DataTable SelectById(int id)
        {
            string sql = $"select * from {TableName} where id=@id";
            return GetData(sql, new Dictionary<string, object> { { "id", id } });
        }

        public virtual int Save(T item)
        {
            DataTable dataTable = SelectById(item.GetPK());
            DataRow row = dataTable.GetRow(item);
            Model2Row(item, row);
            int ret = Save(dataTable);
            item.SetPK(dataTable.Rows[0].Field<int>(PKField));
            return ret;
        }


        public List<T> GetAll() => GetAllByParam(new Dictionary<string, object>());
        public List<T> GetAllById(int id) => GetAllByParam(new Dictionary<string, object>() { { "id", id } });
        public List<T> GetAllByPid (int pid) => GetAllByParam(new Dictionary<string, object>() { { "pid", pid } });
        public List<T> GetAllByTenantId(int tenantId) => GetAllByParam(new Dictionary<string, object>() { { "tenantId", tenantId } });

        public List<T> GetAllByParam(Dictionary<string, object> param)
        {
            string whereClause = string.Empty;
            if (param != null && param.Count > 0)
                whereClause = "WHERE " + string.Join(" AND ", param.Select(p => $"{p.Key} = @{p.Key}"));
            string sql = $"SELECT * FROM {TableName} {whereClause}";
            DataTable d_info = GetData(sql, param);

            List<T> list = new List<T>();
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
