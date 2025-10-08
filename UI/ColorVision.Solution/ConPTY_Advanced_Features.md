# ConPTY Terminal Implementation - Advanced Features Documentation

## Current Implementation

已实现的基础功能：

### ✅ 已完成
1. **ConPTY基础集成** - 使用Windows ConPTY API实现伪终端
2. **基本命令执行** - 支持cmd.exe命令执行
3. **输入/输出流处理** - 双向通信机制
4. **进程生命周期管理** - 启动、停止和清理
5. **ANSI/VT100序列支持** - 原生支持终端转义序列
6. **基本UI** - 黑色背景、Consolas字体、类似VSCode的外观

## 待实现的高级功能

以下是需要进一步实现的高级功能列表：

### 1. ANSI转义序列解析器 (ANSI Escape Sequence Parser)

**优先级**: 高

**描述**: 当前实现直接显示ConPTY的输出，但不解析ANSI转义序列用于颜色、光标定位等。

**需要实现**:
- ANSI颜色代码解析 (例如: `\x1b[31m` 为红色文本)
- 光标位置控制 (CSI序列)
- 文本格式化 (粗体、斜体、下划线)
- 清屏和行清除命令

**参考资源**:
- https://en.wikipedia.org/wiki/ANSI_escape_code
- https://github.com/spectreconsole/spectre.console (C# ANSI解析示例)

**实现建议**:
```csharp
// 创建AnsiParser类来处理转义序列
public class AnsiParser
{
    public List<AnsiToken> Parse(string text) { }
    public void ApplyToRichTextBox(RichTextBox rtb, List<AnsiToken> tokens) { }
}
```

---

### 2. 终端窗口大小调整 (Terminal Resize)

**优先级**: 中

**描述**: 当窗口大小改变时，需要通知ConPTY调整终端尺寸。

**需要实现**:
- 监听窗口SizeChanged事件
- 计算字符列数和行数（基于字体大小）
- 调用`ResizePseudoConsole` API

**实现示例**:
```csharp
private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
{
    // 计算新的终端尺寸
    short cols = CalculateColumns(e.NewSize.Width);
    short rows = CalculateRows(e.NewSize.Height);
    _terminal?.Resize(cols, rows);
}
```

---

### 3. 选择和复制功能 (Selection & Copy)

**优先级**: 中

**描述**: 实现鼠标选择文本和复制到剪贴板的功能。

**需要实现**:
- 鼠标拖拽选择文本
- Ctrl+C复制选中文本（不发送SIGINT到进程）
- 右键菜单支持

---

### 4. 多终端标签页 (Multiple Terminal Tabs)

**优先级**: 中

**描述**: 类似VSCode，支持多个终端实例。

**需要实现**:
- 使用TabControl管理多个终端
- 每个标签页独立的ConPTY实例
- 标签页创建/关闭管理

---

### 5. 配置和主题 (Configuration & Themes)

**优先级**: 低

**描述**: 可配置的终端设置。

**需要实现**:
- 颜色方案配置（背景色、前景色、ANSI颜色映射）
- 字体选择和大小
- 光标样式（块状、下划线、竖线）
- 滚动缓冲区大小

---

### 6. Shell选择 (Shell Selection)

**优先级**: 低

**描述**: 支持不同的Shell程序。

**需要实现**:
- PowerShell支持
- WSL/Bash支持
- 自定义Shell配置

**实现示例**:
```csharp
public enum ShellType
{
    CMD,
    PowerShell,
    WSL,
    Custom
}

public void StartTerminal(ShellType shellType)
{
    string command = shellType switch
    {
        ShellType.CMD => "cmd.exe",
        ShellType.PowerShell => "powershell.exe",
        ShellType.WSL => "wsl.exe",
        _ => "cmd.exe"
    };
    _terminal.Start(80, 25, command);
}
```

---

### 7. 性能优化 (Performance Optimization)

**优先级**: 中

**描述**: 大量输出时的性能优化。

**需要实现**:
- 虚拟化滚动（仅渲染可见行）
- 输出节流（批量处理更新）
- 限制滚动缓冲区大小

---

### 8. 高级输入处理 (Advanced Input Handling)

**优先级**: 中

**描述**: 完整的键盘输入支持。

**需要实现**:
- 箭头键、Home/End键支持
- Ctrl+C/Ctrl+V/Ctrl+Z等快捷键
- Tab键自动补全（需要Shell支持）
- 历史命令导航（上/下箭头）

---

### 9. 链接检测和点击 (Link Detection & Click)

**优先级**: 低

**描述**: 检测URL和文件路径，支持点击打开。

**需要实现**:
- 正则表达式检测URL和路径
- 鼠标悬停效果
- 点击打开浏览器或文件

---

### 10. 搜索功能 (Search Feature)

**优先级**: 低

**描述**: 在终端输出中搜索文本。

**需要实现**:
- Ctrl+F打开搜索框
- 高亮匹配结果
- 上一个/下一个导航

---

## 实现优先级建议

**第一阶段**（必要功能）:
1. ANSI转义序列解析器
2. 终端窗口大小调整
3. 高级输入处理

**第二阶段**（增强功能）:
4. 选择和复制功能
5. 多终端标签页
6. 性能优化

**第三阶段**（可选功能）:
7. 配置和主题
8. Shell选择
9. 链接检测
10. 搜索功能

---

## 技术参考

### 相关库和项目
- **Windows Terminal**: Microsoft官方开源终端
  - https://github.com/microsoft/terminal
  - 可以参考其ConPTY集成和ANSI解析实现

- **ConPTY文档**:
  - https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session

- **ANSI转义序列参考**:
  - https://gist.github.com/fnky/458719343aabd01cfb17a3a4f7296797

### 建议的NuGet包
- **Spectre.Console.Rendering**: ANSI解析和渲染
- **Terminal.Gui**: 终端UI框架参考

---

## 注意事项

1. **Windows版本要求**: ConPTY API需要Windows 10 1809或更高版本
2. **线程安全**: UI更新必须在UI线程上进行（使用Dispatcher）
3. **资源清理**: 确保正确释放ConPTY句柄和管道
4. **编码**: 使用UTF-8编码以支持多语言字符

---

## 示例代码片段

### ANSI颜色解析示例
```csharp
// 简化的ANSI颜色解析
public static class AnsiColors
{
    private static readonly Dictionary<int, Color> ColorMap = new()
    {
        { 30, Colors.Black },
        { 31, Colors.Red },
        { 32, Colors.Green },
        { 33, Colors.Yellow },
        { 34, Colors.Blue },
        { 35, Colors.Magenta },
        { 36, Colors.Cyan },
        { 37, Colors.White }
    };

    public static Color GetColor(int code)
    {
        return ColorMap.TryGetValue(code, out var color) ? color : Colors.White;
    }
}
```

### 窗口大小计算示例
```csharp
private (short cols, short rows) CalculateTerminalSize()
{
    var typeface = new Typeface(rtbOutput.FontFamily, 
        rtbOutput.FontStyle, 
        rtbOutput.FontWeight, 
        rtbOutput.FontStretch);
    
    var formattedText = new FormattedText(
        "M", // 使用M字符作为参考（通常是最宽的）
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        typeface,
        rtbOutput.FontSize,
        Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    double charWidth = formattedText.Width;
    double charHeight = formattedText.Height;

    short cols = (short)(rtbOutput.ActualWidth / charWidth);
    short rows = (short)(rtbOutput.ActualHeight / charHeight);

    return (Math.Max((short)1, cols), Math.Max((short)1, rows));
}
```

---

最后更新: 2025-01-26
