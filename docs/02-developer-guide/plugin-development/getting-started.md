# 插件开发入门

本页给出当前仓库可执行的最短路径。官方插件位于 `Plugins/<PluginId>/`，面向 Windows x64 和 `net10.0-windows`；具体属性仍以 `Plugins/Directory.Build.props` 与插件 `.csproj` 为准。

## 1. 建立项目

带 WPF UI 的最小项目可以写成：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <VersionPrefix>1.0.0.0</VersionPrefix>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="manifest.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="README.md" CopyToOutputDirectory="PreserveNewest" />
    <None Update="CHANGELOG.md" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <Import Project="..\..\PluginProject.HostCopy.targets" />
</Project>
```

无界面插件不需要 `UseWPF`。只引用实际使用的宿主项目，不要把宿主自带的 `ColorVision.*.dll` 作为私有副本重新分发。

## 2. 添加 manifest

在项目根目录创建 `manifest.json`：

```json
{
  "manifest_version": 1,
  "id": "MyPlugin",
  "name": "我的插件",
  "version": "1.0.0.0",
  "description": "插件说明",
  "dllpath": "MyPlugin.dll",
  "requires": "1.0.0.0"
}
```

`id` 必须稳定且唯一，`dllpath` 必须指向实际生成的程序集，`requires` 要替换为插件真实支持的最低宿主版本。发布版本由编译后 DLL 的 `FileVersion` 派生，`manifest.json` 的 `version` 应保持同步，但不是普通插件打包的权威版本源。没有 manifest 时按“目录名 + 同名 DLL”加载只是兼容路径，不是新插件方案。

## 3. 接入宿主扩展点

按需求实现现有 provider 接口，例如菜单、配置页或状态栏 provider。参考 `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs` 和 `SystemMonitorIStatusBarProvider.cs`；不要自行建立另一套插件生命周期。

## 4. 构建与 HostCopy

直接构建项目会稳定产生项目输出：

```powershell
dotnet build .\Plugins\MyPlugin\MyPlugin.csproj -c Debug -p:Platform=x64
```

`PluginProject.HostCopy.targets` 的 `PostBuild` 只有在 solution/MSBuild 构建提供非空、非 `*Undefined*` 的 `SolutionDir` 时才执行，并把主 DLL 与项目目录中的 manifest、README、CHANGELOG 同步到宿主 Debug/Release 插件目录。直接 `dotnet build <plugin.csproj>` 不保证触发 HostCopy；这时从项目 `bin\x64\<Configuration>\net10.0-windows\` 验证输出，或显式复制到宿主 `Plugins/<PluginId>/`。

## 5. 验证

1. 确认输出中存在插件 DLL 和 `manifest.json`。
2. 确认 manifest 的 `id`、`dllpath` 与目录和程序集一致。
3. 启动主程序，验证菜单、配置页或状态栏入口。
4. 只校验 manifest、不构建/打包/上传时运行：

```powershell
python .\Scripts\package_cvxp.py --project-file .\Plugins\MyPlugin\MyPlugin.csproj --validate-only
```

## 6. 分清三类发布入口

| 发布对象 | 唯一正常入口 |
| --- | --- |
| 普通 ColorVision 插件 `.cvxp` | `Scripts\package_plugin.bat MyPlugin`；会构建、上传，并在上传尝试后删除本地包 |
| Spectrum 独立 ZIP + `.cvxp` | `Scripts\Spectrum.bat --release-notes "本次说明"`；不能用普通插件脚本代替正式发布 |
| ColorVision 主程序 | `Scripts\release.bat`；它不是插件发布入口 |

普通插件发布以打包脚本确认包与 `LATEST_RELEASE` 上传成功为准；Spectrum 和主程序脚本还会执行远端元数据、签名/大小及下载验收。三类发布都不能只凭本地 build 成功判断。

## 常见问题

- 未发现插件：检查宿主输出下的目录、manifest、`id` 和 `dllpath`。
- 项目构建成功但宿主目录没变化：检查这次构建是否真的提供有效 `SolutionDir`。
- 功能未出现：检查 provider 类型是否公开、可实例化，并已使用宿主现有扩展接口。
- 依赖冲突：检查 `.deps.json` 和宿主 `ColorVision.*` 版本，不要随包覆盖宿主程序集。

继续阅读：[插件生命周期](./lifecycle.md)、[插件开发概览](./overview.md)、[构建与发布脚本](../scripts/README.md)。
