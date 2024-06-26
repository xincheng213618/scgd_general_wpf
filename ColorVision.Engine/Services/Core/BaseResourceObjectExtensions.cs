using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Core
{
    public static class BaseResourceObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this BaseResourceObject This) where T : BaseResourceObject
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


        public static bool ExistsDevice<T>(this T This,string Code) where T : BaseResourceObject
        {
            foreach (var item in This.VisualChildren)
            {
                if (item is DeviceService t && t.Code == Code)
                    return true;
                if (item is TerminalService t1 && t1.Code == Code)
                    return true;
            }
            //这里追加一个规则，所有服务下设备名称均不允许相同
            //这里追加一个规则，所有Code均不允许相同 2024.04.19
            var deviceS = SysResourceDao.Instance.GetByParam(new System.Collections.Generic.Dictionary<string, object>() { { "code", Code } });
            if (deviceS != null)
                return true;
            return false;
        }

        public static string NewCreateFileName<T>(this T t ,string FileName) where T : BaseResourceObject
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
