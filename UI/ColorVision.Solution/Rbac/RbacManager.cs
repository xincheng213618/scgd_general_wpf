using ColorVision.Common.MVVM;
using ColorVision.Rbac.Services;
using ColorVision.Rbac.Services.Auth;
using ColorVision.Rbac.Security;
using ColorVision.UI.Authorizations;
using SqlSugar;
using System.IO;
using System.Windows;
using ColorVision.Rbac.Entity;


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
        public RelayCommand OpenPermissionManagerCommand { get; set; }

        public RbacManagerConfig Config => RbacManagerConfig.Instance;
        
        // 核心服务
        public IAuthService AuthService { get; set; }
        public IUserService UserService { get; set; }
        public IPermissionService PermissionService { get; set; }
        public IAuditLogService AuditLogService { get; set; }
        
        // 新增服务
        public IRoleService RoleService { get; set; }
        public ITenantService TenantService { get; set; }
        public ISessionService SessionService { get; set; }
        public IPermissionChecker PermissionChecker { get; set; }

        // 后台服务
        private SessionCleanupService? _sessionCleanupService;

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
            db.CodeFirst.InitTables<PermissionEntity, RolePermissionEntity>();
            db.CodeFirst.InitTables<AuditLogEntity>();
            db.CodeFirst.InitTables<SessionEntity>(); // 新增会话表

            // 初始化服务
            AuditLogService = new AuditLogService(db);
            AuthService = new AuthService(db);
            UserService = new UserService(db);
            PermissionService = new PermissionService(db);
            
            // 初始化新增服务
            RoleService = new RoleService(db, AuditLogService);
            TenantService = new TenantService(db, AuditLogService);
            SessionService = new SessionService(db);
            PermissionChecker = new PermissionChecker(db);
            
            // 启动会话清理后台服务
            _sessionCleanupService = new SessionCleanupService(SessionService, TimeSpan.FromHours(1));
            
            EditUserDetailAction = new EditUserDetailAction(UserService);

            InitAdmin();
            // 种子权限
            PermissionService.EnsureSeedAsync().GetAwaiter().GetResult();
            SeedRolePermissions();

            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            EditCommand = new RelayCommand(a=> EditUserDetailAction.EditAsync(), a => IsUserLoggedIn());
            OpenUserManagerCommand = new RelayCommand(a => OpenUserManager());
            OpenPermissionManagerCommand = new RelayCommand(a => OpenPermissionManager());

            // 若已有登录缓存则同步权限
            if (Config.LoginResult?.UserDetail != null)
                Authorization.Instance.PermissionMode = Config.LoginResult.UserDetail.PermissionMode;
        }

        private void SeedRolePermissions()
        {
            // 为 admin 角色赋予全部权限
            var adminRole = db.Queryable<RoleEntity>().First(r => r.Code == "admin");
            if (adminRole == null) return;
            var allPermissions = db.Queryable<PermissionEntity>().Where(p=>p.IsDelete!=true && p.IsEnable).Select(p=>p.Id).ToList();
            var existing = db.Queryable<RolePermissionEntity>().Where(rp=>rp.RoleId == adminRole.Id).Select(rp=>rp.PermissionId).ToList();
            var toInsertIds = allPermissions.Where(id=>!existing.Contains(id)).ToList();
            if (toInsertIds.Count>0)
            {
                var list = toInsertIds.Select(pid=> new RolePermissionEntity{ RoleId = adminRole.Id, PermissionId = pid}).ToList();
                db.Insertable(list).ExecuteCommand();
            }
        }

        public void OpenUserManager()
        {
            // Check if user has admin permissions
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("只有管理员才能访问用户管理功能。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            new UserManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public void OpenPermissionManager()
        {
            // Check if user has admin permissions
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("只有管理员才能访问权限管理功能。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            new PermissionManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        /// <summary>
        /// 获取权限缓存统计信息
        /// </summary>
        public CacheStatistics GetPermissionCacheStatistics()
        {
            return ((PermissionChecker)PermissionChecker).GetCacheStatistics();
        }

        /// <summary>
        /// 立即清理过期会话
        /// </summary>
        public async Task CleanupExpiredSessionsNowAsync()
        {
            if (_sessionCleanupService != null)
            {
                await _sessionCleanupService.CleanupNowAsync();
            }
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

        public List<RoleEntity> GetUserRoles(int userId)
        {
            return db.Queryable<RoleEntity>()
                .InnerJoin<UserRoleEntity>((r, ur) => r.Id == ur.RoleId)
                .Where((r, ur) => ur.UserId == userId && r.IsDelete != true && r.IsEnable)
                .Select(r => r)
                .ToList();
        }

        public bool CreateRole(string name, string code, string remark = "")
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权创建角色。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var result = RoleService.CreateRoleAsync(name, code, remark).GetAwaiter().GetResult();
                return result;
            }
            catch (Exceptions.PermissionDeniedException ex)
            {
                MessageBox.Show(ex.Message, "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exceptions.RbacException ex)
            {
                MessageBox.Show($"创建角色失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建角色失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
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
            // 权限检查：仅管理员及以上（SuperAdministrator）可创建用户
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权创建新用户。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            var result = UserService.CreateUserAsync(username, password, remark, roleIds).GetAwaiter().GetResult();
            if (result)
            {
                try { AuditLogService.AddAsync(Config.LoginResult?.UserDetail?.UserId, Config.LoginResult?.User?.Username, "user.create", $"创建用户:{username}"); } catch { }
            }
            return result;
        }

        // 新增：更新用户角色
        public bool UpdateUserRoles(int userId, IEnumerable<int> roleIds)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权修改用户角色。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                db.Deleteable<UserRoleEntity>().Where(ur => ur.UserId == userId).ExecuteCommand();
                if (roleIds != null)
                {
                    var list = roleIds.Distinct().Select(rid => new UserRoleEntity { UserId = userId, RoleId = rid }).ToList();
                    if (list.Count > 0)
                        db.Insertable(list).ExecuteCommand();
                }
                // 安全地记录审计日志，避免未登录时出错
                try 
                { 
                    if (Config.LoginResult?.UserDetail?.UserId != null && Config.LoginResult?.User?.Username != null)
                    {
                        AuditLogService.AddAsync(Config.LoginResult.UserDetail.UserId, Config.LoginResult.User.Username, "user.role.update", $"设置用户{userId}角色:{string.Join(',', roleIds ?? Array.Empty<int>())}"); 
                    }
                } 
                catch { }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新用户角色失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 检查用户是否已登录（包括通过记住我功能自动登录的情况）
        /// </summary>
        private bool IsUserLoggedIn()
        {
            return Config.LoginResult != null && 
                   Config.LoginResult.User != null && 
                   Config.LoginResult.UserDetail != null;
        }

        public void Dispose()
        {
            _sessionCleanupService?.Dispose();
            db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
