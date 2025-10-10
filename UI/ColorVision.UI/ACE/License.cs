using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.ACE
{
    /// <summary>
    /// License 验证和管理类
    /// 提供基于 RSA 签名的许可证验证功能
    /// </summary>
    public class License
    {
        /// <summary>
        /// RSA 公钥（仅用于验证许可证签名）
        /// </summary>
        private static readonly string publicKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        
        /// <summary>
        /// 许可证签名的预期长度（Base64 编码）
        /// </summary>
        private const int ExpectedLicenseLength = 344; // SHA256 signature length in base64

        /// <summary>
        /// 获取公司名称，用于确定许可证文件路径
        /// </summary>
        /// <returns>公司名称，默认为 "ColorVision"</returns>
        private static string GetCompanyName()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var companyAttribute = (AssemblyCompanyAttribute?)assembly
                .GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)
                .FirstOrDefault();
            return companyAttribute?.Company ?? "ColorVision";
        }

        /// <summary>
        /// 检测许可证是否存在且有效
        /// 按优先级检查多个可能的许可证文件位置
        /// </summary>
        /// <returns>如果找到有效许可证返回 true，否则返回 false</returns>
        public static bool Check()
        {
            string[] paths = {
                "license",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), GetCompanyName(), "license")
            };
            
            return paths.Any(path => 
            {
                try
                {
                    return File.Exists(path) && Check(File.ReadAllText(path));
                }
                catch
                {
                    // 如果读取文件失败，继续检查下一个路径
                    return false;
                }
            });
        }

        /// <summary>
        /// 验证许可证字符串是否有效
        /// </summary>
        /// <param name="license">Base64 编码的许可证签名字符串</param>
        /// <returns>如果许可证有效返回 true，否则返回 false</returns>
        public static bool Check(string license)
        {
            if (string.IsNullOrWhiteSpace(license))
            {
                return false;
            }

            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    // 导入公钥，准备验证签名
                    rsa.FromXmlString(publicKeyXml);
                    
                    byte[] machineCodeBytes = Encoding.UTF8.GetBytes(GetMachineCode());
                    byte[] signatureBytes = Convert.FromBase64String(license);
                    
                    // 使用 SHA256 替代已弃用的 MD5
                    return rsa.VerifyData(machineCodeBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception)
            {
                // 验证失败（格式错误、签名无效等）
                return false;
            }
        }

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
        /// 创建并保存许可证文件到默认位置
        /// 注意：此方法需要私钥，仅供许可证生成工具使用
        /// </summary>
        /// <param name="privateKeyXml">RSA 私钥（XML 格式）</param>
        /// <exception cref="ArgumentNullException">当私钥为空时抛出</exception>
        /// <exception cref="UnauthorizedAccessException">当无法创建目录或写入文件时抛出</exception>
        public static void Create(string privateKeyXml)
        {
            if (string.IsNullOrWhiteSpace(privateKeyXml))
            {
                throw new ArgumentNullException(nameof(privateKeyXml), "私钥不能为空");
            }

            string activationCode = Create(GetMachineCode(), privateKeyXml);
            string licensePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                GetCompanyName()
            );
            
            if (!Directory.Exists(licensePath))
            {
                Directory.CreateDirectory(licensePath);
            }
            
            File.WriteAllText(Path.Combine(licensePath, "license"), activationCode);
        }

        /// <summary>
        /// 为指定的机器码创建许可证签名
        /// </summary>
        /// <param name="machineCode">机器码</param>
        /// <param name="privateKeyXml">RSA 私钥（XML 格式）</param>
        /// <returns>Base64 编码的许可证签名字符串</returns>
        /// <exception cref="ArgumentNullException">当参数为空时抛出</exception>
        public static string Create(string machineCode, string privateKeyXml)
        {
            if (string.IsNullOrWhiteSpace(machineCode))
            {
                throw new ArgumentNullException(nameof(machineCode), "机器码不能为空");
            }
            
            if (string.IsNullOrWhiteSpace(privateKeyXml))
            {
                throw new ArgumentNullException(nameof(privateKeyXml), "私钥不能为空");
            }

            return Sign(machineCode, privateKeyXml);
        }

        /// <summary>
        /// 使用 RSA 私钥对文本进行签名
        /// </summary>
        /// <param name="text">要签名的文本</param>
        /// <param name="privateKey">RSA 私钥（XML 格式）</param>
        /// <returns>Base64 编码的签名字符串</returns>
        /// <exception cref="ArgumentNullException">当参数为空时抛出</exception>
        public static string Sign(string text, string privateKey)
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
