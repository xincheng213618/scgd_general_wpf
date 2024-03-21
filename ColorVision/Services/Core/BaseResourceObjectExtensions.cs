using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Terminal;

namespace ColorVision.Services.Core
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


        public static bool ExistsDevice<T>(this T This,string Code) where T : BaseResourceObject
        {
            foreach (var item in This.VisualChildren)
            {
                if (item is DeviceService t && t.Code == Code)
                    return true;
                if (item is TerminalService t1 && t1.Code == Code)
                    return true;
            }
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
