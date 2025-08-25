using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Database
{
    public class BaseTableDao<T> : BaseDao where T : IPKModel ,new()
    {

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

    }
}
