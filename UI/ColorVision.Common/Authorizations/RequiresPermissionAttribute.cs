using System;

namespace ColorVision.UI.Authorizations
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class RequiresPermissionAttribute : Attribute
    {
        public PermissionMode RequiredPermission { get; } 

        public RequiresPermissionAttribute(PermissionMode requiredPermission)
        {
            RequiredPermission = requiredPermission;
        }
    }


}

