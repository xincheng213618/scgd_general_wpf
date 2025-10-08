# ConPTY 终端实现架构图

## 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                    TerminalManagerWindow (UI)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  RichTextBox (rtbOutput)                                   │  │
│  │  - 黑色背景 (#000000)                                      │  │
│  │  - 白色文本                                                │  │
│  │  - Consolas 等宽字体                                       │  │
│  │  - 显示终端输出 (包括 ANSI 转义序列)                      │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  TextBox (tbInput)                                         │  │
│  │  - 深色背景 (#1E1E1E)                                      │  │
│  │  - Enter 键发送命令                                        │  │
│  └───────────────────────────────────────────────────────────┘  │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ↓
┌─────────────────────────────────────────────────────────────────┐
│                     ConPtyTerminal (核心类)                       │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  公共接口                                               │    │
│  │  • Start(cols, rows, command)  - 启动终端              │    │
│  │  • SendInput(text)             - 发送输入              │    │
│  │  • Resize(cols, rows)          - 调整大小              │    │
│  │  • OutputReceived 事件         - 输出通知              │    │
│  │  • Dispose()                   - 清理资源              │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  内部状态                                               │    │
│  │  • _hPC                        - ConPTY 句柄           │    │
│  │  • _consoleInputPipeWriteHandle  - 输入管道           │    │
│  │  • _consoleOutputPipeReadHandle  - 输出管道           │    │
│  │  • _inputWriter                - 输入流               │    │
│  │  • _outputReader               - 输出流               │    │
│  │  • _cancellationTokenSource    - 取消令牌             │    │
│  └────────────────────────────────────────────────────────┘    │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ↓
┌─────────────────────────────────────────────────────────────────┐
│              ConPtyNativeMethods (Windows API)                   │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  ConPTY API (kernel32.dll)                              │    │
│  │  • CreatePseudoConsole         - 创建伪终端            │    │
│  │  • ResizePseudoConsole         - 调整终端大小          │    │
│  │  • ClosePseudoConsole          - 关闭伪终端            │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  进程和管道 API (kernel32.dll)                          │    │
│  │  • CreatePipe                  - 创建管道              │    │
│  │  • CreateProcess               - 创建进程              │    │
│  │  • InitializeProcThreadAttributeList                   │    │
│  │  • UpdateProcThreadAttribute                           │    │
│  │  • DeleteProcThreadAttributeList                       │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  数据结构                                               │    │
│  │  • COORD                       - 坐标/尺寸             │    │
│  │  • STARTUPINFOEX               - 进程启动信息          │    │
│  │  • PROCESS_INFORMATION         - 进程信息              │    │
│  └────────────────────────────────────────────────────────┘    │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ↓
┌─────────────────────────────────────────────────────────────────┐
│                   Windows ConPTY (操作系统)                      │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  伪终端 (Pseudo Console)                                │    │
│  │  • VT100/ANSI 转义序列处理                              │    │
│  │  • 光标控制                                             │    │
│  │  • 颜色和格式                                           │    │
│  │  • 完整的终端模拟                                       │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
│                            ↓                                     │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  子进程 (cmd.exe / powershell.exe / 其他)              │    │
│  │  • 认为自己运行在真实终端中                             │    │
│  │  • 输出 ANSI 转义序列                                   │    │
│  │  • 支持交互式输入                                       │    │
│  └────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

## 数据流向

### 用户输入流

```
用户按键
   ↓
TextBox.KeyDown 事件 (Enter)
   ↓
TerminalManagerWindow.tbInput_KeyDown()
   ↓
ConPtyTerminal.SendInput(command + "\r\n")
   ↓
StreamWriter (_inputWriter) 写入
   ↓
管道 (Pipe)
   ↓
Windows ConPTY
   ↓
子进程 stdin
```

### 输出流

```
子进程 stdout/stderr
   ↓
Windows ConPTY (处理 VT 序列)
   ↓
管道 (Pipe)
   ↓
StreamReader (_outputReader) 读取 (异步)
   ↓
ConPtyTerminal.ReadOutput()
   ↓
触发 OutputReceived 事件
   ↓
TerminalManagerWindow.OnTerminalOutput()
   ↓
Dispatcher.Invoke (UI 线程)
   ↓
RichTextBox.AppendText() 显示
```

## 关键技术点

### 1. ConPTY 初始化流程

```
1. CreatePipe() × 2         → 创建输入和输出管道
2. CreatePseudoConsole()    → 创建伪终端，关联管道
3. InitializeProcThreadAttributeList() → 初始化进程属性列表
4. UpdateProcThreadAttribute() → 附加 ConPTY 到属性列表
5. CreateProcess()          → 创建子进程并关联 ConPTY
6. 清理临时句柄            → 释放不需要的句柄
```

### 2. 异步输出处理

```csharp
// 在后台线程异步读取输出
Task.Run(() => ReadOutput(_cancellationTokenSource.Token));

// ReadOutput 方法持续读取直到进程结束
while (!cancellationToken.IsCancellationRequested)
{
    int bytesRead = await _outputReader.ReadAsync(buffer, 0, buffer.Length);
    if (bytesRead > 0)
    {
        // 触发事件通知 UI
        OutputReceived?.Invoke(this, output);
    }
}
```

### 3. 资源管理

```
IDisposable 模式确保资源正确释放：
1. CancellationTokenSource.Cancel()  → 停止输出读取
2. StreamWriter.Dispose()            → 关闭输入流
3. StreamReader.Dispose()            → 关闭输出流
4. SafeFileHandle.Dispose() × 2      → 关闭管道句柄
5. ClosePseudoConsole()              → 关闭 ConPTY 句柄
```

## 与旧实现的对比

### 旧实现 (Process + StandardInput/Output)

```
┌──────────────┐
│  Process     │
│  cmd.exe     │
│              │
│ [检测非TTY] │  ← 问题：程序知道不是真实终端
│              │
│  Stdout  ────┼──→ BeginOutputReadLine() → 文本
│  Stderr  ────┼──→ BeginErrorReadLine()  → 文本
│  Stdin   ←───┼──  StandardInput.WriteLine()
└──────────────┘

限制：
✗ 不支持 ANSI 颜色
✗ 交互式程序可能不工作 (如 Python REPL)
✗ 某些程序检测到非 TTY 改变行为
✗ 无法正确处理光标控制
```

### 新实现 (ConPTY)

```
┌──────────────────┐
│  Windows ConPTY  │  ← VT/ANSI 处理层
│  ┌────────────┐  │
│  │  Process   │  │
│  │  cmd.exe   │  │
│  │            │  │
│  │ [检测TTY] │  │  ← 优势：程序认为是真实终端
│  │            │  │
│  │  I/O       │  │
│  └────────────┘  │
│                  │
│  VT Sequences ───┼──→ Pipe → StreamReader → 输出
│  Input       ←───┼──  Pipe ← StreamWriter ← 输入
└──────────────────┘

优势：
✓ 完整 ANSI/VT100 支持
✓ 所有交互式程序正常工作
✓ 程序检测为真实终端
✓ 支持颜色、光标控制等
✓ 行为与真实终端一致
```

## 系统要求

```
最低要求:
├─ Windows 10 Version 1809 (Build 17763)
├─ .NET 6.0 或 .NET 8.0
└─ 不需要额外依赖

推荐配置:
├─ Windows 10 Version 21H2 或 Windows 11
├─ .NET 8.0
└─ 现代多核处理器
```

## 文件清单

```
UI/ColorVision.Solution/
├─ TerminalManagerWindow.xaml           [已修改] UI 定义，黑色主题
├─ TerminalManagerWindow.xaml.cs        [已修改] 使用 ConPtyTerminal
├─ ConPtyTerminal.cs                    [新增]   ConPTY 封装类
├─ NativeMethods/
│  └─ ConPtyNativeMethods.cs           [新增]   Windows API 声明
├─ ConPtyExample.cs                     [新增]   使用示例
├─ ConPTY_README.md                     [新增]   使用文档
└─ ConPTY_Advanced_Features.md          [新增]   高级功能规划
```

## 下一步计划

详见 `ConPTY_Advanced_Features.md`，优先级排序：

```
高优先级（必要功能）:
1. ☐ ANSI 转义序列解析器        → 正确显示颜色和格式
2. ☐ 窗口大小自动调整           → 响应窗口 resize
3. ☐ 完整键盘输入支持           → 箭头键、Ctrl+C 等

中优先级（增强功能）:
4. ☐ 文本选择和复制             → 鼠标操作
5. ☐ 多标签页支持               → 多个终端实例
6. ☐ 性能优化                   → 大量输出时的优化

低优先级（可选功能）:
7. ☐ 主题和配置                 → 自定义颜色、字体
8. ☐ Shell 选择                 → PowerShell、WSL 等
9. ☐ 链接检测                   → 点击打开 URL
10.☐ 搜索功能                   → Ctrl+F 搜索
```

---

最后更新: 2025-01-26
