# ColorVision.UI

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

ColorVision 系统的核心UI控件库和框架支持，提供丰富的 UI 组件、系统功能和基础设施。它是整个应用程序 UI 层的基础，包含插件系统、属性编辑器、快捷键系统、菜单系统、搜索功能、多语言支持等核心功能。

## 作用范围

基础 UI 层，为所有上层模块提供统一的插件管理、属性编辑、快捷键管理、菜单系统、多语言支持、CUDA支持等基础服务。

## 主要功能点

### 插件系统 (PluginLoader)
- **动态加载** - 从 Plugins 目录自动扫描和加载插件
- **Manifest 支持** - 基于 manifest.json 的插件元数据管理
- **依赖检查** - 自动验证插件依赖的 ColorVision 组件版本
- **版本控制** - 支持插件版本管理和更新
- **启用/禁用** - 动态启用或禁用插件

### 属性编辑器 (PropertyEditor)
- **树形结构** - PropertyTreeNode 支持层次化属性展示
- **类型编辑器** - 支持多种类型的属性编辑器（布尔、枚举、字符串、数字等）
- **动态属性** - 支持运行时动态添加和移除属性
- **自定义编辑器** - 可扩展的自定义属性编辑器机制
- **可见性控制** - 支持属性的条件显示和隐藏

### 快捷键系统 (HotKey)
- **全局快捷键** - GlobalHotKeyManager 支持系统级快捷键
- **窗口快捷键** - WindowHotKeyManager 支持窗口级快捷键
- **动态注册** - 运行时动态注册和修改快捷键
- **冲突检测** - 自动检测快捷键冲突
- **持久化** - 快捷键配置保存和加载

### 菜单系统 (Menus)
- **基础菜单** - 提供标准的文件、编辑菜单（打开、保存、复制、粘贴等）
- **动态菜单** - 支持运行时动态添加和修改菜单项
- **菜单搜索** - MenuSearchProvider 支持菜单项搜索
- **命令绑定** - 与 WPF 命令系统集成

### 多语言支持 (LanguageManager)
- **资源文件** - 基于 .resx 的资源文件本地化
- **动态切换** - 运行时切换界面语言
- **自动加载** - 自动加载对应语言的资源
- **扩展支持** - 支持插件的多语言资源

### CUDA 支持 (CudaHelper)
- **设备检测** - 自动检测 NVIDIA CUDA 设备
- **初始化管理** - CudaInitializer 管理 CUDA 环境初始化
- **系统信息** - SystemHelper 提供 CUDA 相关的系统信息

### 主题配置 (ThemeConfig)
- **配置管理** - ThemeConfig 管理主题设置
- **动态切换** - 支持运行时主题切换
- **配置提供者** - ThemeConfigSetingProvider 集成配置系统

### 日志系统 (LogImp)
- **日志状态** - LogLoadState 管理日志加载状态
- **本地配置** - WindowLogLocalConfig 窗口日志本地配置
- **常量定义** - LogConstants 日志相关常量

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                      ColorVision.UI                           │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │PluginLoader │    │PropertyEditor│   │   HotKey    │      │
│  │             │    │             │    │             │      │
│  │ • 插件加载  │    │ • 属性树    │    │ • 全局快捷键│      │
│  │ • Manifest  │    │ • 类型编辑  │    │ • 窗口快捷键│      │
│  │ • 依赖检查  │    │ • 自定义编辑│    │ • 冲突检测  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    Menus    │    │   Language  │    │    CUDA     │      │
│  │             │    │             │    │             │      │
│  │ • 文件菜单  │    │ • 多语言    │    │ • 设备检测  │      │
│  │ • 编辑菜单  │    │ • 动态切换  │    │ • 初始化    │      │
│  │ • 菜单搜索  │    │ • 资源管理  │    │ • 系统信息  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI.Desktop - 桌面应用程序入口
- ColorVision.Solution - 解决方案管理
- ColorVision.ImageEditor - 图像编辑器
- ColorVision.Database - 数据库模块
- 所有其他 UI 模块

**引用的程序集**:
- ColorVision.Common - 通用工具类和接口
- 无其他 UI 层依赖（保持底层纯净）

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.UI\ColorVision.UI.csproj" />
```

### 基础使用示例

#### 1. 插件开发
```csharp
// 创建插件类
public class MyPlugin : IPlugin
{
    public string Header => "我的插件";
    public string Description => "这是一个示例插件";
    
    public void Execute()
    {
        // 插件执行逻辑
        MessageBox.Show("插件已执行！");
    }
}
```

#### 2. 快捷键注册
```csharp
// 创建全局快捷键
var hotKey = new HotKeys(
    "全局保存",
    new Hotkey { Key = Key.S, Modifiers = ModifierKeys.Control },
    () => { SaveDocument(); }
);
hotKey.Kinds = HotKeyKinds.Global;

// 注册快捷键
GlobalHotKeyManager.GetInstance(mainWindow).Register(hotKey);
```

#### 3. 多语言切换
```csharp
// 切换语言
LanguageManager.Current.ChangeLanguage("en");

