# ColorVision.Themes

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 功能定位

主题管理和样式系统，提供五种预设主题（系统/浅色/深色/粉色/青色）、自定义控件、值转换器和窗口样式。

## 主要功能

### 主题管理
- **ThemeManager** — 主题切换核心类，支持运行时动态切换
- **五种主题** — UseSystem / Light / Dark / Pink / Cyan
- **系统主题跟随** — 自动适配 Windows 系统主题
- **标题栏颜色** — SetWindowTitleBarColor 匹配主题
- **配置持久化** — 主题选择自动保存

### 主题资源
| 资源文件 | 说明 |
|----------|------|
| `Base.xaml` | 基础共享样式 |
| `Dark.xaml` | 深色主题 |
| `White.xaml` | 浅色主题 |
| `Pink.xaml` | 粉色主题（标题栏 #E8A6C1） |
| `Cyan.xaml` | 青色主题（标题栏 #00796B） |
| `Menu.xaml` / `GroupBox.xaml` / `Icons.xaml` | 通用控件样式 |

### 自定义控件
- **MessageBox** — 主题化消息对话框
- **ProgressRing** — 进度环
- **LoadingOverlay** — 加载遮罩层
- **UploadControl** — 文件上传（拖拽 + 文件选择）
- **UploadWindow / UploadMsg** — 上传窗口和消息

### 值转换器
| 转换器 | 用途 |
|--------|------|
| `BooleanToVisibilityReConverter` | 布尔 → 可见性（反向） |
| `InverseBooleanConverter` | 布尔反向 |
| `MemorySizeConverter` | 字节 → 可读大小 |
| `EnumToVisibilityConverter` | 枚举 → 可见性 |
| `IntToVisibilityConverter` | 整数 → 可见性 |
| `WidthToBooleanConverter` | 宽度 → 布尔 |

### 窗口样式
- **BaseWindow** — 基础窗口样式
- **WindowHelper** — 窗口帮助类
- **WindowBlur** — 窗口模糊效果（DWM）

## 依赖关系

- **引用**: HandyControl 3.5.1
- **被引用**: ColorVision.UI, ColorVision.Scheduler, ColorVision.ImageEditor

## 构建

```bash
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj
```
