using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Digests;

namespace ColorVision.User
{
    public class PasswordHashing
    {
        // 生成盐值
        public static byte[] GenerateSalt()
        {
            var random = new SecureRandom();
            byte[] salt = new byte[16]; // 128-bit salt
            random.NextBytes(salt);
            return salt;
        }

        // 使用SHA256散列密码和盐
        public static byte[] HashPassword(string password, byte[] salt)
        {
            // 将密码字符串转换为字节
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            // 创建散列计算器
            IDigest digest = new Sha256Digest();
            digest.BlockUpdate(salt, 0, salt.Length);
            digest.BlockUpdate(passwordBytes, 0, passwordBytes.Length);

            // 输出散列值
            byte[] result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);

            return result;
        }

        // 验证密码是否正确
        public static bool VerifyPassword(string password, byte[] salt, byte[] hash)
        {
            // 对输入密码进行散列
            byte[] passwordHash = HashPassword(password, salt);

            // 比较散列值是否一致
            return ConstantTimeComparison(passwordHash, hash);
        }

        // 常数时间比较，防止时间攻击
        private static bool ConstantTimeComparison(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }
    }
}
