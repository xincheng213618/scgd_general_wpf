using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;

namespace ColorVision.Rbac.Dtos
{
    public class LoginResultDto:ViewModelBase
    {
        public UserSummaryDto User { get => _User; set { _User = value;  OnPropertyChanged(); } } 
        private UserSummaryDto _User = new();

        public UserDetailDto UserDetail { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
    }

    public class UserSummaryDto: ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get => _Username; set { _Username = value; OnPropertyChanged(); } } 
        private string _Username = "";
        public bool IsEnable { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class UserDetailDto
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? Remark { get; set; }
        public string? UserImage { get; set; }
        public PermissionMode PermissionMode { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
