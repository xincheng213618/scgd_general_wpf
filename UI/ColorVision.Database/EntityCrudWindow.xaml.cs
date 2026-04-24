using ColorVision.Common.Utilities;
using ColorVision.Themes;
using SqlSugar;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace ColorVision.Database
{
    /// <summary>
    /// 独立的实体 CRUD 窗口 - 内部使用 EntityCrudControl
    /// </summary>
    public partial class EntityCrudWindow : Window
    {
        private readonly Type _entityType;

        public EntityCrudWindow(Type entityType)
        {
            _entityType = entityType;
            InitializeComponent();
            var tableName = entityType.GetCustomAttribute<SugarTable>()?.TableName
                            ?? entityType.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                            ?? entityType.Name;
            Title = $"数据管理 - {tableName}";
            this.ApplyCaption();
        }

        /// <summary>
        /// 静态工厂方法 - 保留兼容性
        /// </summary>
        public static EntityCrudWindow Create<T>() where T : class, IEntity, new()
        {
            return new EntityCrudWindow(typeof(T));
        }

        [Obsolete("使用 Create<T>() 或 new EntityCrudWindow(Type) 替代")]
        public static EntityCrudWindow Create<T>(BaseTableDao<T> daoInstance, string tableName) where T : class, IEntity, new()
        {
            return new EntityCrudWindow(typeof(T));
        }

        [Obsolete("使用 Create<T>() 或 new EntityCrudWindow(Type) 替代")]
        public static EntityCrudWindow Create<T>(BaseTableDao<T> daoInstance) where T : class, IEntity, new()
        {
            return new EntityCrudWindow(typeof(T));
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ContentRoot.Children.Add(new EntityCrudControl(_entityType));
        }
    }
}
