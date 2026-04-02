# ColorVision Plugins

> 插件系统目录，包含系统扩展插件和第三方功能模块

## 插件系统概述

ColorVision 采用插件化架构，所有功能模块都以插件形式加载。插件位于 `Plugins` 目录下，主程序启动时会自动扫描并加载所有有效插件。

### 插件特点

- **动态加载** - 运行时自动发现和加载插件
- **独立部署** - 每个插件独立目录，便于管理和更新
- **版本兼容** - 通过 manifest.json 声明依赖版本
- **菜单集成** - 自动集成到主程序菜单系统
- **配置持久化** - 支持插件级配置保存和恢复

## 标准插件列表

| 插件名称 | 版本 | 功能描述 | 目标框架 |
|----------|------|----------|----------|
| [EventVWR](./EventVWR/) | 1.0.0 | Windows事件查看器与Dump文件管理 | .NET 8.0/10.0 |
| [SystemMonitor](./SystemMonitor/) | 1.0.1 | 系统性能监控工具（CPU/内存/磁盘） | .NET 8.0/10.0 |
| [Pattern](./Pattern/) | 1.0.0 | 图卡生成工具，支持11种测试图案 | .NET 8.0/10.0 |
| [ScreenRecorder](./ScreenRecorder/) | 1.0.0 | 屏幕录制工具，支持多源录制 | .NET 8.0/10.0 |
| [WindowsServicePlugin](./WindowsServicePlugin/) | 1.0.0 | Windows服务管理与运维工具 | .NET 8.0/10.0 |
| [ImageProjector](./ImageProjector/) | 1.0.0 | 图片投影工具，支持多显示器 | .NET 8.0/10.0 |
| [Spectrum](./Spectrum/) | 2.1.4.0 | 光谱仪测试与色彩分析工具 | .NET 8.0/10.0 |

## 插件目录结构

每个插件目录遵循标准结构：

```
Plugins/
├── PluginName/
│   ├── manifest.json          # 插件清单文件（必需）
│   ├── PluginName.csproj      # 项目文件
│   ├── README.md              # 插件说明文档
│   ├── CHANGELOG.md           # 更新日志（可选）
│   ├── App.xaml/.cs           # 应用程序定义
│   ├── MainWindow.xaml/.cs    # 主窗口
│   ├── Sources/               # 源代码目录
│   ├── Properties/            # 资源文件
│   └── Assets/                # 静态资源
```

## manifest.json 格式

```json
{
  "manifest_version": 1,
  "Id": "PluginName",
  "name": "插件显示名称",
  "version": "1.0.0",
  "description": "插件功能描述",
  "dllpath": "PluginName.dll",
  "requires": "1.3.12.0",
  "author": "作者名称",
  "entry_point": "Namespace.PluginClass"
}
```

字段说明：
- `manifest_version` - 清单格式版本
- `Id` - 插件唯一标识符
- `name` - 插件显示名称
- `version` - 插件版本号
- `description` - 插件功能描述
- `dllpath` - 主程序集路径
- `requires` - 最低 ColorVision 版本要求
- `author` - 作者信息
- `entry_point` - 插件入口类（可选）

## 插件开发指南

### 快速开始

1. **创建项目**
   ```bash
   dotnet new wpf -n MyPlugin -o Plugins/MyPlugin
   ```

2. **实现插件接口**
   ```csharp
   public class MyPlugin : IPluginBase
   {
       public override string Header => "我的插件";
       public override string Description => "插件功能描述";
       
       public override void Execute()
       {
           // 插件执行逻辑
       }
   }
   ```

3. **创建 manifest.json**
   ```json
   {
     "manifest_version": 1,
     "Id": "MyPlugin",
     "name": "我的插件",
     "version": "1.0.0",
     "description": "插件功能描述",
     "dllpath": "MyPlugin.dll",
     "requires": "1.3.12.0"
   }
   ```

4. **配置构建输出**
   ```xml
   <Target Name="PostBuild" AfterTargets="PostBuildEvent">
     <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" /&#xD;&#xA;" />
   </Target>
   ```

### 详细文档

- [插件开发概览](../docs/02-developer-guide/plugin-development/overview.md)
- [插件开发入门](../docs/02-developer-guide/plugin-development/getting-started.md)
- [插件生命周期](../docs/02-developer-guide/plugin-development/lifecycle.md)
- [API参考 - 插件](../docs/04-api-reference/plugins/)

## 插件分类

### 系统工具类
- **EventVWR** - 系统事件查看与Dump管理
- **SystemMonitor** - 系统性能监控
- **WindowsServicePlugin** - Windows服务管理

### 图像处理类
- **Pattern** - 测试图案生成
- **ImageProjector** - 图片投影工具
- **ScreenRecorder** - 屏幕录制

### 专业测试类
- **Spectrum** - 光谱仪测试与色彩分析

## 构建所有插件

```bash
# 构建所有插件
dotnet build Plugins/Directory.Build.props

# 构建特定插件
dotnet build Plugins/Spectrum/Spectrum.csproj

# 发布模式构建
dotnet build Plugins/ -c Release
```

## 插件调试

1. 设置启动项目为 ColorVision
2. 在插件代码中设置断点
3. 按 F5 启动调试
4. 触发插件功能，断点将自动命中

## 常见问题

### 插件无法加载
- 检查 manifest.json 格式是否正确
- 确认 requires 版本与主程序兼容
- 验证 dllpath 指向的文件是否存在

### 菜单未显示
- 确保插件类正确实现 IPlugin 或 IPluginBase 接口
- 检查 OwnerGuid 是否正确设置
- 验证菜单排序 Order 值

### 依赖冲突
- 确保插件引用的库与主程序兼容
- 避免引用不同版本的相同库
- 使用主程序已加载的共享库

## 维护者

ColorVision 插件团队

## 许可证

所有插件遵循 ColorVision 主项目许可证。
