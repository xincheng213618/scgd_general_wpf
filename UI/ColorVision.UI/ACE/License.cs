using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;


namespace ColorVision.UI.ACE
{
    public class ColorVisionLicense
    {
        [JsonProperty("authority_signature")]
        public string AuthoritySignature { get; set; }

        [JsonProperty("device_mode")]
        public string DeviceMode { get; set; }

        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; }

        public DateTime ExpiryDateTime { get => ExpiryDate == null ? DateTime.Now : TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)).AddSeconds(int.Parse(ExpiryDate)); }

        [JsonProperty("issue_date")]
        public string IssueDate { get; set; }

        [JsonProperty("issuing_authority")]
        public string IssuingAuthority { get; set; }

        [JsonProperty("licensee")]
        public string Licensee { get; set; }

        [JsonProperty("licensee_signature")]
        public string LicenseeSignature { get; set; }
    }

    public class License
    {
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

        public static bool Check(string license)
        {
            if (string.IsNullOrWhiteSpace(license))
                return false;

            try
            {
                // Try to parse as enhanced license (Base64-encoded JSON)
                string licenseContent = Base64Decode(license);
                var enhancedLicense = JsonConvert.DeserializeObject<ColorVisionLicense>(licenseContent);
                
                if (enhancedLicense != null && !string.IsNullOrWhiteSpace(enhancedLicense.Licensee))
                {
                    // Check if license is expired
                    return enhancedLicense.ExpiryDateTime > DateTime.Now;
                }
            }
            catch
            {
                // If parsing fails, it's not a valid enhanced license
                return false;
            }

            return false;
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

        private static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
   
}
