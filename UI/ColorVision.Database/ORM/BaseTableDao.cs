using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Database
{
    public class BaseTableDao<T> : BaseDao where T : IPKModel ,new()
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDao<T>));

        public BaseTableDao() : base(ReflectionHelper.GetTableName(typeof(T)), ReflectionHelper.GetPrimaryKey(typeof(T)))
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

    }
}
