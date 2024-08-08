using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;


namespace ColorVision.UI.ACE
{
    public class License
    {
        private static readonly string publicKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private static readonly string privateKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent><P>/OfgYc6H7sSiFUrwkTVtQEyuSm309+Whwuvuul/3zLkNJlvorGC2D5ksTz3Q0XFehHWgWNc0jQ3MRyKp2EHxgw==</P><Q>6ZrTQbe25FVr92pxAlBeO1iONdbLRM+/VmuwrZVgeHvu++8ChAidQT13rcVfqvLDuGq5/q2bgQgmraqdgRNIew==</Q><DP>0sEQ1bDcyncGcyQOMZQKRSkhnVjgaaztDpi6Sooq4GndsXep/+xgC8Ojjy1+VOtazpuPUjmUy28SKr2SOGtLrQ==</DP><DQ>b7mMsDGdVzdDm+Fciy7E4r1HxpgkP5TcfgijR2HZ8cXUVsnI+jzkeP9c7c8oIipZUSo6KoP9i4jKduTSz5jZYQ==</DQ><InverseQ>2kXWXpMpHplGwG/eHR17tVNyfaxjl2Hu2QWnlg5Jf/vLDMcA9MspGS5mS5uCNTTPh34T9PEtmCdA5L5i8kakwg==</InverseQ><D>EmVOzr0PyzX6IXn0ecjaKcUodBEaJcqpgwY3aYZJxCjs+2GFzQLO6qFhxBPFl9MIPrao04jVfjrk9ZEpZByWvUmq79tlzpBjeZW2wcjeUrZYK0/b0D7NRelf6InSJaOb9QKw/hhSPsl3x+nXPyhUFfz6q8bThGDSriC/eb3aSyE=</D></RSAKeyValue>";

        private static string GetCompanyName()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var companyAttribute = (AssemblyCompanyAttribute)assembly
                .GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)
                .FirstOrDefault();
            return companyAttribute?.Company ?? "ColorVision";  
        }

        /// <summary>
        /// 检测
        /// </summary>
        /// <returns></returns>
        public static bool Check()
        {
            string[] paths = {
                "license",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), GetCompanyName(), "license")
            };
            return paths.Any(path => File.Exists(path) && Check(File.ReadAllText(path)));
        }

        public static bool Check(string lisense)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            //导入公钥，准备验证签名
            rsa.FromXmlString(publicKeyXml);
            return lisense.Length==172 && rsa.VerifyData(Encoding.UTF8.GetBytes(GetMachineCode()), "MD5", Convert.FromBase64String(lisense));
        }

        /// <summary>
        /// 获取机器码并加密
        /// </summary>
        /// <returns></returns>
        public static string GetMachineCode()
        {
            byte[] code = Encoding.UTF8.GetBytes(Environment.MachineName);
            string Reg="";
            
            foreach (byte a in code)
            {
                Reg += a.ToString("x2");
            }
            return Reg;
        }
        public static void Create() {

            string ActivationCode =  Create(GetMachineCode());
            string LicensePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{GetCompanyName()}";
            if (!Directory.Exists(LicensePath))
            {
                Directory.CreateDirectory(LicensePath);
            }
            File.WriteAllText(LicensePath + "\\license", ActivationCode);
        } 
        public static string Create(string MachineCode)
        {
            return Sign(MachineCode, privateKeyXml);
        }
        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="Text"></param>
        /// <param name="PrivateKey"></param>
        /// <returns></returns>
        public static string Sign(string Text, string PrivateKey)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(PrivateKey);
            return Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(Text), "MD5"));
        }
    }
   
}
