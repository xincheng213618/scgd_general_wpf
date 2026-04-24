using System;

namespace ColorVision.Database
{
    public enum DatabaseType
    {
        MySql,
        Sqlite
    }

    /// <summary>
    /// 标记实体类型使用的数据库来源，默认 MySql
    /// 用法: [DatabaseSource(DatabaseType.Sqlite)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DatabaseSourceAttribute : Attribute
    {
        public DatabaseType DatabaseType { get; }

        public DatabaseSourceAttribute(DatabaseType databaseType)
        {
            DatabaseType = databaseType;
        }
    }
}
