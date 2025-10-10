#r "bin/Debug/net8.0-windows/LicenseGenerator.dll"
#r "nuget: Newtonsoft.Json, 13.0.3"

using LicenseGenerator;
using System;
using System.Text;
using Newtonsoft.Json;

Console.WriteLine("=== 增强型许可证测试 ===\n");

// 测试 1: 生成和验证增强许可证
Console.WriteLine("测试 1: 生成和验证增强许可证");

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
Console.WriteLine($"  许可证前80字符:\n  {license.Substring(0, Math.Min(80, license.Length))}...");

// 验证许可证
bool isValid = LicenseHelper.VerifyEnhancedLicense(license, machineCode);
Console.WriteLine($"  验证结果: {(isValid ? "通过 ✓" : "失败 ✗")}");

// 解析许可证
var parsedLicense = LicenseHelper.ParseEnhancedLicense(license);
if (parsedLicense != null)
{
    Console.WriteLine($"  客户名称: {parsedLicense.Licensee}");
    Console.WriteLine($"  设备型号: {parsedLicense.DeviceMode}");
    Console.WriteLine($"  签发机构: {parsedLicense.IssuingAuthority}");
    Console.WriteLine($"  过期日期: {parsedLicense.ExpiryDateTime:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"  剩余天数: {parsedLicense.GetRemainingDays()}");
    Console.WriteLine($"  是否过期: {(parsedLicense.IsExpired() ? "是" : "否")}");
    
    // 解码并显示 JSON
    byte[] jsonBytes = Convert.FromBase64String(license);
    string jsonStr = Encoding.UTF8.GetString(jsonBytes);
    var jsonObj = JsonConvert.DeserializeObject<dynamic>(jsonStr);
    string formattedJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
    Console.WriteLine($"\n  JSON 内容:\n{formattedJson}");
}

Console.WriteLine($"\n测试结果: {(isValid ? "成功 ✓" : "失败 ✗")}\n");

// 测试 2: 测试过期许可证
Console.WriteLine("测试 2: 过期许可证验证");
DateTime expiredDate = DateTime.Now.AddDays(-1);
string expiredLicense = LicenseHelper.CreateEnhancedLicense(
    machineCode,
    licensee,
    deviceMode,
    expiredDate,
    issuingAuthority
);
bool expiredValid = LicenseHelper.VerifyEnhancedLicense(expiredLicense, machineCode);
Console.WriteLine($"  验证结果: {(expiredValid ? "通过 ✓" : "失败 ✗")}");
Console.WriteLine($"  期望结果: 失败 ✗");
Console.WriteLine($"  测试结果: {(!expiredValid ? "成功（正确识别过期）✓" : "失败（未识别过期）✗")}\n");

// 测试 3: 机器码不匹配
Console.WriteLine("测试 3: 机器码不匹配");
string wrongMachineCode = "61626364"; // "abcd" 的十六进制
bool mismatchValid = LicenseHelper.VerifyEnhancedLicense(license, wrongMachineCode);
Console.WriteLine($"  验证结果: {(mismatchValid ? "通过 ✓" : "失败 ✗")}");
Console.WriteLine($"  期望结果: 失败 ✗");
Console.WriteLine($"  测试结果: {(!mismatchValid ? "成功（正确识别机器码不匹配）✓" : "失败（未识别机器码不匹配）✗")}\n");

Console.WriteLine("=== 测试完成 ===");
