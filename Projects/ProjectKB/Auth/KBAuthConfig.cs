using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;

namespace ProjectKB.Auth
{
    public class KBAuthConfig : IConfig
    {
        public static KBAuthConfig Instance => ConfigService.Instance.GetRequiredService<KBAuthConfig>();

        /// <summary>
        /// 管理员密码的SHA256哈希值
        /// </summary>
        public string AdminPasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 密码盐值
        /// </summary>
        public string AdminPasswordSalt { get; set; } = string.Empty;

        /// <summary>
        /// 空闲超时时间（分钟），0表示不超时
        /// </summary>
        public int IdleTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// 是否已初始化（首次使用时设置默认密码）
        /// </summary>
        public bool IsInitialized { get; set; }

        [JsonIgnore]
        private const string DefaultPassword = "admin";

        /// <summary>
        /// 初始化默认密码（仅首次）
        /// </summary>
        public void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                SetPassword(DefaultPassword);
                IsInitialized = true;
                ConfigService.Instance.SaveConfigs();
            }
        }

        /// <summary>
        /// 验证密码是否正确
        /// </summary>
        public bool VerifyPassword(string password)
        {
            if (string.IsNullOrEmpty(AdminPasswordHash) || string.IsNullOrEmpty(AdminPasswordSalt))
                return false;

            string hashed = HashPassword(password, AdminPasswordSalt);
            return string.Equals(hashed, AdminPasswordHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 设置新密码
        /// </summary>
        public void SetPassword(string newPassword)
        {
            AdminPasswordSalt = GenerateSalt();
            AdminPasswordHash = HashPassword(newPassword, AdminPasswordSalt);
        }

        /// <summary>
        /// 修改密码（验证旧密码后设置新密码）
        /// </summary>
        public bool ChangePassword(string oldPassword, string newPassword)
        {
            if (!VerifyPassword(oldPassword))
                return false;

            SetPassword(newPassword);
            ConfigService.Instance.SaveConfigs();
            return true;
        }

        private static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        private static string HashPassword(string password, string salt)
        {
            return Cryptography.GetSha256Hash(password + salt);
        }
    }
}
