using System.Collections.Generic;
using System.Data;

namespace ColorVision.MySql.ORM
{
    public class BaseDao1 : BaseDao
    {
        public bool IsLogicDel { get { return _IsLogicDel; } set { _IsLogicDel = value; } }
        private bool _IsLogicDel;

        public BaseDao1(string tableName, string pkField, bool isLogicDel) : base(tableName, pkField)
        {
            _IsLogicDel = isLogicDel;
        }

        protected string GetDelSQL(bool hasAnd) => _IsLogicDel ? hasAnd ? " and is_delete=0" : "is_delete=0" : string.Empty;

        public DataTable SelectById(int id)
        {
            string sql = $"select * from {TableName} where id=@id" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "id",  id}
            };
            DataTable d_info = GetData(sql, param);
            return d_info;
        }
    }
}
