namespace ColorVision.Database
{

    public class BaseDao
    {
        public static SqlSugar.SqlSugarClient Db => MySqlControl.GetInstance().DB;
    }

    public class BaseTableDao<T> : BaseDao where T : IPKModel ,new()
    {

    }
}
