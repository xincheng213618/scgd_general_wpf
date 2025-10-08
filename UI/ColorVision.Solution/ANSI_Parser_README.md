# ANSI Escape Sequence Parser

## 概述 (Overview)

`AnsiEscapeSequenceParser` 是一个用于解析 ANSI/VT100 转义序列并将其转换为 WPF 格式化文本的解析器类。它使终端窗口能够正确显示彩色文本和格式化内容，而不是显示原始的转义序列代码。

The `AnsiEscapeSequenceParser` is a parser class that converts ANSI/VT100 escape sequences into WPF formatted text. It enables the terminal window to properly display colored text and formatting instead of showing raw escape sequence codes.

## 功能特性 (Features)

### ✅ 支持的颜色模式 (Supported Color Modes)

1. **16色标准 ANSI 颜色 (16-Color Standard ANSI)**
   - 前景色: `ESC[30-37m` (普通), `ESC[90-97m` (高亮)
   - 背景色: `ESC[40-47m` (普通), `ESC[100-107m` (高亮)
   - 示例: `\x1b[31mRed text\x1b[0m`

2. **256色模式 (256-Color Mode)**
   - 前景色: `ESC[38;5;{n}m` (n = 0-255)
   - 背景色: `ESC[48;5;{n}m` (n = 0-255)
   - 示例: `\x1b[38;5;196mBright red\x1b[0m`

3. **RGB 真彩色 (RGB True Color / 24-bit)**
   - 前景色: `ESC[38;2;{r};{g};{b}m`
   - 背景色: `ESC[48;2;{r};{g};{b}m`
   - 示例: `\x1b[38;2;255;100;50mOrange\x1b[0m`

### ✅ 支持的文本格式 (Supported Text Formatting)

- **粗体 (Bold)**: `ESC[1m`
- **斜体 (Italic)**: `ESC[3m`
- **下划线 (Underline)**: `ESC[4m`
- **重置 (Reset)**: `ESC[0m`
- **取消粗体**: `ESC[22m`
- **取消斜体**: `ESC[23m`
- **取消下划线**: `ESC[24m`

## 使用方法 (Usage)

### 基本用法 (Basic Usage)

```csharp
using System.Windows.Media;
using ColorVision.Solution;

// 要解析的 ANSI 文本
string ansiText = "\x1b[31mRed text\x1b[0m Normal \x1b[1;32mBold green\x1b[0m";

// 调用解析器
var inlines = AnsiEscapeSequenceParser.Parse(
    ansiText, 
    Colors.White,    // 默认前景色
    Colors.Black     // 默认背景色
);

// 添加到 WPF Paragraph
var paragraph = new Paragraph();
foreach (var inline in inlines)
{
    paragraph.Inlines.Add(inline);
}
```

### 在 TerminalManagerWindow 中使用

```csharp
private void AppendText(string text)
{
    if (string.IsNullOrEmpty(text)) return;

    // 解析 ANSI 转义序列
    var defaultForeground = Colors.White;
    var defaultBackground = Colors.Black;
    var inlines = AnsiEscapeSequenceParser.Parse(text, defaultForeground, defaultBackground);

    // 创建段落并添加格式化的文本
    var paragraph = new Paragraph { Margin = new Thickness(0) };
    foreach (var inline in inlines)
    {
        paragraph.Inlines.Add(inline);
    }

    rtbOutput.Document.Blocks.Add(paragraph);
    rtbOutput.ScrollToEnd();
}
```

## ANSI 颜色代码参考 (ANSI Color Code Reference)

### 标准前景色 (Standard Foreground Colors)
```
ESC[30m - 黑色 (Black)
ESC[31m - 红色 (Red)
ESC[32m - 绿色 (Green)
ESC[33m - 黄色 (Yellow)
ESC[34m - 蓝色 (Blue)
ESC[35m - 洋红 (Magenta)
ESC[36m - 青色 (Cyan)
ESC[37m - 白色 (White)
```

### 高亮前景色 (Bright Foreground Colors)
```
ESC[90m - 亮黑色 (Bright Black / Gray)
ESC[91m - 亮红色 (Bright Red)
ESC[92m - 亮绿色 (Bright Green)
ESC[93m - 亮黄色 (Bright Yellow)
ESC[94m - 亮蓝色 (Bright Blue)
ESC[95m - 亮洋红 (Bright Magenta)
ESC[96m - 亮青色 (Bright Cyan)
ESC[97m - 亮白色 (Bright White)
```

### 标准背景色 (Standard Background Colors)
```
ESC[40m - 黑色背景 (Black Background)
ESC[41m - 红色背景 (Red Background)
ESC[42m - 绿色背景 (Green Background)
ESC[43m - 黄色背景 (Yellow Background)
ESC[44m - 蓝色背景 (Blue Background)
ESC[45m - 洋红背景 (Magenta Background)
ESC[46m - 青色背景 (Cyan Background)
ESC[47m - 白色背景 (White Background)
```

