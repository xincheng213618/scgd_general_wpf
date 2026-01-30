using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Templates;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services
{
    public static class ServiceObjectBaseExtensions
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateInitializer));

        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this ServiceObjectBase This) where T : ServiceObjectBase
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }


        public static T CreateDefaultConfig<T>() where T : ViewModelBase, new() => new();

        public static T TryDeserializeConfig<T>(string? json) where T : ViewModelBase, new() 
        {
            if (string.IsNullOrEmpty(json)) return CreateDefaultConfig<T>();
            try
            {
                return JsonConvert.DeserializeObject<T>(json) ?? CreateDefaultConfig<T>();
            }
            catch(Exception ex)
            {
                log.Error($"反序列化配置时出错，返回默认配置。异常信息：{ex}");
                return CreateDefaultConfig<T>();
            }
        }

        public static bool ExistsDevice<T>(this T This,string Code) where T : ServiceObjectBase
        {
            // 检查本地 VisualChildren 是否有 Code 重复
            foreach (var item in This.VisualChildren)
            {
                if ((item is DeviceService ds && ds.Code == Code) ||
                    (item is TerminalService ts && ts.Code == Code))
                {
                    return true;
                }
            }
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            //这里追加一个规则，所有Code均不允许相同 2024.04.19
            var exists = Db.Queryable<SysResourceModel>() .Any(x => x.Code == Code);

            return exists;
        }

        public static string NewCreateFileName<T>(this T t ,string FileName) where T : ServiceObjectBase
        {
            if (!t.ExistsDevice(FileName))
                return FileName;
            for (int i = 1; i < 999; i++)
            {
                if (!t.ExistsDevice($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }
    }
}
