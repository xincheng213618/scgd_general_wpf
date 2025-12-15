using ColorVision.Rbac.Dtos;
using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Security;
using SqlSugar;

namespace ColorVision.Rbac.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ISqlSugarClient _db;

        public AuthService(ISqlSugarClient db)
        {
            _db = db;
        }

        public async Task<LoginResultDto?> LoginAndGetDetailAsync(string userName, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
                return null;

            var normalized = userName.Trim();
            var utcNow = DateTimeOffset.Now;

            // 1) 取用户（不包含软删除，启用状态）
            var user = await _db.Queryable<UserEntity>()
                .Where(u => u.Username == normalized && u.IsEnable && u.IsDelete != true)
                .FirstAsync(ct);

            if (user == null)
                return null;

            // 2) 校验密码（支持老明文在线迁移）
            var valid = PasswordHasher.Verify(password, user.Password, out var needsUpgrade);
            if (!valid)
                return null;

            // 3) 事务：确保用户详情存在、更新时间；查询角色
            await _db.Ado.BeginTranAsync();
            try
            {
                // 如果密码需要升级（包括历史明文或迭代数太低），这里统一升级到 PBKDF2
                if (needsUpgrade)
                {
                    var newHash = PasswordHasher.Hash(password);
                    await _db.Updateable<UserEntity>()
                        .SetColumns(u => new UserEntity { Password = newHash, UpdatedAt = utcNow })
                        .Where(u => u.Id == user.Id)
                        .ExecuteCommandAsync(ct);
                    user.Password = newHash;
                    user.UpdatedAt = utcNow;
                }

                // 确保 user_detail 存在（幂等 upsert）
                var storage = _db.Storageable(new UserDetailEntity
                {
                    UserId = user.Id,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                })
                .WhereColumns(it => new { it.UserId }) // 以 UserId 作为唯一键
                .ToStorage();

                if (storage.InsertList.Count != 0)
                {
                    await storage.AsInsertable.ExecuteCommandAsync(ct);
                }

                // 取详情
                var userDetail = await _db.Queryable<UserDetailEntity>()
                    .FirstAsync(ud => ud.UserId == user.Id, ct);

                // 更新最后活跃时间
                await _db.Updateable<UserDetailEntity>()
                    .SetColumns(ud => new UserDetailEntity { UpdatedAt = utcNow })
                    .Where(ud => ud.UserId == user.Id)
                    .ExecuteCommandAsync(ct);

                // 拉取角色（一次 Join）
                var roles = await _db.Queryable<RoleEntity>()
                    .InnerJoin<UserRoleEntity>((r, ur) => r.Id == ur.RoleId)
                    .Where((r, ur) => ur.UserId == user.Id)
                    .Select((r, ur) => new RoleDto { Id = r.Id, Name = r.Name })
                    .ToListAsync(ct);

                await _db.Ado.CommitTranAsync();

                // 4) 构造精简返回（不包含密码）
                return new LoginResultDto
                {
                    User = new UserSummaryDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        IsEnable = user.IsEnable,
                        CreatedAt = user.CreatedAt,
                        UpdatedAt = user.UpdatedAt
                    },
                    UserDetail = new UserDetailDto
                    {
                        UserId = userDetail.UserId,
                        Email = userDetail.Email,
                        Phone = userDetail.Phone,
                        Address = userDetail.Address,
                        Company = userDetail.Company,
                        Department = userDetail.Department,
                        Position = userDetail.Position,
                        Remark = userDetail.Remark,
                        UserImage = userDetail.UserImage,
                        PermissionMode = userDetail.PermissionMode,
                        CreatedAt = userDetail.CreatedAt,
                        UpdatedAt = utcNow
                    },
                    Roles = roles
                };
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
        }
    }
}
