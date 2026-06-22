using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using System.IO;

namespace ProjectKB.Auth
{
    public class KBAuthConfig
    {
        public static KBAuthConfig Instance { get; } = new();

        /// <summary>
        /// 管理员账号
        /// </summary>
        public string AdminUserName { get; set; } = "admin";

        /// <summary>
        /// 是否启用ProjectKB权限控制；默认关闭，关闭时所有功能可用。
        /// </summary>
        public bool EnablePermissionControl { get; set; }

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

        public static string PasswordFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ProjectKB",
            "Security",
            "Auth",
            "kb.auth.json");

        [JsonIgnore]
        private bool _passwordFileLoaded;

        [JsonIgnore]
        internal const string DefaultUserName = "admin";

        [JsonIgnore]
        internal const string DefaultPassword = "admin";

        /// <summary>
        /// 初始化默认账号密码（仅首次）
        /// </summary>
        public void EnsureInitialized()
        {
            LoadPasswordFile();
            if (IsInitialized && (string.IsNullOrWhiteSpace(AdminPasswordHash) || string.IsNullOrWhiteSpace(AdminPasswordSalt)))
                IsInitialized = false;

            if (!IsInitialized)
            {
                AdminUserName = DefaultUserName;
                SetPassword(DefaultPassword);
                IsInitialized = true;
                SavePasswordFile();
                return;
            }

            if (string.IsNullOrWhiteSpace(AdminUserName))
            {
                AdminUserName = DefaultUserName;
                SavePasswordFile();
            }
        }

        /// <summary>
        /// 验证账号密码是否正确
        /// </summary>
        public bool VerifyCredentials(string userName, string password)
        {
            if (!string.Equals(userName?.Trim(), AdminUserName, StringComparison.OrdinalIgnoreCase))
                return false;

            return VerifyPassword(password);
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
            SavePasswordFile();
            return true;
        }

        /// <summary>
        /// 保存独立密码文件，避免删除主配置时影响数据库、设备和UI设置。
        /// </summary>
        internal void SavePasswordFile()
        {
            string? directory = Path.GetDirectoryName(PasswordFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var data = new PasswordFileData
            {
                AdminUserName = AdminUserName,
                EnablePermissionControl = EnablePermissionControl,
                AdminPasswordHash = AdminPasswordHash,
                AdminPasswordSalt = AdminPasswordSalt,
                IdleTimeoutMinutes = IdleTimeoutMinutes,
                IsInitialized = IsInitialized
            };
            File.WriteAllText(PasswordFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            TryHideFile(PasswordFilePath);
        }

        private void LoadPasswordFile()
        {
            if (_passwordFileLoaded)
                return;

            _passwordFileLoaded = true;

            if (!File.Exists(PasswordFilePath))
            {
                ResetToUninitializedDefault();
                return;
            }

            try
            {
                PasswordFileData? data = JsonConvert.DeserializeObject<PasswordFileData>(File.ReadAllText(PasswordFilePath));
                if (data != null)
                    Apply(data);
                else
                    ResetToUninitializedDefault();
            }
            catch
            {
                ResetToUninitializedDefault();
            }
        }

        private void Apply(PasswordFileData data)
        {
            AdminUserName = string.IsNullOrWhiteSpace(data.AdminUserName) ? DefaultUserName : data.AdminUserName;
            EnablePermissionControl = data.EnablePermissionControl;
            AdminPasswordHash = data.AdminPasswordHash ?? string.Empty;
            AdminPasswordSalt = data.AdminPasswordSalt ?? string.Empty;
            IdleTimeoutMinutes = data.IdleTimeoutMinutes;
            IsInitialized = data.IsInitialized;
        }

        private void ResetToUninitializedDefault()
        {
            AdminUserName = DefaultUserName;
            EnablePermissionControl = false;
            AdminPasswordHash = string.Empty;
            AdminPasswordSalt = string.Empty;
            IdleTimeoutMinutes = 30;
            IsInitialized = false;
        }

        private static void TryHideFile(string filePath)
        {
            try
            {
                File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
            }
            catch
            {
            }
        }

        private sealed class PasswordFileData
        {
            public string AdminUserName { get; set; } = DefaultUserName;
            public bool EnablePermissionControl { get; set; }
            public string AdminPasswordHash { get; set; } = string.Empty;
            public string AdminPasswordSalt { get; set; } = string.Empty;
            public int IdleTimeoutMinutes { get; set; } = 30;
            public bool IsInitialized { get; set; }
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
