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
        protected string _code;
        protected string _viewName;
        public BaseModMasterDao(string code, string viewName, string tableName, string pkField) : base(tableName, pkField)
        {
            _code = code;
            _viewName = viewName;
        }

        public override DataTable GetTableAll(int tenantId)
        {
            string sql;
            if(string.IsNullOrEmpty(_viewName)) sql = $"select * from {TableName} where is_delete=0 and tenant_id={tenantId} and pcode='{_code}'";
            else sql = $"select * from {_viewName} where is_delete=0 and tenant_id={tenantId} and pcode='{_code}'";
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
