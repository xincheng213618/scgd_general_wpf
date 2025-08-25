using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Terminal;
using Newtonsoft.Json;
using ColorVision.Database;

namespace ColorVision.Engine.Services.Core
{
    public static class ServiceObjectBaseExtensions
    {
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
            catch
            {
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
            //这里追加一个规则，所有Code均不允许相同 2024.04.19
            var exists = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>() .Any(x => x.Code == Code);

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
