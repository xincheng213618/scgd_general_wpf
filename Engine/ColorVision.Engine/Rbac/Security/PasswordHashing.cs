using System;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Rbac.Security
{
    // 存储格式：PBKDF2$HMACSHA256$<iterations>$<saltBase64>$<hashBase64>
    public static class PasswordHasher
    {
        private const int DefaultIterations = 100_000;
        private const int SaltSize = 16; // 128-bit
        private const int KeySize = 32;  // 256-bit
        private const string Prefix = "PBKDF2$HMACSHA256$";

        public static string Hash(string password, int? iterations = null)
        {
            var iters = iterations ?? DefaultIterations;
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iters,
                HashAlgorithmName.SHA256,
                KeySize);

            return $"{Prefix}{iters}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string stored, out bool needsUpgrade)
        {
            needsUpgrade = false;

            if (string.IsNullOrWhiteSpace(stored))
                return false;

            // 非标准格式 => 认为是历史明文，登录成功后触发升级
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                var ok = TimingSafeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(password));
                needsUpgrade = ok;
                return ok;
            }

            try
            {
                // PBKDF2$HMACSHA256$iters$salt$hash
                var parts = stored.Split('$');
                if (parts.Length != 5) return false;

                var iters = int.Parse(parts[2]);
                var salt = Convert.FromBase64String(parts[3]);
                var hash = Convert.FromBase64String(parts[4]);

                var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iters, HashAlgorithmName.SHA256, hash.Length);

                var ok = TimingSafeEquals(key, hash);
                // 低于当前推荐迭代数则触发升级
                needsUpgrade = ok && iters < DefaultIterations;
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private static bool TimingSafeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
