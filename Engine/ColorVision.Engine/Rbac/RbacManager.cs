using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace ColorVision.Engine.Rbac
{
    public class UserLoginResult
    {
        public UserEntity User { get; set; } =new UserEntity();
        public UserDetailEntity UserDetail { get; set; } = new UserDetailEntity();
        public List<RoleEntity> Roles { get; set; }
    }
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

        public static UserConfig Config => UserConfig.Instance;

        public RbacManager()
        {  
            // 确保目录存在
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = $"DataSource={SqliteDbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
            });

            // 初始化表
            db.CodeFirst.InitTables<UserEntity, UserDetailEntity>();
            db.CodeFirst.InitTables<TenantEntity, UserTenantEntity>();
            db.CodeFirst.InitTables<RoleEntity, UserRoleEntity>();
            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            InitAdmin();

            EditCommand = new RelayCommand(a=> Edit());
            OpenUserManagerCommand = new RelayCommand(a => OpenUserManager());
            Authorization.Instance.PermissionMode = Config.UserLoginResult.UserDetail.PermissionMode;
        }

        public void OpenUserManager()
        {
            new UserManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public void Edit()
        {
            UserDetailEntity UserDetail = Config.UserLoginResult.UserDetail;
            new PropertyEditorWindow(UserDetail) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();

            UserDetail.UpdatedAt = DateTime.Now;
            db.Updateable(UserDetail).ExecuteCommand();
            Authorization.Instance.PermissionMode = UserDetail.PermissionMode;
        }


        public List<UserEntity> GetUsers()
        {
            var users = db.Queryable<UserEntity>().Where(u => u.IsDelete == false || u.IsDelete == null).ToList();
            return users;
        }

        private void InitAdmin()
        {
            // 检查是否已有用户
            if (!db.Queryable<UserEntity>().Any())
            {
                // 添加admin用户
                var adminUser = new UserEntity
                {
                    Username = "admin",
                    Password = "admin", // 推荐加密
                    IsEnable = true,
                    IsDelete = false,
                    Remark = "系统管理员"
                };
                db.Insertable(adminUser).ExecuteCommand();
            }

            // 检查是否已有admin角色
            var adminRole = db.Queryable<RoleEntity>().First(r => r.Code == "admin");
            if (adminRole == null)
            {
                adminRole = new RoleEntity
                {
                    Name = "管理员",
                    Code = "admin",
                    Remark = "系统管理员角色",
                    IsEnable = true,
                    IsDelete = false
                };
                db.Insertable(adminRole).ExecuteCommand();
            }

            // 建立admin用户和admin角色关联
            var adminUserId = db.Queryable<UserEntity>().Where(u => u.Username == "admin").Select(u => u.Id).First();
            var adminRoleId = db.Queryable<RoleEntity>().Where(r => r.Name == "admin").Select(r => r.Id).First();
            // 检查是否已有关联
            var exists = db.Queryable<UserRoleEntity>().Any(ur => ur.UserId == adminUserId && ur.RoleId == adminRoleId);
            if (!exists)
            {
                var adminUserRole = new UserRoleEntity
                {
                    UserId = adminUserId,
                    RoleId = adminRoleId
                };
                db.Insertable(adminUserRole).ExecuteCommand();
            }
        }


        public UserLoginResult LoginAndGetDetail(string userName, string password)
        {
            var user = db.Queryable<UserEntity>()
                .Where(u => (u.Username == userName)
                            && u.Password == password
                            && u.IsEnable
                            && (u.IsDelete == false || u.IsDelete == null))
                .First();


            if (user == null)
                return null;

            // 获取用户详细信息
            var userDetail = db.Queryable<UserDetailEntity>().First(ud => ud.UserId == user.Id);
            if (userDetail == null)
            {
                userDetail = new UserDetailEntity();
                userDetail.UserId = user.Id;
                userDetail.Id = db.Insertable(userDetail).ExecuteReturnIdentity();
            }
            // 获取用户角色列表
            var roleIds = db.Queryable<UserRoleEntity>().Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToList();
            List<RoleEntity> roles = new List<RoleEntity>();
            if (roleIds.Count > 0)
            {
                roles = db.Queryable<RoleEntity>().Where(r => roleIds.Contains(r.Id)).ToList();
            }

            return new UserLoginResult
            {
                User = user,
                UserDetail = userDetail,
                Roles = roles
            };
        }


        public bool CreateUser(string username, string password, string remark = "", List<int> roleIds = null)
        {
            // 检查用户名是否已存在
            bool exists = db.Queryable<UserEntity>().Any(u => u.Username == username);
            if (exists)
                return false; // 用户名已存在

            var newUser = new UserEntity
            {
                Username = username,
                Password = password, // 实际建议加密
                IsEnable = true,
                IsDelete = false,
                Remark = remark,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            int userId = db.Insertable(newUser).ExecuteReturnIdentity();

            // 创建对应的详细信息表
            var userDetail = new UserDetailEntity
            {
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
                // 其他字段可补充
            };
            db.Insertable(userDetail).ExecuteCommand();

            // 分配角色（如果有）
            if (roleIds != null)
            {
                foreach (var roleId in roleIds)
                {
                    var ur = new UserRoleEntity
                    {
                        UserId = userId,
                        RoleId = roleId
                    };
                    db.Insertable(ur).ExecuteCommand();
                }
            }

            return true;
        }

        public void Dispose()
        {
            db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
