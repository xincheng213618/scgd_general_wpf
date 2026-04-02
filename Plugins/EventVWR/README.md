# EventVWR Plugin - 事件查看器与Dump管理

> 版本: 1.0.0 | 目标框架: .NET 8.0 / .NET 10.0 Windows | 依赖: ColorVision.UI, ColorVision.Common

## 🎯 功能定位

EventVWR 是 ColorVision 的系统事件查看与应用程序崩溃转储(Dump)管理插件，提供Windows事件日志查看和应用程序Dump文件配置功能，帮助开发者和运维人员快速诊断系统问题和应用崩溃。

## 主要功能点

### 事件查看器 (EventWindow)
- **Windows事件日志查看** - 查看系统应用程序事件日志
- **错误事件筛选** - 自动筛选并显示错误级别(EventLogEntryType.Error)的事件
- **事件详情展示** - 点击事件查看详细错误信息和堆栈跟踪
- **时间倒序排列** - 最新发生的事件显示在最前面
- **现代化界面** - 采用Material Design风格的用户界面

### Dump文件管理 (DumpConfig)
- **Dump类型配置** - 支持自定义、小型、完全三种Dump类型
- **注册表配置** - 通过Windows注册表配置LocalDumps
- **Dump文件夹设置** - 自定义Dump文件保存路径
- **Dump数量限制** - 设置保留的Dump文件数量
- **自定义Dump标志** - 支持MinidumpType标志组合

### 菜单集成
- **帮助菜单集成** - 在帮助菜单下添加Dump文件设置子菜单
- **动态菜单项** - 根据当前配置动态显示选中状态
- **快捷操作** - 一键保存Dump、清空Dump配置

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                      EventVWR Plugin                          │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ EventWindow │    │  DumpConfig │    │   MenuDump  │      │
│  │             │    │             │    │             │      │
│  │ • 事件列表  │    │ • Dump类型  │    │ • 菜单集成  │      │
│  │ • 详情查看  │    │ • 注册表操作│    │ • 动态项    │      │
│  │ • 错误筛选  │    │ • 路径配置  │    │ • 快捷操作  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │  DumpType   │    │MinidumpType │                          │
│  │             │    │             │                          │
│  │ • Custom    │    │ • Normal    │                          │
│  │ • Mini      │    │ • WithData  │                          │
│  │ • Full      │    │ • FullMemory│                          │
│  └─────────────┘    └─────────────┘                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 查看Windows事件日志

```csharp
// 打开事件查看器窗口
var eventWindow = new EventWindow();
eventWindow.Show();
```

界面说明：
- **左侧列表** - 显示所有错误级别的事件，包含时间和来源
- **右侧详情** - 显示选中事件的详细错误信息
- **状态栏** - 显示当前状态

### 配置Dump文件

```csharp
// 获取Dump配置
var dumpConfig = new DumpConfig();

// 设置Dump类型
dumpConfig.DumpType = DumpType.Mini;

// 设置Dump文件夹
dumpConfig.DumpFolder = @"C:\CrashDumps";

// 设置Dump数量限制
dumpConfig.DumpCount = 10;

// 应用配置（需要管理员权限）
dumpConfig.SetDump();
```

### Dump类型说明

| 类型 | 值 | 说明 |
|------|-----|------|
| Custom | 0 | 自定义转储，使用CustomDumpFlags指定详细选项 |
| Mini | 1 | 小型转储，包含基本信息，文件较小 |
| Full | 2 | 完全转储，包含完整内存信息，文件较大 |

### MinidumpType标志

参考 [Microsoft文档](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ne-minidumpapiset-minidump_type)

常用标志：
- `MiniDumpNormal` (0x00000000) - 正常小型转储
- `MiniDumpWithDataSegs` (0x00000001) - 包含数据段
- `MiniDumpWithFullMemory` (0x00000002) - 包含完整内存
- `MiniDumpWithHandleData` (0x00000004) - 包含句柄数据

## 主要组件

### EventWindow
事件查看器主窗口，显示Windows事件日志。

```csharp
public partial class EventWindow : Window
{
    public ObservableCollection<EventLogEntry> logEntries { get; set; }
    
    // 加载应用程序事件日志中的错误事件
    private void Window_Initialized(object sender, EventArgs e);
    
    // 显示选中事件的详细信息
    private void ListViewEvent_SelectionChanged(object sender, SelectionChangedEventArgs e);
}
```

### DumpConfig
Dump文件配置类，管理Windows Error Reporting的LocalDumps配置。

```csharp
public class DumpConfig : IConfig
{
    // Dump文件夹路径
    public string DumpFolder { get; set; }
    
    // Dump类型
    public DumpType DumpType { get; set; }
    
    // Dump文件数量限制
    public int DumpCount { get; set; }
    
    // 自定义Dump标志
    public MinidumpType CustomDumpFlags { get; set; }
    
    // 应用配置到注册表
    public void SetDump();
    
    // 保存当前Dump
    public void SaveDump();
    
    // 清除Dump配置
    public void ClearDump();
}
```

### EventVWRPlugins
插件入口类。

```csharp
public class EventVWRPlugins : IPluginBase
{
    public override string Header => "事件插件";
    public override string Description => "增强的异常管理,提供事件插件和Dump设置";
}
```

## 目录说明

- `EventWindow.xaml/cs` - 事件查看器窗口
- `Dump/` - Dump文件管理
  - `DumpConfig.cs` - Dump配置
  - `DumpType.cs` - Dump类型枚举
  - `MinidumpType.cs` - Minidump类型标志枚举
  - `MenuDump.cs` - Dump菜单项
  - `DumpFileCollector.cs` - Dump文件收集器
- `ExportEventWindow.cs` - 事件窗口导出

## 权限要求

**注意**: 配置Dump文件需要管理员权限。

```csharp
// 检查管理员权限
if (!Tool.IsAdministrator())
{
    MessageBox.Show("操作需要使用管理员权限", "权限不足");
    return;
}
```

## 注册表路径

Dump配置存储在以下注册表路径：

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{AppName}.exe
```

配置项：
- `DumpFolder` - Dump文件保存文件夹路径
- `DumpCount` - 保留的Dump文件数量
- `DumpType` - Dump类型 (0=Custom, 1=Mini, 2=Full)
- `CustomDumpFlags` - 自定义Dump标志 (当DumpType=0时使用)

## 开发调试

```bash
# 构建项目
dotnet build Plugins/EventVWR/EventVWR.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. Dump文件管理
- 定期清理旧的Dump文件以释放磁盘空间
- 使用Mini类型进行日常调试，Full类型进行深度分析
- 将Dump文件夹设置在非系统盘以避免系统盘空间不足

### 2. 事件日志查看
- 关注重复出现的错误事件
- 结合时间戳定位问题发生时机
- 查看事件来源确定问题组件

### 3. 权限处理
- 在配置Dump设置前检查管理员权限
- 提供清晰的权限不足提示
- 考虑使用UAC提升权限

## 相关文档链接

- [Windows Error Reporting 文档](https://docs.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps)
- [MinidumpType 枚举文档](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ne-minidumpapiset-minidump_type)
- [ColorVision.UI README](../ColorVision.UI/README.md)

## 更新日志

### v1.0.0（2026-02）
- 初始版本发布
- Windows事件日志查看功能
- Dump文件配置管理
- 菜单集成

## 维护者

ColorVision 插件团队
