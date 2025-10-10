using System;
using System.Text;
using Newtonsoft.Json;

namespace LicenseGenerator
{
    public class ManualTest
    {
        public static void TestEnhancedLicense()
        {
            Console.WriteLine("=== 增强型许可证手动测试 ===\n");
            
            string machineCode = LicenseHelper.GetMachineCode();
            Console.WriteLine($"当前机器码: {machineCode}\n");

            // 测试 1: 生成和验证增强许可证
            Console.WriteLine("测试 1: 生成和验证增强许可证");
            
            string licensee = "测试公司";
            string deviceMode = "CV-CAM-1000";
            DateTime expiryDate = DateTime.Now.AddYears(1);
            string issuingAuthority = "ColorVision";

            string license = LicenseHelper.CreateEnhancedLicense(
                machineCode,
                licensee,
                deviceMode,
                expiryDate,
                issuingAuthority
            );

            Console.WriteLine($"  许可证长度: {license.Length}");
            Console.WriteLine($"  许可证前100字符:\n  {license.Substring(0, Math.Min(100, license.Length))}...\n");

            bool isValid = LicenseHelper.VerifyEnhancedLicense(license, machineCode);
            Console.WriteLine($"  验证结果: {(isValid ? "通过 ✓" : "失败 ✗")}");

            var parsed = LicenseHelper.ParseEnhancedLicense(license);
            if (parsed != null)
            {
                Console.WriteLine($"  客户名称: {parsed.Licensee}");
                Console.WriteLine($"  设备型号: {parsed.DeviceMode}");
                Console.WriteLine($"  签发机构: {parsed.IssuingAuthority}");
                Console.WriteLine($"  过期日期: {parsed.ExpiryDateTime:yyyy-MM-dd}");
                Console.WriteLine($"  剩余天数: {parsed.GetRemainingDays()}");
                
                byte[] jsonBytes = Convert.FromBase64String(license);
                string jsonStr = Encoding.UTF8.GetString(jsonBytes);
                var jsonObj = JsonConvert.DeserializeObject<dynamic>(jsonStr);
                string formattedJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                Console.WriteLine($"\n  JSON 内容:\n{formattedJson}");
            }

            Console.WriteLine($"\n  结果: {(isValid ? "成功 ✓" : "失败 ✗")}\n");
        }
    }
}
