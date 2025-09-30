# SystemMonitor 插件

## 1. 插件概述

- **名称**: SystemMonitor (性能监控)
- **类型**: 系统工具
- **功能**: 实时监控系统性能指标，包括 CPU 使用率、内存占用、磁盘空间和时间显示
- **适用 ColorVision 版本**: ≥ 1.3.8.10
- **依赖的核心模块**: ColorVision.UI, ColorVision.Common

## 2. 功能特性

- **实时系统监控**: 监控 CPU 使用率、内存占用、磁盘使用情况
- **状态栏集成**: 在主程序状态栏显示时间和内存使用信息
- **菜单扩展**: 在工具菜单中添加性能监控选项
- **配置面板**: 实现 IConfigSettingProvider 提供设置面板
- **可配置更新频率**: 支持自定义监控数据更新间隔
- **缓存清理**: 提供系统缓存和日志清理功能
- **多磁盘监控**: 显示所有可用磁盘驱动器的使用情况
- **国际化支持**: 支持多语言资源文件

## 3. 目录结构

```
SystemMonitor/
├── manifest.json                    # 插件清单文件
├── SystemMonitor.csproj            # 项目文件
├── README.md                        # 说明文档
├── SystemMonitors.cs               # 主要业务逻辑类
├── SystemMonitorControl.xaml       # WPF 用户控件界面
├── SystemMonitorControl.xaml.cs    # 用户控件代码隐藏
└── Properties/                      # 资源文件目录
    ├── Resources.resx               # 默认资源文件
    ├── Resources.Designer.cs        # 自动生成的资源类
    ├── Resources.en.resx           # 英文资源
    ├── Resources.fr.resx           # 法文资源
    ├── Resources.ja.resx           # 日文资源
    ├── Resources.ko.resx           # 韩文资源
    ├── Resources.ru.resx           # 俄文资源
    └── Resources.zh-Hant.resx      # 繁体中文资源
```

### manifest.json 字段说明:
- `manifest_version`: 清单版本 (1)
- `id`: 插件唯一标识符 ("SystemMonitor")
- `name`: 插件显示名称 ("性能监控")
- `version`: 插件版本 ("1.0")
- `description`: 插件描述 ("增强的电脑性能监控插件")
- `dllpath`: 主程序集路径 ("SystemMonitor.dll")
- `requires`: 最低 ColorVision 版本要求 ("1.3.8.10")

## 4. 架构与生命周期

### 插件加载方式
- **运行时 Assembly 扫描**: 通过 manifest.json 识别插件
- **PostBuild 复制**: 编译后自动复制到主程序 Plugins 目录
- **依赖注入**: 通过 ColorVision.UI 的配置服务系统注册

### 入口类型与初始化流程
1. **配置服务注册**: SystemMonitorSetting 通过 ConfigService 注册
2. **单例模式**: SystemMonitors 类使用单例模式确保唯一实例
3. **性能计数器初始化**: 异步初始化 Windows 性能计数器
4. **定时器启动**: 根据配置的更新频率启动监控定时器

### 与宿主通信方式
- **接口实现**: 实现 IConfigSettingProvider, IMenuItemProvider, IStatusBarProvider
- **配置绑定**: 通过 ConfigService 与主程序配置系统集成
- **事件通知**: 使用 RelayCommand 处理用户交互
- **数据绑定**: 通过 WPF 数据绑定与 UI 同步

## 5. 关键扩展点实现

### 配置 (IConfig)
```csharp
public class SystemMonitorSetting : ViewModelBase, IConfig
{
    public int UpdateSpeed { get; set; } = 1000;
    public string DefaultTimeFormat { get; set; } = "yyyy/MM/dd HH:mm:ss";
    public bool IsShowTime { get; set; }
    public bool IsShowRAM { get; set; }
}
```

### 配置面板提供者 (IConfigSettingProvider)
```csharp
public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
{
    return new List<ConfigSettingMetadata> {
        new ConfigSettingMetadata
        {
            Name = Resources.PerformanceTest,
            Description = Resources.PerformanceTest,
            Order = 10,
            Type = ConfigSettingType.TabItem,
            Source = SystemMonitors.GetInstance(),
            UserControl = new SystemMonitorControl(),
        }
    };
}
```

