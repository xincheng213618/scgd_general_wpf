# ColorVision 外部插件 SDK

本文档是给第三方插件作者的最小接入说明。目标是让插件项目可以独立维护，不需要放进 ColorVision 主仓库，只通过 NuGet 引用平台 SDK，并最终交付 `.cvxp` 插件包。

## 1. 插件工程要求

- Windows WPF 插件目标框架使用 `net10.0-windows`。
- 默认平台目标使用 `x64`。
- 插件项目引用 ColorVision SDK 包，例如 `ColorVision.UI`。
- 插件输出目录必须包含 `manifest.json` 和插件主 DLL。
- 插件可以继续保留独立 `App.xaml`，这样既能单独运行，也能被 ColorVision 作为插件加载。

推荐 `.csproj` 基础模板：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>x64;ARM64</Platforms>
    <PlatformTarget>$(Platform)</PlatformTarget>
    <GenerateDependencyFile>true</GenerateDependencyFile>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <VersionPrefix>0.1.0.0</VersionPrefix>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ColorVision.UI" Version="1.5.5.1" />
  </ItemGroup>

  <ItemGroup>
    <None Update="manifest.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="README.md" CopyToOutputDirectory="PreserveNewest" />
    <None Update="CHANGELOG.md" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

## 2. manifest.json

插件根目录必须包含 `manifest.json`。`id` 要稳定，后续更新、安装、卸载都以它为唯一标识。

```json
{
  "manifest_version": 1,
  "id": "DemoPlugin",
  "name": "演示插件",
  "version": "0.1.0.0",
  "description": "一个独立维护的 ColorVision 插件。",
  "dllpath": "DemoPlugin.dll",
  "requires": "1.4.6.25",
  "author": "Your Name",
  "entry_point": "DemoPlugin.DemoMenuProvider",
  "icon": "PackageIcon.png"
}
```

## 3. 菜单入口

实现 `IMenuItemProvider` 后，ColorVision 会在启动加载插件 DLL，并由菜单系统自动发现菜单项。

```csharp
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace DemoPlugin;

public sealed class DemoMenuProvider : IMenuItemProvider
{
    public IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        return
        [
            new MenuItemMetadata
            {
                OwnerGuid = MenuItemConstants.Tool,
                GuidId = "DemoPlugin",
                Header = "演示插件",
                Order = 600,
                Command = new RelayCommand(_ =>
                {
                    var window = new MainWindow
                    {
                        Owner = Application.Current?.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    window.Show();
                })
            }
        ];
    }
}
```

## 4. 构建和本地安装

外部插件可以在自己的仓库里构建：

```powershell
dotnet build .\DemoPlugin.csproj -c Debug -p:Platform=x64
```

开发调试时，把构建输出复制到 ColorVision 运行目录：

```powershell
$pluginId = "DemoPlugin"
$source = ".\bin\x64\Debug\net10.0-windows"
$target = "C:\Path\To\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$source\*" $target -Recurse -Force
```

启动 ColorVision 后，插件菜单会出现在 `工具` 菜单下。

## 5. 打包为 .cvxp

ColorVision 主仓提供 `Scripts/package_cvxp.py`，可以对外部插件项目打包：

```powershell
python C:\Path\To\ColorVision\Scripts\package_cvxp.py `
  --project-file C:\Path\To\DemoPlugin\DemoPlugin.csproj `
  --build `
  --configuration Release `
  --framework net10.0-windows `
  --output-dir C:\Path\To\Packages `

```

包名格式为 `{PluginId}-{version}.cvxp`。压缩包内部顶层目录必须是插件 ID，例如 `DemoPlugin/manifest.json`、`DemoPlugin/DemoPlugin.dll`。

## 6. 上传插件市场

推荐使用结构化发布接口：

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

python C:\Path\To\ColorVision\Scripts\publish_plugin.py `
  -p DemoPlugin `
  -v 0.1.0.0 `
  -f C:\Path\To\Packages\DemoPlugin-0.1.0.0.cvxp `
  -n "演示插件" `
  -d "一个独立维护的 ColorVision 插件" `
  -c "Tools" `
  -r "1.4.6.25" `
  --api-url http://your-marketplace-host:9999
```

## 7. 版本兼容建议

- 插件版本使用四段版本号，例如 `0.1.0.0`。
- `manifest.json` 的 `requires` 写最低支持的 ColorVision 版本。
- 插件更新时保持 `id` 不变。
- 大模型、大样例数据建议放到插件目录下的 `Models/` 或单独下载，不要混入平台主仓。
- 如果插件依赖 ColorVision 已经自带的 DLL，打包脚本会根据 `shared_files.json` 去重，避免 `.cvxp` 过大。

## 8. 最小交付清单

外部插件仓库至少包含：

```text
DemoPlugin.csproj
MainWindow.xaml
MainWindow.xaml.cs
DemoMenuProvider.cs
manifest.json
README.md
CHANGELOG.md
```

发布给用户时只需要 `.cvxp`，用户不需要插件源码。
