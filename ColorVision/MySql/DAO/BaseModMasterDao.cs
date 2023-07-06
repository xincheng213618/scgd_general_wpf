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
        public BaseModMasterDao(string code, string viewName, string tableName, string pkField, bool isLogicDel) : base(viewName, tableName, pkField, isLogicDel)
        {
            Code = code;
        }

        public override DataTable GetTableAllByTenantId(int tenantId)
        {
            string sql;
            if(string.IsNullOrEmpty(ViewName)) sql = $"select * from {TableName} where is_delete=0 and tenant_id={tenantId} and pcode='{Code}'";
            else sql = $"select * from {ViewName} where is_delete=0 and tenant_id={tenantId} and pcode='{Code}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public string GetPCode() { return Code; }
    }

    public class BaseModDetailDao<T> : BaseServiceMaster<T> where T : IBaseModel
    {
        protected string Code { get; set; }
        public BaseModDetailDao(string code, string viewName, string tableName, string pkField, bool isLogicDel) : base(viewName, tableName, pkField, isLogicDel)
        {
            Code = code;
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("cc_pid", typeof(int));
            dInfo.Columns.Add("pid", typeof(int));
            dInfo.Columns.Add("value_a", typeof(string));
            dInfo.Columns.Add("value_b", typeof(string));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            return dInfo;
        }
    }
}
