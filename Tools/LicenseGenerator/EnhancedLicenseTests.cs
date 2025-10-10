using System;
using System.Text;
using Newtonsoft.Json;

namespace LicenseGenerator.Tests
{
    /// <summary>
    /// 测试增强型许可证生成和验证
    /// </summary>
    public class EnhancedLicenseTests
    {
        public static void RunTests()
        {
            Console.WriteLine("=== 增强型许可证测试 ===\n");

            // 测试 1: 生成和验证增强许可证
            Test1_CreateAndVerifyEnhancedLicense();

            // 测试 2: 测试过期许可证
            Test2_ExpiredLicense();

            // 测试 3: 测试机器码不匹配
            Test3_MachineCodeMismatch();

            // 测试 4: 解析许可证信息
            Test4_ParseLicenseInfo();

            Console.WriteLine("\n=== 测试完成 ===");
        }

        private static void Test1_CreateAndVerifyEnhancedLicense()
        {
            Console.WriteLine("测试 1: 生成和验证增强许可证");
            
            try
            {
                string machineCode = "74657374"; // "test" 的十六进制
                string licensee = "测试公司";
                string deviceMode = "CV-CAM-1000";
                DateTime expiryDate = DateTime.Now.AddYears(1);
                string issuingAuthority = "ColorVision";

                // 生成许可证
                string license = LicenseHelper.CreateEnhancedLicense(
                    machineCode,
                    licensee,
                    deviceMode,
                    expiryDate,
                    issuingAuthority
                );

                Console.WriteLine($"  生成的许可证长度: {license.Length}");
                Console.WriteLine($"  许可证前50字符: {license.Substring(0, Math.Min(50, license.Length))}...");

                // 验证许可证
                bool isValid = LicenseHelper.VerifyEnhancedLicense(license, machineCode);
                Console.WriteLine($"  验证结果: {(isValid ? "通过 ✓" : "失败 ✗")}");

                // 解析许可证
                var parsedLicense = LicenseHelper.ParseEnhancedLicense(license);
                if (parsedLicense != null)
                {
                    Console.WriteLine($"  客户名称: {parsedLicense.Licensee}");
                    Console.WriteLine($"  设备型号: {parsedLicense.DeviceMode}");
                    Console.WriteLine($"  剩余天数: {parsedLicense.GetRemainingDays()}");
                }

                Console.WriteLine($"  结果: {(isValid ? "成功" : "失败")}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  错误: {ex.Message}\n");
            }
        }

        private static void Test2_ExpiredLicense()
        {
            Console.WriteLine("测试 2: 过期许可证验证");
            
            try
            {
                string machineCode = "74657374";
                string licensee = "测试公司";
                string deviceMode = "CV-CAM-1000";
                DateTime expiryDate = DateTime.Now.AddDays(-1); // 昨天过期
                string issuingAuthority = "ColorVision";

                // 生成过期许可证
                string license = LicenseHelper.CreateEnhancedLicense(
                    machineCode,
                    licensee,
                    deviceMode,
                    expiryDate,
                    issuingAuthority
                );

                // 验证许可证（应该失败，因为已过期）
                bool isValid = LicenseHelper.VerifyEnhancedLicense(license, machineCode);
                Console.WriteLine($"  验证结果: {(isValid ? "通过 ✓" : "失败 ✗")}");
                Console.WriteLine($"  期望结果: 失败 ✗");
                Console.WriteLine($"  结果: {(!isValid ? "成功（正确识别过期）" : "失败（未识别过期）")}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  错误: {ex.Message}\n");
            }
        }

        private static void Test3_MachineCodeMismatch()
        {
            Console.WriteLine("测试 3: 机器码不匹配");
            
            try
            {
                string machineCode = "74657374";
                string wrongMachineCode = "61626364"; // "abcd" 的十六进制
                string licensee = "测试公司";
                string deviceMode = "CV-CAM-1000";
                DateTime expiryDate = DateTime.Now.AddYears(1);
                string issuingAuthority = "ColorVision";

                // 为 machineCode 生成许可证
                string license = LicenseHelper.CreateEnhancedLicense(
                    machineCode,
                    licensee,
                    deviceMode,
                    expiryDate,
                    issuingAuthority
                );

                // 使用错误的机器码验证（应该失败）
                bool isValid = LicenseHelper.VerifyEnhancedLicense(license, wrongMachineCode);
                Console.WriteLine($"  验证结果: {(isValid ? "通过 ✓" : "失败 ✗")}");
                Console.WriteLine($"  期望结果: 失败 ✗");
                Console.WriteLine($"  结果: {(!isValid ? "成功（正确识别机器码不匹配）" : "失败（未识别机器码不匹配）")}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  错误: {ex.Message}\n");
            }
        }

        private static void Test4_ParseLicenseInfo()
        {
            Console.WriteLine("测试 4: 解析许可证信息");
            
            try
            {
                string machineCode = "74657374";
                string licensee = "示例公司";
                string deviceMode = "CV-CAM-2000";
                DateTime expiryDate = new DateTime(2025, 12, 31);
                string issuingAuthority = "ColorVision Official";

                // 生成许可证
                string license = LicenseHelper.CreateEnhancedLicense(
                    machineCode,
                    licensee,
                    deviceMode,
                    expiryDate,
                    issuingAuthority
                );

                // 解析许可证
                var parsed = LicenseHelper.ParseEnhancedLicense(license);
                if (parsed != null)
                {
                    Console.WriteLine($"  被许可人: {parsed.Licensee}");
                    Console.WriteLine($"  设备型号: {parsed.DeviceMode}");
                    Console.WriteLine($"  签发机构: {parsed.IssuingAuthority}");
                    Console.WriteLine($"  签发日期: {parsed.IssueDateDateTime:yyyy-MM-dd}");
                    Console.WriteLine($"  过期日期: {parsed.ExpiryDateTime:yyyy-MM-dd}");
                    Console.WriteLine($"  剩余天数: {parsed.GetRemainingDays()}");
                    Console.WriteLine($"  是否过期: {(parsed.IsExpired() ? "是" : "否")}");

                    // 解码并显示 JSON
                    byte[] jsonBytes = Convert.FromBase64String(license);
                    string jsonStr = Encoding.UTF8.GetString(jsonBytes);
                    var jsonObj = JsonConvert.DeserializeObject<dynamic>(jsonStr);
                    string formattedJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                    Console.WriteLine($"\n  JSON 内容:\n{formattedJson}");

                    Console.WriteLine($"\n  结果: 成功\n");
                }
                else
                {
                    Console.WriteLine($"  结果: 失败（无法解析）\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  错误: {ex.Message}\n");
            }
        }
    }
}
