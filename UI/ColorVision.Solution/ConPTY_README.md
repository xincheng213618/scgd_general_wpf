# ConPTY Terminal 实现说明

## 概述

本实现为 ColorVision.Solution 的 `TerminalManagerWindow` 添加了基于 Windows ConPTY (Pseudo Console) API 的终端功能，提供类似 VSCode 终端的体验。

## 实现的功能

### ✅ 基础功能

1. **ConPTY 集成**
   - 使用 Windows 10 1809+ 的 ConPTY API
   - 完整的伪终端支持，包括 ANSI/VT100 转义序列
   - 相比之前的 `Process.StandardInput/Output`，能够正确处理交互式程序

2. **用户界面**
   - 黑色背景 (#000000)
   - 白色文本
   - Consolas 等宽字体
   - 类似 VSCode 的终端外观
   - 输入框使用深色主题 (#1E1E1E)

3. **输入输出**
   - 支持命令输入（Enter 键发送）
   - 实时显示终端输出
   - 异步输出处理，不阻塞UI线程

4. **进程管理**
   - 正确的进程生命周期管理
   - 窗口关闭时自动清理资源
   - 安全的句柄释放

## 文件结构

```
UI/ColorVision.Solution/
├── TerminalManagerWindow.xaml           # UI 定义
├── TerminalManagerWindow.xaml.cs        # 窗口逻辑
├── ConPtyTerminal.cs                    # ConPTY 封装类
├── NativeMethods/
│   └── ConPtyNativeMethods.cs          # Windows API P/Invoke 声明
└── ConPTY_Advanced_Features.md         # 高级功能文档
```

## 核心类说明

### ConPtyTerminal

主要的 ConPTY 封装类，提供以下功能：

```csharp
public class ConPtyTerminal : IDisposable
{
    // 启动终端
    public void Start(short cols = 80, short rows = 25, string command = "cmd.exe")
    
    // 发送输入
    public void SendInput(string input)
    
    // 调整大小
    public void Resize(short cols, short rows)
    
    // 输出事件
    public event EventHandler<string>? OutputReceived;
}
```

### ConPtyNativeMethods

包含所有必要的 Windows API P/Invoke 声明：

- `CreatePseudoConsole` - 创建伪终端
- `ResizePseudoConsole` - 调整终端大小
- `ClosePseudoConsole` - 关闭伪终端
- `CreatePipe` - 创建管道
- `CreateProcess` - 创建进程
- 各种结构体和常量定义

## 使用方法

### 基本使用

```csharp
// 创建终端实例
var terminal = new ConPtyTerminal();

// 订阅输出事件
terminal.OutputReceived += (sender, output) => 
{
    Console.Write(output);
};

// 启动终端（80列 x 25行，运行 cmd.exe）
terminal.Start(80, 25, "cmd.exe");

// 发送命令
terminal.SendInput("dir\r\n");

// 清理资源
terminal.Dispose();
```

### 在 TerminalManagerWindow 中使用

窗口已经集成了 ConPTY，通过菜单 "帮助" -> "终端" 打开。

## 系统要求

- **操作系统**: Windows 10 版本 1809 (October 2018 Update) 或更高
- **运行时**: .NET 6.0+ / .NET 8.0+
- **ConPTY API**: 需要 Windows 10 内置支持

可以使用以下代码检查系统版本：

```csharp
// 检查是否支持 ConPTY
var version = Environment.OSVersion.Version;
bool supportsConPty = version.Major >= 10 && version.Build >= 17763; // 1809
```

## 与旧实现的对比

### 旧实现 (Process + Redirect)
```csharp
// 问题：
// 1. 不支持交互式程序（如 Python REPL）
// 2. 无法处理 ANSI 转义序列
// 3. 无法正确显示彩色输出
// 4. 某些程序检测到非交互式环境会改变行为

_process = new Process();
_process.StartInfo.FileName = "cmd.exe";
_process.StartInfo.RedirectStandardInput = true;
_process.StartInfo.RedirectStandardOutput = true;
_process.StartInfo.RedirectStandardError = true;
```

### 新实现 (ConPTY)
```csharp
// 优势：
// 1. 完整的伪终端支持
// 2. 支持所有交互式程序
// 3. 原生 ANSI/VT100 支持
// 4. 程序检测为真实终端环境

_terminal = new ConPtyTerminal();
_terminal.OutputReceived += OnTerminalOutput;
_terminal.Start(80, 25, "cmd.exe");
```

## 已知限制

当前的基础实现有以下限制：

1. **ANSI 解析**: 输出包含原始 ANSI 转义序列，尚未解析为颜色和格式
2. **窗口调整**: 窗口大小改变时不会自动调整终端尺寸
3. **文本选择**: 尚不支持鼠标选择和复制文本
4. **快捷键**: 仅支持基本的 Enter 键，其他快捷键待实现
5. **多标签**: 尚不支持多个终端标签页

详细的待实现功能请参考 `ConPTY_Advanced_Features.md`。

## 后续改进计划

详细的改进计划和实现指南，请参考 `ConPTY_Advanced_Features.md` 文档，其中包括：

1. ANSI 转义序列解析器（高优先级）
2. 终端窗口大小调整（中优先级）
3. 文本选择和复制功能
4. 多终端标签页支持
5. 配置和主题系统
6. 性能优化
7. 更多...

## 故障排查

### 问题：终端无法启动

**可能原因**:
- Windows 版本不支持 ConPTY (< Windows 10 1809)
- 缺少必要的权限

**解决方法**:
```csharp
try
{
    _terminal.Start(80, 25, "cmd.exe");
}
catch (InvalidOperationException ex)
{
    MessageBox.Show($"终端启动失败: {ex.Message}\n\n" +
                    $"请确保您的系统为 Windows 10 1809 或更高版本。",
                    "ConPTY 错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
}
```

### 问题：输出显示乱码

**可能原因**:
- 编码问题
- ANSI 转义序列未被处理

**解决方法**:
- 确保使用 UTF-8 编码（已在代码中设置）
- 未来版本将实现 ANSI 解析器来正确处理转义序列

## 参考资源

### 官方文档
- [Windows Pseudo Console (ConPTY)](https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [Console Virtual Terminal Sequences](https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences)

### 示例项目
- [Windows Terminal](https://github.com/microsoft/terminal) - Microsoft 官方终端
- [ConPTY in C#](https://github.com/mRemoteNG/mRemoteNG/blob/develop/mRemoteNG/Connection/Protocol/RDP/ConPtyShell.cs)

### ANSI 参考
- [ANSI Escape Codes Gist](https://gist.github.com/fnky/458719343aabd01cfb17a3a4f7296797)
- [Build your own Command Line](https://www.lihaoyi.com/post/BuildyourownCommandLinewithANSIescapecodes.html)

## 贡献

欢迎贡献代码来实现 `ConPTY_Advanced_Features.md` 中列出的高级功能！

### 建议的贡献方向

1. 实现 ANSI 转义序列解析器
2. 添加终端窗口大小自动调整
3. 实现文本选择和复制功能
4. 添加多标签页支持
5. 创建配置界面

---

最后更新: 2025-01-26
作者: GitHub Copilot
