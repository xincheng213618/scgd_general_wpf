namespace ColorVision.Rbac.Exceptions
{
    /// <summary>
    /// RBAC模块基础异常类
    /// </summary>
    public class RbacException : Exception
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; }

        public RbacException(string message, string errorCode = "RBAC_ERROR") 
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public RbacException(string message, Exception innerException, string errorCode = "RBAC_ERROR") 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// 权限不足异常
    /// </summary>
    public class PermissionDeniedException : RbacException
    {
        public PermissionDeniedException(string message) 
            : base(message, "PERMISSION_DENIED") 
        { }

        public PermissionDeniedException(string message, Exception innerException) 
            : base(message, innerException, "PERMISSION_DENIED") 
        { }
    }

    /// <summary>
    /// 无效凭据异常
    /// </summary>
    public class InvalidCredentialsException : RbacException
    {
        public InvalidCredentialsException() 
            : base("用户名或密码不正确", "INVALID_CREDENTIALS") 
        { }

        public InvalidCredentialsException(string message) 
            : base(message, "INVALID_CREDENTIALS") 
        { }
    }

    /// <summary>
    /// 用户不存在异常
    /// </summary>
    public class UserNotFoundException : RbacException
    {
        public int UserId { get; }

        public UserNotFoundException(int userId) 
            : base($"用户不存在: {userId}", "USER_NOT_FOUND") 
        {
            UserId = userId;
        }
    }

    /// <summary>
    /// 角色不存在异常
    /// </summary>
    public class RoleNotFoundException : RbacException
    {
        public int RoleId { get; }

        public RoleNotFoundException(int roleId) 
            : base($"角色不存在: {roleId}", "ROLE_NOT_FOUND") 
        {
            RoleId = roleId;
        }
    }

    /// <summary>
    /// 会话无效异常
    /// </summary>
    public class InvalidSessionException : RbacException
    {
        public InvalidSessionException() 
            : base("会话无效或已过期", "INVALID_SESSION") 
        { }

        public InvalidSessionException(string message) 
            : base(message, "INVALID_SESSION") 
        { }
    }

    /// <summary>
    /// 并发冲突异常
    /// </summary>
    public class ConcurrencyException : RbacException
    {
        public ConcurrencyException() 
            : base("数据已被其他人修改，请刷新后重试", "CONCURRENCY_CONFLICT") 
        { }

        public ConcurrencyException(string message) 
            : base(message, "CONCURRENCY_CONFLICT") 
        { }
    }
}
