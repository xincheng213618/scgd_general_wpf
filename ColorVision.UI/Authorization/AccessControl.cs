using System;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Authorization
{
    public class Authorization : IConfig
    {
        public static Authorization Instance => ConfigHandler.GetInstance().GetRequiredService<Authorization>();

        public PermissionMode PermissionMode { get; set; } = PermissionMode.Administrator;
    }

    public static class AccessControl
    {
        public static bool Check(Action action)
        {
            var attribute = action.Method.GetCustomAttribute<RequiresPermissionAttribute>();
            return attribute == null || Authorization.Instance.PermissionMode <= attribute.RequiredPermission;
        }
        public static bool Check(PermissionMode permissionMode) =>  Authorization.Instance.PermissionMode <= permissionMode;

        public static void ExecuteWithPermissionCheck(Action action, PermissionMode currentPermission)
        {
            var methodInfo = action.Method;
            var attribute = methodInfo.GetCustomAttribute<RequiresPermissionAttribute>();

            if (attribute != null)
            {
                if (currentPermission == attribute.RequiredPermission)
                {
                    action();
                }
                else
                {
                    MessageBox.Show("You do not have the required permission to execute this action.");
                }
            }
            else
            {
                action();
            }
        }
    }


}