### 菜单扩展 (IMenuItemProvider)
```csharp
public IEnumerable<MenuItemMetadata> GetMenuItems()
{
    return new List<MenuItemMetadata>
    {
        new MenuItemMetadata()
        {
            OwnerGuid = "Tool",
            GuidId = "SystemMonitor",
            Header = Resources.PerformanceTest,
            Order = 500,
            Command = new RelayCommand(A => {
                Window window = new Window() { 
                    Title = Resources.PerformanceTest, 
                    Owner = Application.Current.GetActiveWindow()
                };
                window.Content = new SystemMonitorControl();
                window.Show();
            })
        }
    };
}
```

### 状态栏提供者 (IStatusBarProvider)
```csharp
public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
{
    return new List<StatusBarMeta>
    {
        new StatusBarMeta()
        {
            Name = "Time",
            Order = 12,
            Type = StatusBarType.Text,
            BindingName = nameof(SystemMonitors.Time),
            VisibilityBindingName = "Config.IsShowTime",
            Source = SystemMonitors.GetInstance()
        },
        new StatusBarMeta()
        {
            Name = "RAM",
            Order = 10,
            Type = StatusBarType.Text,
            BindingName = nameof(SystemMonitors.MemoryThis),
            VisibilityBindingName = "Config.IsShowRAM",
            Source = SystemMonitors.GetInstance()
        }
    };
}
```

## 6. 安装与部署

### 自动部署
插件编译后会自动通过 PostBuild 事件复制到主程序的 Plugins 目录：

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="
        set DebugPluginsDir=$(SolutionDir)ColorVision\bin\x64\Debug\net8.0-windows\Plugins\$(TargetName)
        set ReleasePluginsDir=$(SolutionDir)ColorVision\bin\x64\Release\net8.0-windows\Plugins\$(TargetName)
        
        if not exist &quot;%DebugPluginsDir%&quot; (mkdir &quot;%DebugPluginsDir%&quot;)
        if not exist &quot;%ReleasePluginsDir%&quot; (mkdir &quot;%ReleasePluginsDir%&quot;)
        
        copy &quot;$(OutDir)$(TargetName)$(TargetExt)&quot; &quot;%DebugPluginsDir%&quot;
        copy &quot;$(OutDir)$(TargetName)$(TargetExt)&quot; &quot;%ReleasePluginsDir%&quot;
        
        if exist &quot;$(OutDir)manifest.json&quot; (
            copy &quot;$(OutDir)manifest.json&quot; &quot;%DebugPluginsDir%&quot;
            copy &quot;$(OutDir)manifest.json&quot; &quot;%ReleasePluginsDir%&quot;
        )" />
