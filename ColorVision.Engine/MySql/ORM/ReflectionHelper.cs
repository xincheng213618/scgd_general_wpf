using Microsoft.DwayneNeed.Win32.Gdi32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.MySql.ORM
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }

        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TableAttribute : Attribute
    {
        public string TableName { get; }

        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }

    public static class ReflectionHelper
    {

        public static BaseTableDao<T>? Create<T>() where T :IPKModel, new()
        {
            var type = typeof(BaseTableDao<>);
            var TableName = GetTableName(type);

            var genericType = type.MakeGenericType(typeof(T));
            return (BaseTableDao<T>)Activator.CreateInstance(genericType, TableName);
        }



        private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new Dictionary<Type, PropertyInfo[]>();
        public static T GetModelFromDataRow<T>(DataRow row) where T : new()
        {
            T model = new T();
            var properties = GetProperties(typeof(T));

            foreach (var prop in properties)
            {
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

        public static DataRow Model2Row<T>(T model, DataRow row)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                var columnName = GetColumnName(prop);
                if (row.Table.Columns.Contains(columnName))
                {
                    var value = prop.GetValue(model);
                    row[columnName] = value ?? DBNull.Value;
                }
            }
            return row;
        }

        public static DataTable CreateColumns<T>(DataTable table)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                var columnName = GetColumnName(prop);
                table.Columns.Add(columnName, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }
            return table;
        }
        private static PropertyInfo[] GetProperties(Type type)
        {
            if (!PropertyCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties();
                PropertyCache[type] = properties;
            }
            return properties;
        }
        private static string GetColumnName(System.Reflection.PropertyInfo prop)
        {
            var attribute = prop.GetCustomAttributes(typeof(ColumnAttribute), false).FirstOrDefault() as ColumnAttribute;
            return attribute?.Name ?? prop.Name;
        }

        private static string GetTableName(Type type)
        {
            var attribute = type.GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault() as TableAttribute;
            return attribute?.TableName ?? type.Name;
        }
    }

}
