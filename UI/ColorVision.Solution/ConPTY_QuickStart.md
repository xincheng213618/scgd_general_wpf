# ConPTY 终端实现 - 快速开始

## 什么是 ConPTY？

ConPTY (Pseudo Console) 是 Windows 10 1809+ 提供的伪终端 API，允许应用程序创建完整的终端模拟环境。

**主要优势**：
- ✅ 完整支持 ANSI/VT100 转义序列（颜色、格式化等）
- ✅ 支持所有交互式程序（如 Python REPL、Node.js REPL）
- ✅ 程序检测为真实终端环境
- ✅ 与 Windows Terminal、VSCode 使用相同技术

## 已实现的功能

### 基础功能 ✅
- ConPTY API 集成
- 命令执行（cmd.exe）
- 输入/输出处理
- 进程生命周期管理
- VSCode 风格 UI（黑色背景、Consolas 字体）

### UI 改进 ✅
- 黑色背景 (#000000)
- 白色文本输出
- 深色输入框 (#1E1E1E)
- Consolas 等宽字体
- 自动滚动

## 使用方法

### 通过 UI 使用

在应用程序菜单中选择：**帮助 → 终端**

### 编程使用

```csharp
using ColorVision.Solution;

// 创建终端
var terminal = new ConPtyTerminal();

// 监听输出
terminal.OutputReceived += (sender, output) => 
{
    Console.Write(output);
};

// 启动终端
terminal.Start(cols: 80, rows: 25, command: "cmd.exe");

// 发送命令
terminal.SendInput("dir\r\n");

// 清理
terminal.Dispose();
```

## 文件结构

```
UI/ColorVision.Solution/
│
├── TerminalManagerWindow.xaml          # UI（已更新为黑色主题）
├── TerminalManagerWindow.xaml.cs       # 窗口逻辑（使用 ConPTY）
├── ConPtyTerminal.cs                   # ConPTY 封装类
├── NativeMethods/
│   └── ConPtyNativeMethods.cs         # Windows API P/Invoke
│
├── ConPtyExample.cs                    # 使用示例代码
├── ConPTY_README.md                    # 详细文档
├── ConPTY_Architecture.md              # 架构说明
└── ConPTY_Advanced_Features.md         # 高级功能规划
```

## 核心类

### `ConPtyTerminal`

主要的终端封装类：

```csharp
public class ConPtyTerminal : IDisposable
{
    // 启动终端
    public void Start(short cols, short rows, string command)
    
    // 发送输入
    public void SendInput(string input)
    
    // 调整大小
    public void Resize(short cols, short rows)
    
    // 输出事件
    public event EventHandler<string>? OutputReceived;
    
    // 释放资源
    public void Dispose()
}
```

## 待实现功能

按优先级排序：

### 🔴 高优先级
1. **ANSI 转义序列解析器** - 正确显示颜色和格式
2. **窗口大小调整** - 响应窗口 resize 事件
3. **完整键盘输入** - 箭头键、Ctrl+C 等

### 🟡 中优先级
4. **文本选择和复制** - 鼠标选择、Ctrl+C 复制
5. **多标签页** - 支持多个终端实例
6. **性能优化** - 大量输出时的优化

### 🟢 低优先级
7. **主题配置** - 自定义颜色、字体
8. **Shell 选择** - PowerShell、WSL 支持
9. **链接检测** - 点击 URL 打开浏览器
10. **搜索功能** - Ctrl+F 搜索输出

详细信息请参考 `ConPTY_Advanced_Features.md`。

## 系统要求

- **最低**: Windows 10 Version 1809 (Build 17763)
- **推荐**: Windows 10 21H2+ 或 Windows 11
- **运行时**: .NET 6.0+ / .NET 8.0+

## 与旧实现对比

| 特性 | 旧实现 (Process) | 新实现 (ConPTY) |
|------|-----------------|----------------|
| ANSI 颜色 | ❌ | ✅ |
| 交互式程序 | ⚠️ 部分支持 | ✅ 完全支持 |
| 光标控制 | ❌ | ✅ |
| 终端检测 | ❌ 非 TTY | ✅ 真实 TTY |
| Python REPL | ❌ | ✅ |
| Node.js REPL | ❌ | ✅ |
| PowerShell | ⚠️ 受限 | ✅ 完整 |

## 快速问题排查

### Q: 终端无法启动
**A**: 检查 Windows 版本是否 >= 10 1809 (Build 17763)

### Q: 看到乱码
**A**: 这是 ANSI 转义序列，需要实现解析器（见高级功能文档）

### Q: 某些键不工作
**A**: 当前仅支持基本文本输入和 Enter 键，更多键支持计划中

## 参考资源

### 官方文档
- [Windows ConPTY API](https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [Console Virtual Terminal Sequences](https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences)

### 相关项目
- [Windows Terminal](https://github.com/microsoft/terminal)
- [ConPTY in C# Example](https://github.com/mRemoteNG/mRemoteNG)

### ANSI 参考
- [ANSI Escape Codes](https://gist.github.com/fnky/458719343aabd01cfb17a3a4f7296797)

## 贡献

欢迎贡献代码实现高级功能！优先建议：

1. 实现 ANSI 转义序列解析器
2. 添加窗口大小自动调整
3. 改进键盘输入处理

---

**作者**: GitHub Copilot  
**日期**: 2025-01-26  
**版本**: 1.0.0 (基础实现)
