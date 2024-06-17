using System;

namespace ColorVision.UI.Authorization
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


}

