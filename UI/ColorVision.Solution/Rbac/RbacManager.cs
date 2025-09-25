using ColorVision.Common.MVVM;
using ColorVision.Rbac.Services;
using ColorVision.Rbac.Services.Auth;
using ColorVision.Rbac.Security;
using ColorVision.UI.Authorizations;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ColorVision.Rbac
{
    public class RbacManager:IDisposable
    {
        private static RbacManager _instance;
        private static readonly object Locker = new();
        public static RbacManager GetInstance() { lock (Locker) { return _instance ??= new RbacManager(); } }

        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        public static string SqliteDbPath { get; set; } = DirectoryPath + "Rbac.db";

        private SqlSugarClient db;
        public RelayCommand LoginCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public RelayCommand OpenUserManagerCommand { get; set; }

        public static RbacManagerConfig Config => RbacManagerConfig.Instance;
        public AuthService AuthService { get; set; }
        public UserService UserService { get; set; }

        public EditUserDetailAction EditUserDetailAction { get; set; }
        public RbacManager()
        {  
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = $"DataSource={SqliteDbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
            });

            db.CodeFirst.InitTables<UserEntity, UserDetailEntity>();
            db.CodeFirst.InitTables<TenantEntity, UserTenantEntity>();
            db.CodeFirst.InitTables<RoleEntity, UserRoleEntity>();

            AuthService = new AuthService(db);
            UserService = new UserService(db);
            EditUserDetailAction = new EditUserDetailAction(UserService);

            InitAdmin();

            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            EditCommand = new RelayCommand(a=> EditUserDetailAction.EditAsync());
            OpenUserManagerCommand = new RelayCommand(a => OpenUserManager());

            // 若已有登录缓存则同步权限
            if (Config.LoginResult?.UserDetail != null)
                Authorization.Instance.PermissionMode = Config.LoginResult.UserDetail.PermissionMode;
        }

        public void OpenUserManager()
        {
            new UserManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public List<UserEntity> GetUsers()
        {
            var users = db.Queryable<UserEntity>().Where(u => u.IsDelete == false || u.IsDelete == null).ToList();
            return users;
        }

        public List<RoleEntity> GetRoles()
        {
            return db.Queryable<RoleEntity>().Where(r => r.IsDelete != true && r.IsEnable).ToList();
        }

        private void InitAdmin()
        {
            // 创建管理员角色
            var adminRole = db.Queryable<RoleEntity>().First(r => r.Code == "admin");
            if (adminRole == null)
            {
                adminRole = new RoleEntity
                {
                    Name = "管理员",
                    Code = "admin",
                    Remark = "系统管理员角色",
                    IsEnable = true,
                    IsDelete = false,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                db.Insertable(adminRole).ExecuteCommand();
            }

            // 创建管理员用户
            var adminUser = db.Queryable<UserEntity>().First(r => r.Username == "admin");
            if (adminUser == null)
            {
                adminUser = new UserEntity
                {
                    Username = "admin",
                    Password = PasswordHasher.Hash("admin"),
                    IsEnable = true,
                    IsDelete = false,
                    Remark = "系统管理员",
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                var adminId = db.Insertable(adminUser).ExecuteReturnIdentity();
                // 详情
                db.Storageable(new UserDetailEntity
                {
                    UserId = adminId,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now,
                    PermissionMode = ColorVision.UI.Authorizations.PermissionMode.SuperAdministrator
                }).WhereColumns(it => new { it.UserId }).ToStorage().AsInsertable.ExecuteCommand();
            }

            // 关联
            adminUser = db.Queryable<UserEntity>().First(r => r.Username == "admin");
            adminRole = db.Queryable<RoleEntity>().First(r => r.Code == "admin");
            if (adminUser != null && adminRole != null)
            {
                bool exists = db.Queryable<UserRoleEntity>().Any(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id);
                if (!exists)
                {
                    db.Insertable(new UserRoleEntity { UserId = adminUser.Id, RoleId = adminRole.Id }).ExecuteCommand();
                }
            }
        }

        public bool CreateUser(string username, string password, string remark = "", List<int> roleIds = null)
        {
            // 兼容旧同步接口 => 调用异步实现
            return UserService.CreateUserAsync(username, password, remark, roleIds).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
