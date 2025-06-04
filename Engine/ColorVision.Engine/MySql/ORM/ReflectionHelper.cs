using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace ColorVision.Engine.MySql.ORM
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; set; }

        public string Comment { get; set; }

        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TableAttribute : Attribute
    {
        public string TableName { get; set; }
        //默认是id
        public string PrimaryKey { get; set; } = "id";

        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }

    public static class ReflectionHelper
    {
        public static BaseTableDao<T>? Create<T>() where T : IPKModel, new()
        {
            var type = typeof(BaseTableDao<>);
            var tableName = GetTableName(type);

            var genericType = type.MakeGenericType(typeof(T));
            return (BaseTableDao<T>)Activator.CreateInstance(genericType, tableName);
        }

        private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new Dictionary<Type, PropertyInfo[]>();

        public static T GetModelFromDataRow<T>(DataRow row) where T : new()
        {
            T model = new T();
            var properties = GetProperties(typeof(T));

            foreach (var prop in properties)
            {
                if (ShouldIgnoreProperty(prop)) continue;

                var columnName = GetColumnName(prop);
                if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                {
                    var value = row[columnName];
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    // Convert the value to the target type
                    object safeValue;
                    if (targetType.IsEnum)
                    {
                        // If the target type is an enum, convert the value to the underlying type of the enum
                        var enumUnderlyingType = Enum.GetUnderlyingType(targetType);
                        var enumValue = Convert.ChangeType(value, enumUnderlyingType);
                        safeValue = Enum.ToObject(targetType, enumValue);
                    }
                    else
                    {
                        safeValue = Convert.ChangeType(value, targetType);
                    }

                    prop.SetValue(model, safeValue);
                }
            }
            return model;
        }

        public static DataRow Model2RowAuto<T>(T model, DataRow row)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (ShouldIgnoreProperty(prop)) continue;

                var columnName = GetColumnName(prop);
                var value = prop.GetValue(model);

                if (ShouldIgnoreProperty(prop)) continue;
                if (!row.Table.Columns.Contains(columnName)) continue;

                // 自增需要从1开始
                if (columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && value is int intValue && intValue <= 0)
                {
                    row[columnName] = DBNull.Value;
                }
                else
                {
                    row[columnName] = value ?? DBNull.Value;
                }
            }
            return row;
        }

        public static DataRow Model2Row<T>(T model, DataRow row)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (ShouldIgnoreProperty(prop)) continue;

                var columnName = GetColumnName(prop);
                var value = prop.GetValue(model);
                // 自增需要从1开始
                row[columnName] = value ?? DBNull.Value;
            }
            return row;
        }

        public static DataTable CreateColumns<T>(DataTable table)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (ShouldIgnoreProperty(prop)) continue;

                var columnName = GetColumnName(prop);
                table.Columns.Add(columnName, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }
            return table;
        }

        public static PropertyInfo[] GetProperties(Type type)
        {
            if (!PropertyCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties();
                PropertyCache[type] = properties;
            }
            return properties;
        }

        public static string GetColumnName(PropertyInfo prop)
        {
            var attribute = prop.GetCustomAttributes(typeof(ColumnAttribute), false).FirstOrDefault() as ColumnAttribute;
            return attribute?.Name ?? prop.Name;
        }

        public static string GetTableName(Type type)
        {
            var attribute = type.GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault() as TableAttribute;
            return attribute?.TableName ?? type.Name;
        }
        public static string GetPrimaryKey(Type type)
        {
            var attribute = type.GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault() as TableAttribute;
            return attribute?.PrimaryKey ?? "id";
        }

        private static bool ShouldIgnoreProperty(PropertyInfo prop)
        {
            return prop.GetCustomAttributes(typeof(ColumnIgnoreAttribute), false).Length != 0;
        }
    }

}