### 组合示例 (Combination Examples)
```csharp
// 粗体红色文本
"\x1b[1;31mBold Red\x1b[0m"

// 黄色文本在蓝色背景上
"\x1b[33;44mYellow on Blue\x1b[0m"

// 粗体、斜体、下划线的绿色文本
"\x1b[1;3;4;32mBold Italic Underlined Green\x1b[0m"

// 256色 - 亮红色
"\x1b[38;5;196mBright Red (256-color)\x1b[0m"

// RGB真彩色 - 橙色
"\x1b[38;2;255;165;0mOrange (RGB)\x1b[0m"
```

## 测试 (Testing)

项目包含 `AnsiParserTests.cs` 文件，提供了测试用例和演示代码。

### 运行测试 (Run Tests)

```csharp
using ColorVision.Solution;

// 验证解析器功能
bool success = AnsiParserTests.VerifyParser();

// 打印所有测试字符串
AnsiParserTests.PrintTests();

// 生成彩色演示文本
string demo = AnsiParserTests.GenerateColorDemo();
Console.WriteLine(demo);
```

### 在终端中测试 (Test in Terminal)

打开终端窗口后，可以尝试以下命令:

#### Windows CMD
```cmd
powershell -c "Write-Host 'Red Text' -ForegroundColor Red"
powershell -c "Write-Host 'Green Text' -ForegroundColor Green"
```

#### PowerShell
```powershell
Write-Host "Red Text" -ForegroundColor Red
Write-Host "Green Background" -BackgroundColor Green
Write-Host "`e[1mBold`e[0m `e[3mItalic`e[0m `e[4mUnderline`e[0m"
```

#### Git (需要已安装 Git)
```cmd
git log --oneline --graph --color
git diff --color
git status
```

#### Python (需要已安装 Python)
```python
python
>>> print("\033[31mRed\033[0m \033[32mGreen\033[0m \033[34mBlue\033[0m")
```

## 技术实现细节 (Technical Implementation Details)

### 解析流程 (Parsing Flow)

1. **正则表达式匹配** (Regex Matching)
   - 使用正则表达式 `\x1b\[([0-9;]*)m` 查找所有转义序列
   - 提取序列中的参数代码

2. **状态维护** (State Maintenance)
   - 维护当前文本格式状态 (颜色、粗体、斜体等)
   - 每个转义序列更新状态

3. **文本分段** (Text Segmentation)
   - 将文本分为多个片段
   - 每个片段应用相应的格式

4. **WPF 转换** (WPF Conversion)
   - 创建 `Run` 对象
   - 应用 `Foreground`、`Background`、`FontWeight`、`FontStyle`、`TextDecorations`

### 颜色映射 (Color Mapping)

解析器使用精心调校的颜色映射，与主流终端 (Windows Terminal, VSCode) 保持一致:

```csharp
// 标准 16 色映射
{ 0, Color.FromRgb(0, 0, 0) },         // Black
{ 1, Color.FromRgb(205, 49, 49) },     // Red
{ 2, Color.FromRgb(13, 188, 121) },    // Green
{ 3, Color.FromRgb(229, 229, 16) },    // Yellow
{ 4, Color.FromRgb(36, 114, 200) },    // Blue
{ 5, Color.FromRgb(188, 63, 188) },    // Magenta
{ 6, Color.FromRgb(17, 168, 205) },    // Cyan
{ 7, Color.FromRgb(229, 229, 229) },   // White
// ... 高亮颜色 (8-15)
```

### 256色实现 (256-Color Implementation)

- **0-15**: 标准 16 色
- **16-231**: 6×6×6 色立方体 (216 色)
  - R: 0, 51, 102, 153, 204, 255
  - G: 0, 51, 102, 153, 204, 255
  - B: 0, 51, 102, 153, 204, 255
- **232-255**: 24 级灰度

## 兼容性 (Compatibility)

### 支持的终端程序 (Supported Terminal Programs)
- ✅ Windows CMD
- ✅ PowerShell
- ✅ Git Bash
- ✅ Python REPL
- ✅ Node.js REPL
- ✅ npm/yarn 输出
- ✅ 任何使用标准 ANSI 转义序列的程序

### 已知限制 (Known Limitations)
- ❌ 不支持光标移动序列 (如 `ESC[{n}A`, `ESC[{n}B`)
- ❌ 不支持清屏序列 (如 `ESC[2J`)
- ❌ 不支持闪烁、隐藏等较少使用的格式
- ℹ️ 仅解析 SGR (Select Graphic Rendition) 序列

## 性能考虑 (Performance Considerations)

- 使用编译的正则表达式 (`RegexOptions.Compiled`) 提高性能
- 避免不必要的对象创建
- 高效的字符串处理

## 参考资料 (References)

- [ANSI Escape Codes - Wikipedia](https://en.wikipedia.org/wiki/ANSI_escape_code)
- [Console Virtual Terminal Sequences - Microsoft](https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences)
- [XTerm Control Sequences](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)
- [ANSI Escape Codes Gist](https://gist.github.com/fnky/458719343aabd01cfb17a3a4f7296797)

## 更新历史 (Update History)

### Version 1.0 (2025-01-26)
- ✨ 初始实现
- ✅ 16色支持
- ✅ 256色支持
- ✅ RGB 真彩色支持
- ✅ 文本格式化支持 (粗体、斜体、下划线)
- ✅ 完整测试套件

## 许可证 (License)

与 ColorVision.Solution 项目相同。
