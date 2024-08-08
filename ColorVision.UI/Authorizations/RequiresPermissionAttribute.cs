namespace ColorVision.UI.Authorizations
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

