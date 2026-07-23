# 插件开发入门

本页提供当前仓库可执行的最短插件开发路径，不再沿用旧版通用宿主、异步生命周期和 `plugin.json` 示例。

## 先准备什么

- Windows 开发环境
- .NET 8.0 SDK
- WPF 开发工具链
- 当前仓库源码和主程序可运行输出

## 最小开发路径

### 1. 新建插件项目

建议把插件项目直接建在 `Plugins/<PluginId>/` 下，目标框架保持为 `net8.0-windows`。如果插件带界面，启用 WPF。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />

如果需要显式指定入口类型，可以继续补 `entry_point`。

## 4. 把产物复制到主程序插件目录

主程序运行时会从自己的输出目录扫描 `Plugins/`，所以调试时需要把插件产物复制进去。

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</Target>
```

如果你本地输出目录不同，应按实际主程序输出路径调整。

## 5. 运行和调试

1. 构建主程序。
2. 构建插件项目，确认 DLL 和 `manifest.json` 已复制到插件目录。
3. 启动 `ColorVision/ColorVision.csproj`。
4. 在对应菜单、工具页或插件管理界面验证插件是否被加载。

## 推荐参考实现

- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/README.md`

这些示例已经覆盖了基础插件入口和菜单扩展两类常见模式。

## 常见问题

### 插件没有被发现

- 检查 `manifest.json` 是否存在
- 检查 `dllpath` 指向的 DLL 是否真实存在
- 检查插件目录是否已经复制到主程序输出目录下的 `Plugins/<PluginId>/`

### 插件被发现但功能没出现

- 检查是否只实现了基础插件类，但没有实现需要的 provider 接口
- 检查入口类型是否有公开无参构造
- 检查类型是否为非抽象、非泛型开放类型

### 依赖冲突

- 不要重复打包平台自带的 `ColorVision.*.dll`
- 若插件带 `.deps.json`，确认依赖版本不高于目标平台

## 下一步

- 想理解平台如何扫描和装载插件：看 [插件生命周期](./lifecycle.md)
- 想先了解整体结构：看 [插件开发概览](./overview.md)
