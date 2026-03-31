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

            try
            {
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
                db.CodeFirst.InitTables<SessionEntity>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RBAC 数据库初始化失败: {ex.Message}\n\n数据库路径: {SqliteDbPath}", 
                    "数据库错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

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
            SeedPermissionsAndRoles();

            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            EditCommand = new RelayCommand(a=> EditUserDetailAction.EditAsync(), a => IsUserLoggedIn());
            OpenUserManagerCommand = new RelayCommand(a => OpenUserManager());
            OpenPermissionManagerCommand = new RelayCommand(a => OpenPermissionManager());

            // 若已有登录缓存则同步权限
            if (Config.LoginResult?.UserDetail != null)
                Authorization.Instance.PermissionMode = Config.LoginResult.UserDetail.PermissionMode;
        }

        private void SeedPermissionsAndRoles()
        {
            // 种子权限（同步执行，仅在初始化时调用一次）
            PermissionService.EnsureSeed();

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
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("只有管理员才能访问用户管理功能。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            new UserManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public void OpenPermissionManager()
        {
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
            return db.Queryable<UserEntity>().Where(u => u.IsDelete == false || u.IsDelete == null).ToList();
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

        public async Task<bool> CreateRoleAsync(string name, string code, string remark = "")
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权创建角色。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var result = await RoleService.CreateRoleAsync(name, code, remark);
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

        public async Task<bool> CreateUserAsync(string username, string password, string remark = "", List<int>? roleIds = null)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权创建新用户。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            var result = await UserService.CreateUserAsync(username, password, remark, roleIds);
            if (result)
            {
                try
                {
                    await AuditLogService.AddAsync(
                        Config.LoginResult?.UserDetail?.UserId,
                        Config.LoginResult?.User?.Username,
                        "user.create",
                        $"创建用户:{username}");
                }
                catch { }
            }
            return result;
        }

        public async Task<bool> UpdateUserRolesAsync(int userId, IEnumerable<int> roleIds)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权修改用户角色。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                var result = await UserService.UpdateUserRolesAsync(userId, roleIds);
                if (result)
                {
                    try
                    {
                        await AuditLogService.AddAsync(
                            Config.LoginResult?.UserDetail?.UserId,
                            Config.LoginResult?.User?.Username,
                            "user.role.update",
                            $"设置用户{userId}角色:{string.Join(',', roleIds ?? Array.Empty<int>())}");
                    }
                    catch { }
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新用户角色失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 检查用户是否已登录
        /// </summary>
        public bool IsUserLoggedIn()
        {
            return Config.LoginResult != null && 
                   Config.LoginResult.User != null && 
                   Config.LoginResult.UserDetail != null;
        }

        public async Task<bool> DeleteUserAsync(int userId, string username)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权删除用户。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (Config.LoginResult?.User?.Id == userId)
            {
                MessageBox.Show("不能删除当前登录的用户。", "操作拒绝", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                var result = await UserService.DeleteUserAsync(userId);
                if (result)
                {
                    try
                    {
                        await AuditLogService.AddAsync(
                            Config.LoginResult?.UserDetail?.UserId,
                            Config.LoginResult?.User?.Username,
                            "user.delete",
                            $"删除用户:{username}(ID:{userId})");
                    }
                    catch { }
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除用户失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<bool> EnableUserAsync(int userId, string username)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权启用/禁用用户。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                var result = await UserService.EnableUserAsync(userId);
                if (result)
                {
                    try
                    {
                        await AuditLogService.AddAsync(
                            Config.LoginResult?.UserDetail?.UserId,
                            Config.LoginResult?.User?.Username,
                            "user.enable",
                            $"启用用户:{username}(ID:{userId})");
                    }
                    catch { }
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启用用户失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<bool> DisableUserAsync(int userId, string username)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权启用/禁用用户。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (Config.LoginResult?.User?.Id == userId)
            {
                MessageBox.Show("不能禁用当前登录的用户。", "操作拒绝", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                var result = await UserService.DisableUserAsync(userId);
                if (result)
                {
                    try
                    {
                        await AuditLogService.AddAsync(
                            Config.LoginResult?.UserDetail?.UserId,
                            Config.LoginResult?.User?.Username,
                            "user.disable",
                            $"禁用用户:{username}(ID:{userId})");
                    }
                    catch { }
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"禁用用户失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<string?> ResetUserPasswordAsync(int userId, string username)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("当前用户无权重置密码。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            try
            {
                var newPassword = await UserService.ResetUserPasswordAsync(userId);
                if (newPassword != null)
                {
                    try
                    {
                        await AuditLogService.AddAsync(
                            Config.LoginResult?.UserDetail?.UserId,
                            Config.LoginResult?.User?.Username,
                            "user.password.reset",
                            $"重置用户密码:{username}(ID:{userId})");
                    }
                    catch { }
                }
                return newPassword;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重置密码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public void Dispose()
        {
            _sessionCleanupService?.Dispose();
            db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
