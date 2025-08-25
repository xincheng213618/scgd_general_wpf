using log4net;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Database
{
    /// <summary>
    /// 因为项目中本身包含Service,所以这里取消Service层的设置，直接从Dao层
    /// </summary>
    public class BaseDao
    {  
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));

        public static SqlSugar.SqlSugarClient Db => MySqlControl.GetInstance().DB;

        public string TableName { get { return _TableName; } set { _TableName = value; } }
        private string _TableName;

        public BaseDao(string tableName, string pkField)
        {
            _TableName = tableName;
        }
    }
}
