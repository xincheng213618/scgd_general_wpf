# 终端面板 (Terminal)

## 概述

ColorVision 内置了一个基于 Windows Pseudo Console（ConPTY）技术的集成终端面板，提供真实的 TTY 环境，支持交互式 Shell（PowerShell / CMD）。终端面板位于工作区底部，可通过菜单或快捷键调出。

## 功能特性

| 功能 | 说明 |
|------|------|
| 真实 PTY | 基于 ConPTY API，`isatty()` 返回 `true`，PSReadLine/Python REPL 等工具可正常工作 |
| VT100 支持 | 内置 VT100/xterm 转义序列解析，正确处理光标移动、行编辑、颜色等 |
| 滚动回放 | 最多保留 3000 行滚动历史 |
| 命令历史 | 持久化到 `%AppData%\ColorVision\terminal_history.txt`（最多 1000 条） |
| Shell 切换 | 支持在 PowerShell 和 CMD 之间切换 |
| 脚本运行 | 可直接运行 `.py`、`.ps1`、`.bat`、`.sh`、`.js` 脚本文件 |
| 工作目录 | 自动使用当前解决方案目录作为工作目录 |
| 键盘映射 | 完整映射方向键、F1-F12、Ctrl 组合键等到 VT 序列 |

## 界面说明

```
┌──────────────────────────────────────────────────────────┐
│  [PowerShell ▼]  [+ 新终端]  [清屏]  [终止]              │  ← 工具栏
├──────────────────────────────────────────────────────────┤
│  PS C:\Projects\MySolution>                              │
│  > python train.py --epochs 10                           │
│  Epoch 1/10: 100%|████████| 500/500 ...                  │
│  ...                                                     │
└──────────────────────────────────────────────────────────┘
```

### 工具栏按钮

| 按钮 | 功能 |
|------|------|
| Shell 下拉框 | 选择 PowerShell 或 CMD，切换后重启终端 |
| `+ 新终端` | 终止当前 Shell 并启动新会话 |
| `清屏` | 清除屏幕缓冲区（等同于 `Ctrl+L`） |
| `终止` | 强制终止当前 Shell 进程 |

## 键盘快捷键

| 快捷键 | 行为 |
|--------|------|
| `Ctrl+C` | 无选中文本时发送中断信号；有选中文本时复制 |
| `Ctrl+V` | 粘贴剪贴板内容到终端 |
| `Ctrl+L` | 清屏并发送 form-feed 到 Shell |
| `↑` / `↓` | 命令历史导航（由 Shell 处理） |
| `←` / `→` | 光标移动（由 Shell 处理） |
| `Tab` | 自动补全（由 Shell 处理） |
| `F1` - `F12` | 功能键（映射为标准 xterm 序列） |
| `Home` / `End` | 行首 / 行尾 |
| `Page Up` / `Page Down` | 历史翻页 |
| `Delete` / `Insert` | 删除字符 / 插入模式 |

## 使用方法

### 打开终端面板

终端面板在工作区启动时自动注册为底部停靠面板（Panel ID: `TerminalPanel`）。
若面板被关闭，可通过菜单 **视图 → 终端** 重新显示。

### 运行脚本

在解决方案资源管理器中右键单击脚本文件，选择"在终端中运行"，或通过代码调用：

```csharp
TerminalService.GetInstance().RunScript(@"C:\Projects\script.py");
```

支持的脚本类型：

| 扩展名 | 运行方式 |
|--------|----------|
| `.py` / `.pyw` | `python "file.py"` |
| `.ps1` | `& "file.ps1"` (PowerShell) |
| `.bat` / `.cmd` | `cmd /c "file.bat"` |
| `.sh` | `bash "file.sh"` |
| `.js` | `node "file.js"` |

### 发送命令

```csharp
TerminalService.GetInstance().SendCommand("git status");
```

## 技术架构

```
TerminalPanelProvider          ← IDockPanelProvider，注册为底部面板
    └── TerminalControl        ← WPF UserControl，主视图
            ├── ConPtyTerminal          ← Win32 ConPTY 封装（内部类）
            │       ├── CreatePipe / CreatePseudoConsole
            │       ├── 读线程（ReadLoop，后台线程）
            │       └── OutputReceived / ProcessExited 事件
            ├── TerminalScreenBuffer    ← VT100 屏幕缓冲区（内部类）
            │       ├── CSI / OSC 转义序列解析
            │       ├── 3000 行滚动回放队列
            │       └── Render() / GetCursorOffset()
            ├── CommandHistory          ← 持久化命令历史（内部类）
            │       ├── 保存到 %AppData%\ColorVision\terminal_history.txt
            │       ├── 最大 1000 条，去重
            │       └── NavigateUp / NavigateDown
            └── TerminalService        ← 单例服务，对外 API
```

### 数据流

```
用户键盘输入
    → OutputTextBox.PreviewKeyDown / PreviewTextInput
    → KeyToVTSequence() 转换为 VT 序列
    → ConPtyTerminal.Write() → 管道 → Shell 进程

Shell 输出
    → ConPtyTerminal 读线程 → OutputReceived 事件
    → _pendingOutput 队列（线程安全）
    → DispatcherTimer（30ms） → FlushOutput()
    → TerminalScreenBuffer.Write()（解析 VT100）
    → RenderBuffer() → TextBox.Text 更新
```

## 系统要求

- Windows 10 版本 1809（Build 17763）或更高版本
- ConPTY API（`kernel32.dll`）

## 已知限制

| 限制 | 说明 |
|------|------|
| 仅 Windows | ConPTY 是 Windows 专有 API |
| 无颜色渲染 | SGR（颜色/加粗）序列被解析但忽略，文本为单色显示 |
| 无鼠标支持 | 不处理终端鼠标事件 |
| 固定终端尺寸 | 默认 120 列 × 30 行；窗口调整时需手动触发 Resize |

## 优化建议

当前实现基本功能完整，以下是可进一步改进的方向：

| 优先级 | 优化项 | 说明 |
|--------|--------|------|
| 高 | **动态终端尺寸** | 监听 `TerminalControl.SizeChanged`，计算列/行数并调用 `ConPtyTerminal.Resize()`，解决 PSReadLine 换行错位问题 |
| 高 | **ANSI 颜色支持** | 解析 SGR 颜色序列，改用 `FlowDocument` 或 `Run` 集合渲染彩色文本 |
| 中 | **多标签页** | 支持多 Shell 会话（类似 VS Code 终端面板），当前 "+ 新终端" 会终止现有会话 |
| 中 | **字体大小调节** | 工具栏添加字号下拉框，调整 `OutputTextBox.FontSize` |
| 中 | **终端类型扩展** | 支持 Git Bash、WSL (`wsl.exe`) 等其他 Shell |
| 低 | **CommandHistory 集成** | 当前历史导航由 Shell (PSReadLine) 处理，`CommandHistory` 类未被 UI 使用，可移除或改为本地命令补全 |
| 低 | **平台保护** | 添加 `[SupportedOSPlatform("windows")]` 或 `#if WINDOWS` 编译保护，防止非 Windows 构建失败 |
