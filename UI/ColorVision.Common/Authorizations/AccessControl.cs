using System;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Authorizations
{
    public class Authorization : IConfig
    {
        public static Authorization Instance { get; set; } 

        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; OnPermissionModeChanged();  } }
        private PermissionMode _PermissionMode = PermissionMode.Guest; // 默认访客权限，避免未登录时拥有管理员权限

        public event EventHandler PermissionModeChanged;

        protected virtual void OnPermissionModeChanged()
        {
            PermissionModeChanged?.Invoke(this, EventArgs.Empty);
        }
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

