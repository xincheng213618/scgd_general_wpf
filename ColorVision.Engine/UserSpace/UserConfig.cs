using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace ColorVision.UserSpace
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class RequiresPermissionAttribute : Attribute
    {
        public PermissionMode RequiredPermission { get; }

        public RequiresPermissionAttribute(PermissionMode requiredPermission)
        {
            RequiredPermission = requiredPermission;
        }
    }
    public static class PermissionChecker
    {
        public static bool Check(Action action)
        {
            var methodInfo = action.Method;
            var attribute = methodInfo.GetCustomAttribute<RequiresPermissionAttribute>();
            if (attribute != null)   
            {
                if (UserConfig.Instance.PerMissionMode == attribute.RequiredPermission)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

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



    public class UserConfig : ViewModelBase, IConfig
    {
        public static UserConfig Instance => ConfigHandler.GetInstance().GetRequiredService<UserConfig>();
        /// <summary>
        /// 账号
        /// </summary>
        public string Account { get => _Account; set { _Account = value; NotifyPropertyChanged(); } }
        private string _Account = string.Empty;
        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;


        public PermissionMode PerMissionMode { get => _PerMissionMode; set { _PerMissionMode = value; NotifyPropertyChanged(); } }
        private PermissionMode _PerMissionMode;

        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;

        //性别
        public Gender Gender { get => _Gender; set { _Gender = value; NotifyPropertyChanged(); } }
        private Gender _Gender;
        public string Email { get => _Email; set { _Email = value; NotifyPropertyChanged(); } }
        private string _Email = string.Empty;

        public string Phone { get => _Phone; set { _Phone = value; NotifyPropertyChanged(); } }
        private string _Phone = string.Empty;

        public string Address { get => _Address; set { _Address = value; NotifyPropertyChanged(); } }
        private string _Address = string.Empty;

        public string Company { get => _Company; set { _Company = value; NotifyPropertyChanged(); } }
        private string _Company = string.Empty;

        public string Department { get => _Department; set { _Department = value; NotifyPropertyChanged(); } }
        private string _Department = string.Empty;

        public string Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private string _Position = string.Empty;

        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;

        public string UserImage { get => _UserImage; set { _UserImage = value; NotifyPropertyChanged(); } }
        private string _UserImage = "Config\\user.jpg";
    }

    public enum Gender
    {
        [Description("Male")]
        Male,
        [Description("Female")]
        Female,
    }

    public enum PermissionMode
    {
        Administrator,
        PowerUser,
        User,
        Guest
    }
}

