using System.Security.Cryptography;
using System.Text;

namespace LicenseGenerator
{
    /// <summary>
    /// 许可证助手类 - 包含许可证生成和验证的核心逻辑
    /// 这是从 ColorVision.UI.ACE.License 类中提取的核心功能
    /// </summary>
    public static class LicenseHelper
    {
        /// <summary>
        /// RSA 公钥（用于验证许可证签名）
        /// </summary>
        private const string PublicKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>
        /// RSA 私钥（仅用于许可证生成，不应包含在客户端应用程序中）
        /// </summary>
        private const string PrivateKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent><P>/OfgYc6H7sSiFUrwkTVtQEyuSm309+Whwuvuul/3zLkNJlvorGC2D5ksTz3Q0XFehHWgWNc0jQ3MRyKp2EHxgw==</P><Q>6ZrTQbe25FVr92pxAlBeO1iONdbLRM+/VmuwrZVgeHvu++8ChAidQT13rcVfqvLDuGq5/q2bgQgmraqdgRNIew==</Q><DP>0sEQ1bDcyncGcyQOMZQKRSkhnVjgaaztDpi6Sooq4GndsXep/+xgC8Ojjy1+VOtazpuPUjmUy28SKr2SOGtLrQ==</DP><DQ>b7mMsDGdVzdDm+Fciy7E4r1HxpgkP5TcfgijR2HZ8cXUVsnI+jzkeP9c7c8oIipZUSo6KoP9i4jKduTSz5jZYQ==</DQ><InverseQ>2kXWXpMpHplGwG/eHR17tVNyfaxjl2Hu2QWnlg5Jf/vLDMcA9MspGS5mS5uCNTTPh34T9PEtmCdA5L5i8kakwg==</InverseQ><D>EmVOzr0PyzX6IXn0ecjaKcUodBEaJcqpgwY3aYZJxCjs+2GFzQLO6qFhxBPFl9MIPrao04jVfjrk9ZEpZByWvUmq79tlzpBjeZW2wcjeUrZYK0/b0D7NRelf6InSJaOb9QKw/hhSPsl3x+nXPyhUFfz6q8bThGDSriC/eb3aSyE=</D></RSAKeyValue>";

        /// <summary>
        /// 获取机器码（基于机器名的十六进制编码）
        /// </summary>
        /// <returns>机器码的十六进制字符串表示</returns>
        public static string GetMachineCode()
        {
            byte[] code = Encoding.UTF8.GetBytes(Environment.MachineName);
            StringBuilder reg = new StringBuilder(code.Length * 2);

            foreach (byte a in code)
            {
                reg.Append(a.ToString("x2"));
            }

            return reg.ToString();
        }

        /// <summary>
        /// 为指定的机器码创建许可证签名
        /// </summary>
        /// <param name="machineCode">机器码</param>
        /// <returns>Base64 编码的许可证签名字符串</returns>
        public static string CreateLicense(string machineCode)
        {
            if (string.IsNullOrWhiteSpace(machineCode))
            {
                throw new ArgumentNullException(nameof(machineCode), "机器码不能为空");
            }

            return SignData(machineCode, PrivateKeyXml);
        }

        /// <summary>
        /// 验证许可证字符串是否有效
        /// </summary>
        /// <param name="license">Base64 编码的许可证签名字符串</param>
        /// <param name="machineCode">要验证的机器码</param>
        /// <returns>如果许可证有效返回 true，否则返回 false</returns>
        public static bool VerifyLicense(string license, string machineCode)
        {
            if (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(machineCode))
            {
                return false;
            }

            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKeyXml);

                    byte[] machineCodeBytes = Encoding.UTF8.GetBytes(machineCode);
                    byte[] signatureBytes = Convert.FromBase64String(license);

                    // 使用 SHA256 替代已弃用的 MD5
                    return rsa.VerifyData(machineCodeBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 使用 RSA 私钥对文本进行签名
        /// </summary>
        /// <param name="text">要签名的文本</param>
        /// <param name="privateKey">RSA 私钥（XML 格式）</param>
        /// <returns>Base64 编码的签名字符串</returns>
        private static string SignData(string text, string privateKey)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "待签名文本不能为空");
            }

            if (string.IsNullOrWhiteSpace(privateKey))
            {
                throw new ArgumentNullException(nameof(privateKey), "私钥不能为空");
            }

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKey);
                byte[] dataBytes = Encoding.UTF8.GetBytes(text);
                // 使用 SHA256 替代已弃用的 MD5
                byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return Convert.ToBase64String(signatureBytes);
            }
        }
    }
}
