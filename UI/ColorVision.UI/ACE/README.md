# ColorVision.UI.ACE 模块

## 概述

ACE (Application Cryptography Engine) 模块提供了基于 RSA 数字签名的应用程序许可证验证功能。

## 功能特性

- ✅ **安全的许可证验证**: 使用 RSA + SHA256 数字签名
- ✅ **机器码绑定**: 许可证与机器唯一标识绑定
- ✅ **多路径检查**: 支持多个许可证文件位置
- ✅ **完整的文档**: XML 注释和使用指南

## 快速开始

### 验证许可证

```csharp
// 检查许可证是否存在且有效
bool isValid = License.Check();

if (isValid)
{
    Console.WriteLine("许可证有效");
}
else
{
    Console.WriteLine("许可证无效或不存在");
}
```

### 获取机器码

```csharp
// 获取当前机器的唯一标识
string machineCode = License.GetMachineCode();
Console.WriteLine($"机器码: {machineCode}");
```

### 生成许可证（需要私钥）

```csharp
// 注意：此操作需要 RSA 私钥，应在安全环境中执行
string privateKey = LoadPrivateKeySecurely(); // 从安全位置加载
string machineCode = "74657374"; // 目标机器码
string license = License.Create(machineCode, privateKey);

// 将许可证保存到文件或发送给客户
File.WriteAllText("license", license);
```

## 许可证文件位置

许可证会按以下优先级查找：

1. **当前目录**: `./license`
2. **应用数据目录**: `%APPDATA%\ColorVision\license`

## 安全性

### 已实施的安全措施

1. **私钥保护**: 私钥不再硬编码在源代码中
2. **现代加密**: 使用 SHA256 替代已弃用的 MD5
3. **资源管理**: 正确释放加密对象，防止内存泄漏
4. **输入验证**: 所有公共方法进行参数验证

### 安全最佳实践

- 🔐 **私钥存储**: 使用密钥管理服务（Azure Key Vault、AWS KMS 等）
- 🔒 **私钥访问**: 仅在受信任的构建服务器上使用私钥
- 🚫 **版本控制**: 绝不将私钥提交到 Git 仓库
- 🔄 **密钥轮换**: 定期更新密钥对

## 文档

- [许可证生成指南](./LICENSE_GENERATOR.md) - 如何安全地生成许可证
- [优化报告](./OPTIMIZATION_REPORT.md) - 详细的优化改进说明

## API 参考

### License 类

#### 静态方法

| 方法 | 描述 | 返回值 |
|-----|------|--------|
| `Check()` | 检查许可证是否有效 | `bool` |
| `Check(string license)` | 验证指定的许可证字符串 | `bool` |
| `GetMachineCode()` | 获取当前机器的唯一标识 | `string` |
| `Create(string machineCode, string privateKeyXml)` | 生成许可证签名 | `string` |
| `Create(string privateKeyXml)` | 为当前机器生成许可证并保存 | `void` |
| `Sign(string text, string privateKey)` | 使用 RSA 私钥签名 | `string` |

## 变更历史

### 版本 2.0 (2025-10)

**安全性改进**:
- ✅ 移除硬编码私钥
- ✅ 升级到 SHA256 算法
- ✅ 实现资源正确释放

**代码质量**:
- ✅ 添加参数验证
- ✅ 改进错误处理
- ✅ 完整的 XML 文档注释
- ✅ 性能优化（StringBuilder）

**测试**:
- ✅ 创建单元测试套件

### 版本 1.0

- 初始实现
- 基本的 RSA 签名验证

## 常见问题

### Q: 为什么无法自动生成许可证？

A: 为了安全起见，私钥已从源代码中移除。请使用专门的许可证生成工具。

### Q: 如何为客户生成许可证？

A: 参考 [许可证生成指南](./LICENSE_GENERATOR.md)。

### Q: 旧的许可证还能用吗？

A: 不能。新版本使用 SHA256 算法，旧的 MD5 签名许可证不兼容。

### Q: 如何在开发环境中跳过许可证检查？

A: 使用条件编译：

```csharp
#if DEBUG
    // 开发模式，允许无许可证运行
#else
    if (!License.Check())
    {
        // 生产模式，要求许可证
    }
#endif
```

## 贡献

欢迎提交问题和改进建议！

## 许可证

本模块是 ColorVision 项目的一部分。
