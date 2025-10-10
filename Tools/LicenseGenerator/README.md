# ColorVision 许可证生成工具

这是一个用于生成 ColorVision 软件许可证的命令行工具。

## 功能特性

- 为当前机器生成许可证
- 为指定机器码生成许可证
- 批量生成许可证（从文件）
- 支持命令行和交互式两种模式
- 自动验证生成的许可证

## 快速开始

### Windows
```bash
run.bat
```

### Linux/Mac
```bash
./run.sh
```

### 使用 .NET CLI
```bash
dotnet run --project LicenseGenerator.csproj
```

## 使用方法

### 交互式模式

直接运行程序，不带任何参数：

```bash
LicenseGenerator
```

程序会显示菜单，让您选择操作：
1. 生成当前机器的许可证
2. 为指定机器码生成许可证
3. 批量生成许可证（从文件）
4. 退出

### 命令行模式

#### 为指定机器码生成许可证

```bash
LicenseGenerator -m <机器码>
```

示例：
```bash
LicenseGenerator -m 74657374
```

#### 生成许可证并保存到文件

```bash
LicenseGenerator -m <机器码> -o <输出文件路径>
```

示例：
```bash
LicenseGenerator -m 74657374 -o license.txt
```

#### 批量生成许可证

从文件读取机器码列表，每行一个机器码：

```bash
LicenseGenerator -f <输入文件> -o <输出文件>
```

示例：
```bash
LicenseGenerator -f machinecodes.txt -o licenses.txt
```

输出文件格式为 CSV：`机器码,许可证`

## 命令行参数

- `-m, --machine-code <code>` - 指定机器码生成许可证
- `-f, --file <filepath>` - 从文件读取机器码列表（每行一个）
- `-o, --output <filepath>` - 输出许可证到文件
- `-b, --batch` - 批量模式标志
- `-h, --help` - 显示帮助信息

## 机器码说明

机器码是基于机器名称（Environment.MachineName）生成的十六进制字符串。您可以：

1. 在目标机器上运行 ColorVision 软件获取机器码
2. 使用此工具的"生成当前机器的许可证"功能查看机器码
3. 通过代码调用 `License.GetMachineCode()` 获取

## 批量处理示例

创建一个 `machinecodes.txt` 文件，每行一个机器码：

```
74657374
616263646566
313233343536
```

运行批量生成命令：

```bash
LicenseGenerator -f machinecodes.txt -o licenses.txt
```

输出的 `licenses.txt` 文件格式：

```
74657374,<Base64编码的许可证>
616263646566,<Base64编码的许可证>
313233343536,<Base64编码的许可证>
```

## 注意事项

⚠️ **安全警告**：
- 此工具包含 RSA 私钥，仅供许可证生成使用
- 不要将此工具分发给最终用户
- 私钥应严格保密，仅限于授权的许可证管理人员使用

## 技术说明

- 使用 RSA 签名算法（SHA256）
- 许可证为 Base64 编码的签名数据
- 客户端使用公钥验证许可证，无法生成新许可证
- 每个许可证与特定机器码绑定

## 构建

使用 .NET 8.0 SDK：

```bash
dotnet build
```

发布为独立应用程序：

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## 许可证验证

生成的许可证可以通过 `ColorVision.UI.ACE.License.Check()` 方法验证。