// 获取资源字符串
var message = LanguageManager.Current.GetString("SaveSuccess");
```

#### 4. CUDA 检测
```csharp
// 检查 CUDA 是否可用
if (CudaHelper.IsCudaAvailable())
{
    var deviceCount = CudaHelper.GetDeviceCount();
    Console.WriteLine($"检测到 {deviceCount} 个 CUDA 设备");
}
```

#### 5. 主题配置
```csharp
// 设置主题
ThemeConfig.Instance.Theme = ThemeType.Dark;
ThemeConfig.Instance.Save();

// 应用主题
Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);
```

## 主要组件

### PluginLoader
插件加载器负责从 Plugins 目录扫描、验证和加载插件。

```csharp
public static class PluginLoader
{
    public static PluginLoaderrConfig Config => PluginLoaderrConfig.Instance;
    
    // 从默认路径加载插件
    public static void LoadPlugins()
    
    // 从指定路径加载插件
    public static void LoadPlugins(string path)
}
```

### PluginManifest
插件清单类，定义插件的元数据信息。

```csharp
public class PluginManifest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string DllName { get; set; }
    public string Author { get; set; }
}
```

### HotKeys
快捷键定义类，支持全局和窗口级快捷键。

```csharp
[Serializable]
public class HotKeys : INotifyPropertyChanged
{
    public string Name { get; set; }
    public Hotkey Hotkey { get; set; }
    public HotKeyKinds Kinds { get; set; }  // Global / Windows
    public bool IsGlobal { get; set; }
    public bool IsRegistered { get; }
    public HotKeyCallBackHanlder HotKeyHandler { get; set; }
    
    // 重置为默认快捷键
    public static void SetDefault()
}
```

### LanguageManager
多语言管理器，支持运行时语言切换。

```csharp
public class LanguageManager
{
    public static LanguageManager Current { get; }
    
    // 当前语言代码
    public string CurrentLanguage { get; set; }
    
    // 切换语言
    public void ChangeLanguage(string languageCode)
    
    // 获取资源字符串
    public string GetString(string key)
}
```

## 目录说明

- `Plugins/` - 插件系统相关类
  - `PluginLoader.cs` - 插件加载器
  - `PluginManifest.cs` - 插件清单
  - `PluginInfo.cs` - 插件信息
  - `DepsJson.cs` - 依赖JSON解析
- `PropertyEditor/` - 属性编辑器
  - `PropertyTreeNode.cs` - 属性树节点
  - `PropertyEditors.cs` - 属性编辑器集合
- `HotKey/` - 快捷键系统
  - `HotKeys.cs` - 快捷键定义
  - `GlobalHotKey/` - 全局快捷键管理
  - `WindowHotKey/` - 窗口快捷键管理
- `Menus/` - 菜单系统
  - `Base/` - 基础菜单项
- `Languages/` - 多语言支持
  - `LanguageManager.cs` - 语言管理器
- `CUDA/` - CUDA支持
  - `CudaHelper.cs` - CUDA帮助类
  - `CudaInitializer.cs` - CUDA初始化
- `Themes/` - 主题配置
  - `ThemeConfig.cs` - 主题配置
- `LogImp/` - 日志系统
  - `LogLoadState.cs` - 日志加载状态

## 开发调试

```bash
# 构建项目
dotnet build UI/ColorVision.UI/ColorVision.UI.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 插件开发
- 始终包含 manifest.json 文件
- 使用语义化版本号
- 正确处理依赖关系
- 避免循环依赖

### 2. 快捷键使用
- 避免与系统快捷键冲突
- 提供快捷键配置界面
- 支持快捷键重置功能
- 正确处理快捷键注销

### 3. 多语言支持
- 使用资源文件管理字符串
- 提供默认语言回退
- 支持动态语言切换
- 测试所有支持的语言

### 4. 属性编辑
- 使用类型安全的属性绑定
- 提供属性验证机制
- 支持属性的条件显示
- 优化大数据量的渲染性能

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/ui-components/ColorVision.UI.md)
- [配置管理指南](../../docs/00-getting-started/README.md)
- [ColorVision.UI.Desktop README](./../ColorVision.UI.Desktop/README.md)
- [ColorVision.Common README](./../ColorVision.Common/README.md)

## 更新日志

### v1.5.1.1 (2026-02)
- 支持 .NET 10.0
- 新增法语（Français）语言支持
- 新增俄语（Русский）语言支持
- 属性编辑控件支持 byte 类型的编辑和显示
- PropertyGrid 动态属性编辑器增强
- 插件依赖版本检查优化
- 全局/窗口快捷键系统重构

### v1.4.1.1 (2026-02)
- 菜单管理增强
- 算法管理窗口
- 配置管理优化

### v1.3.18.1 (2025-02)
- 基础配置管理
- 程序集加载
- 文件处理器工厂
- 显示管理

## 维护者

ColorVision UI团队
