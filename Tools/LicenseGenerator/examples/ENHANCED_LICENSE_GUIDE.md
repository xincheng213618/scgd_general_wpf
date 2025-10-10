# 增强型许可证使用示例

本文档展示如何使用增强型许可证功能。

## 增强型许可证 vs 简单许可证

### 简单许可证
- **格式**：Base64 编码的 RSA 签名
- **用途**：基础的软件许可验证
- **包含信息**：仅机器码绑定
- **示例**：
  ```
  hQW7/x9MnKpZE8vF3LkR2jM...（Base64 字符串）
  ```

### 增强型许可证
- **格式**：Base64 编码的 JSON 对象
- **用途**：商业许可、设备许可、相机许可等需要详细信息的场景
- **包含信息**：
  - 客户名称
  - 设备型号
  - 有效期
  - 签发日期
  - 签发机构
  - 机器码
  - 授权签名
- **示例**：
  ```
  eyJhdXRob3JpdHlfc2lnbmF0dXJlIjoiaFFXNy94OU1uS3BaRTh2RjNMa1Iy...
  ```
  （解码后为 JSON）

## 生成步骤

### 1. 获取机器码

在目标设备上运行 LicenseGenerator 工具，或通过代码获取：

```csharp
string machineCode = ColorVision.UI.ACE.License.GetMachineCode();
// 例如：74657374
```

### 2. 使用工具生成增强许可证

1. 打开 LicenseGenerator 工具
2. 切换到"增强许可证"标签页
3. 填写信息：
   - **机器码**：`74657374`
   - **客户名称**：`测试公司`
   - **设备型号**：`CV-CAM-1000`
   - **有效期至**：`2025-12-31`
   - **签发机构**：`ColorVision`
4. 点击"生成增强许可证"
5. 复制或保存生成的许可证

### 3. 生成的许可证示例

**原始 JSON 内容**（Base64 解码后）：
```json
{
  "authority_signature": "hQW7/x9MnKpZE8vF3LkR2jMqN4oP5sT6uV7wX8yZ9aB0cD1eE2fF3gG4hH5iI6jJ7kK8lL9mM0nN1oO2pP3qQ4rR5sS6tT7uU8vV9wW0xX1yY2zZ3aA4bB5cC6dD7eE8fF9gG0hH1iI2jJ3kK4lL5mM6nN7oO8pP9qQ0rR1sS2tT3uU4vV5wW6xX7yY8zZ9aA0bB1cC2dD3eE4fF5gG6hH7iI8jJ9kK0lL1mM2nN3oO4pP5qQ6rR7sS8tT9uU0vV1wW2xX3yY4zZ5aA6bB7cC8dD9eE0fF1gG2hH3iI4jJ5kK6lL7mM8nN9oO0pP1qQ2rR3sS4tT5uU6vV7wW8xX9yY0zZ1aA2bB3cC4dD5eE6fF7gG8hH9iI0jJ1kK2lL3mM4nN5oO6pP7qQ8rR9sS0tT1uU2vV3wW4xX5yY6zZ7aA8bB9cC0dD1eE2fF3gG4hH5iI6jJ7kK8lL9mM0nN1oO2pP3qQ4rR5sS6tT7uU8vV9wW0xX1yY2zZ3aA4bB5cC6dD7eE8fF9gG==",
  "device_mode": "CV-CAM-1000",
  "expiry_date": "1735689600",
  "issue_date": "1704067200",
  "issuing_authority": "ColorVision",
  "licensee": "测试公司",
  "licensee_signature": "74657374"
}
```

**Base64 编码后的许可证**（实际使用的格式）：
```
eyJhdXRob3JpdHlfc2lnbmF0dXJlIjoiaFFXNy94OU1uS3BaRTh2RjNMa1Iy...（完整的 Base64 字符串）
```

## 部署许可证

### 对于相机设备

将生成的 `.lic` 文件放入 ZIP 包中：

```
license_package.zip
└── <机器码>.lic   # 例如：74657374.lic
```

通过 `PhyCamera.SetLicense()` 方法上传：

```csharp
bool success = phyCamera.SetLicense("license_package.zip");
```

### 验证许可证

系统会自动：
1. 提取 .lic 文件
2. Base64 解码
3. 解析 JSON
4. 验证机器码
5. 检查有效期
6. 验证签名

```csharp
// 许可证会被存储到数据库
CameraLicenseModel license = CameraLicenseDao.Instance.GetByMAC(machineCode);

// 检查许可证状态
if (license != null)
{
    var colorVisionLicense = license.ColorVisionLicense;
    Console.WriteLine($"客户: {colorVisionLicense.Licensee}");
    Console.WriteLine($"设备型号: {colorVisionLicense.DeviceMode}");
    Console.WriteLine($"过期时间: {colorVisionLicense.ExpiryDateTime}");
    Console.WriteLine($"是否过期: {colorVisionLicense.ExpiryDateTime < DateTime.Now}");
}
```

## 常见场景

### 场景 1：为新客户生成许可证

1. 获取客户设备的机器码
2. 在工具中填写：
   - 客户名称：`ABC 公司`
   - 设备型号：根据实际设备填写
   - 有效期：根据合同期限设置
3. 生成并保存为 `.lic` 文件
4. 发送给客户部署

### 场景 2：许可证续期

1. 使用相同的机器码
2. 更新有效期为新的日期
3. 重新生成许可证
4. 替换旧的许可证文件

### 场景 3：批量生成许可证

如果需要为多台设备批量生成：
1. 准备机器码列表（可从数据库导出）
2. 逐个在工具中生成
3. 或者扩展工具支持批量导入（未来功能）

## 许可证安全

### 私钥安全
- 私钥仅存在于 LicenseGenerator 工具中
- 不要将工具分发给客户
- 建议在隔离的安全环境中使用

### 许可证分发
- 使用加密通道发送许可证文件
- 可以使用 ZIP 压缩并加密
- 记录许可证生成日志

### 有效期管理
- 设置合理的有效期
- 提前通知客户续期
- 监控许可证过期情况

## 故障排除

### 问题：许可证验证失败

**可能原因**：
1. 机器码不匹配
2. 许可证已过期
3. 文件损坏或格式错误
4. Base64 解码失败

**解决方法**：
1. 确认机器码是否正确
2. 检查有效期设置
3. 重新生成许可证
4. 确保文件内容完整（无多余空格、换行）

### 问题：无法生成增强许可证

**可能原因**：
1. 缺少必填字段
2. 有效期设置不合理
3. 工具版本过旧

**解决方法**：
1. 检查所有必填字段是否填写
2. 确保有效期晚于当前时间
3. 更新工具到最新版本

## 技术细节

### 签名算法

增强型许可证的签名基于：
```
签名数据 = 机器码 + ":" + 过期时间戳
签名算法 = RSA-SHA256
```

### JSON 结构

```typescript
interface EnhancedLicense {
  authority_signature: string;    // RSA 签名（Base64）
  device_mode: string;            // 设备型号
  expiry_date: string;            // Unix 时间戳（秒）
  issue_date: string;             // Unix 时间戳（秒）
  issuing_authority: string;      // 签发机构
  licensee: string;               // 被许可人
  licensee_signature: string;     // 机器码
}
```

### 编码流程

1. 创建 JSON 对象
2. 生成授权签名
3. 序列化为 JSON 字符串
4. UTF-8 编码
5. Base64 编码

### 验证流程

1. Base64 解码
2. UTF-8 解码为 JSON
3. 反序列化对象
4. 验证机器码匹配
5. 检查是否过期
6. 验证 RSA 签名
