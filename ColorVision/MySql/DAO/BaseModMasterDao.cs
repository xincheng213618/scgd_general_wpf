using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class BaseModMasterDao<T> : BaseServiceMaster<T> where T : IBaseModel
    {
        protected string Code { get; set; }
        protected string ViewName { get; set; }
        public BaseModMasterDao(string code, string viewName, string tableName, string pkField) : base(tableName, pkField)
        {
            this.Code = code;
            this.ViewName = viewName;
        }

        public override DataTable GetTableAll(int tenantId)
        {
            string sql;
            if(string.IsNullOrEmpty(ViewName)) sql = $"select * from {TableName} where is_delete=0 and tenant_id={tenantId} and pcode='{Code}'";
            else sql = $"select * from {ViewName} where is_delete=0 and tenant_id={tenantId} and pcode='{Code}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }
    }

    public class BaseModDetailDao<T> : BaseDao
    {
        public BaseModDetailDao(string tableName, string pkField) : base(tableName, pkField)
        {
        }
    }
}
