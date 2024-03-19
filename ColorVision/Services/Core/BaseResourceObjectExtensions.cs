using ColorVision.Services.Devices;

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


        public static bool ExistsDevice(this BaseResourceObject This,string Code)
        {
            foreach (var item in This.VisualChildren)
            {
                if (item is DeviceService t && t.Code == Code)
                    return true;
            }
            return false;
        }
    }
}