</Target>
```

### 手动部署
将以下文件复制到 `ColorVision\bin\x64\Release\net8.0-windows\Plugins\SystemMonitor\` 目录：
- SystemMonitor.dll
- manifest.json
- README.md (可选)

## 7. 构建说明

### 基本构建
```bash
dotnet build Plugins/SystemMonitor/SystemMonitor.csproj -c Release
```

### 目标框架
- 主要目标: .NET 8.0-windows
- 平台支持: x64, ARM64
- WPF 应用程序: 启用
- 代码签名: 启用 (使用 ColorVision.snk)

### 构建输出
编译成功后会在以下位置生成文件：
- `Plugins/SystemMonitor/bin/Release/net8.0-windows/SystemMonitor.dll`
- 自动复制到主程序 Plugins 目录

## 8. 使用指南

### 启动验证
启动 ColorVision 主程序后，验证插件加载成功的方式：
1. **菜单项**: 工具菜单中出现"性能监控"选项
2. **设置面板**: 设置窗口中出现"性能监控"配置页
3. **状态栏**: 根据配置显示时间和内存使用信息

### 典型操作步骤
1. **打开监控面板**: 工具 → 性能监控
2. **配置更新频率**: 在配置面板中设置更新速度(毫秒)
3. **设置时间格式**: 自定义状态栏时间显示格式
4. **启用状态栏显示**: 开启时间和RAM显示开关
5. **清理缓存**: 点击"清理"按钮清除系统缓存和日志

### 配置修改位置
- **设置窗口**: 主程序设置 → 性能监控选项卡
- **右侧属性**: 监控面板内的参数设置
- **状态栏控制**: 通过配置开关控制显示项目

## 9. 配置项说明

| 配置键 | 类型 | 默认值 | 说明 | 是否热更新 |
|--------|------|--------|------|------------|
| UpdateSpeed | int | 1000 | 监控数据更新间隔(毫秒) | 是 |
| DefaultTimeFormat | string | "yyyy/MM/dd HH:mm:ss" | 时间显示格式 | 是 |
| IsShowTime | bool | false | 状态栏显示时间 | 是 |
| IsShowRAM | bool | false | 状态栏显示内存使用 | 是 |

## 10. 日志与诊断

### 日志前缀约定
插件相关日志使用 "SystemMonitor" 前缀标识

### 常见错误与排查
1. **性能计数器初始化失败**: 
   - 检查 Windows 性能计数器服务是否运行
   - 确认当前用户有足够权限访问性能计数器

2. **插件未加载**:
   - 检查 manifest.json 是否存在且格式正确
   - 确认 SystemMonitor.dll 文件完整性
   - 验证最低版本要求是否满足

3. **状态栏不显示**:
   - 检查对应的显示开关是否开启
   - 确认数据绑定是否正常工作

## 11. 示例代码片段

### 注册菜单
```csharp
public class SystemMonitorProvider : IMenuItemProvider
{
    public IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        return new List<MenuItemMetadata>
        {
            new MenuItemMetadata()
            {
                OwnerGuid = "Tool",
                GuidId = "SystemMonitor",
                Header = "性能监控",
                Order = 500,
                Command = new RelayCommand(ShowMonitorWindow)
            }
        };
    }
}
```

### 创建配置类
```csharp
public class SystemMonitorSetting : ViewModelBase, IConfig
{
    public int UpdateSpeed 
    { 
        get => _UpdateSpeed; 
        set { _UpdateSpeed = value; OnPropertyChanged(); } 
    }
    private int _UpdateSpeed = 1000;
}
```

### 性能监控实现
```csharp
private void TimeRun(object? state)
{
    if (PerformanceCounterIsOpen)
    {
        try
        {
            if (Config.IsShowTime)
                Time = DateTime.Now.ToString(Config.DefaultTimeFormat);
        }
        catch (Exception ex)
        {
            // 日志记录错误
        }
    }
}
```

## 12. 版本与变更

### 当前版本
- **插件版本**: 1.0
- **项目版本**: 1.0.0.4
- **兼容版本**: ColorVision ≥ 1.3.8.10

### 主要特性
- 实时系统性能监控
- 状态栏时间和内存显示
- 磁盘使用情况查看
- 缓存清理功能
- 多语言支持

## 13. 兼容性

### 运行要求
- **操作系统**: Windows 10/11 x64 或 ARM64
- **.NET 运行时**: .NET 8.0 Desktop Runtime
- **权限要求**: 读取系统性能计数器权限

### 平台支持
- **x64**: 完全支持
- **ARM64**: 完全支持
- **Windows 性能计数器**: 必需

## 14. 约束与限制

### 性能注意事项
- 默认更新频率为 1 秒，过高频率可能影响系统性能
- 性能计数器初始化采用异步方式，避免阻塞 UI 线程
- 内存监控数据获取有一定延迟

### 已知限制
- 依赖 Windows 性能计数器，在部分受限环境可能无法正常工作
- CPU 使用率监控在某些虚拟化环境中可能不准确
- 磁盘信息为静态获取，不会实时刷新驱动器列表

## 15. 未来规划

### 计划中的扩展
- 添加 GPU 使用率监控
- 支持自定义性能指标
- 增加历史数据图表显示
- 支持性能告警功能
- 导出监控数据到文件

## 16. 许可说明

本插件继承主项目 ColorVision 的许可证条款。

**版权**: Copyright (C) 2025 ColorVision Corporation  
**公司**: ColorVision Corp.  
**作者**: xincheng  

## 17. 致谢

### 使用的第三方库
- **HandyControl**: WPF UI 组件库，用于波浪进度条等控件
- **WPF UI**: 现代化 WPF 控件库，提供开关控件等
- **.NET Performance Counters**: 系统性能数据获取

---

*文档版本: 1.0 | 最后更新: 2025-01-01 | 状态: 正式发布*
