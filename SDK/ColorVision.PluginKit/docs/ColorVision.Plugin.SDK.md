# ColorVision 外部插件 SDK

本文档给第三方插件作者使用。目标是让插件项目独立维护，只通过 NuGet 引用平台 SDK，最终交付 `.cvxp` 插件包。

## 1. 插件工程要求

- Windows WPF 插件目标框架使用 `net10.0-windows`。
- 默认平台目标使用 `x64`。
- 插件项目引用 ColorVision SDK 包，例如 `ColorVision.UI`。
- 插件输出目录必须包含 `manifest.json` 和插件主 DLL。
- 插件可以保留独立 `App.xaml`，这样既能单独运行，也能被 ColorVision 作为插件加载。

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

实现 `IMenuItemProvider` 后，ColorVision 会在启动时加载插件 DLL，并由菜单系统自动发现菜单项。

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

开发调试时，可将构建输出复制到 ColorVision 运行目录：

```powershell
$pluginId = "DemoPlugin"
$source = "C:\Path\To\DemoPlugin\bin\x64\Debug\net10.0-windows"
$target = "C:\Path\To\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$source\*" $target -Recurse -Force
```

安装完成后，插件会位于：

```text
C:\Path\To\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\DemoPlugin
```

启动 ColorVision 后，插件菜单会出现在 `工具` 菜单下。

## 5. 用单个 cvplugin.exe 完成首次配置和后续发布

最终对外建议只发一个 `cvplugin.exe`。

使用方式：

1. 把 `cvplugin.exe` 放到插件项目根目录。
2. 第一次双击时，如果当前目录没有 `pluginkit.config.json`，它会进入交互式配置：
  - 是否配置构建步骤，默认是。
  - 默认使用当前目录下唯一的 `.csproj`；也可以输入别的 `.csproj`、一个只包含单个 `.csproj` 的目录，或者输入 `cmd:<命令>` 保存成自定义构建命令。
  - 打包源目录，默认是 `bin\x64\Release\net10.0-windows`。
  - 包输出目录，默认是当前目录下的 `packages`。
  - 是否在打包完成后上传，默认上传。
  - 如果选择上传，是否在上传成功后保留本地 `.cvxp`，默认保留。
  - 如果选择“不保留本地 `.cvxp`”，而输出目录原本不存在或原本为空，上传成功后这个空目录也会被自动删除。
3. 确认后会在当前目录生成 `pluginkit.config.json`。
4. 后续再次双击 `cvplugin.exe`，它会自动读取当前目录的 `pluginkit.config.json`，并按配置执行构建、打包和上传。

如果你在仓库里直接运行 `scripts/package_cvxp.py`，无参数时的行为和未来的 `cvplugin.exe` 是一致的。

## 6. 仓库内调试入口

仓库内如果还没把脚本打成 `cvplugin.exe`，可以直接运行 `scripts/package_cvxp.py`。无参数行为和未来的 `cvplugin.exe` 一致：没有 config 时先进入交互式配置，有 config 时直接执行。

需要自定义行为时，也可以直接调用：

```powershell
python .\scripts\package_cvxp.py --config .\pluginkit.config.json --build
```

包名格式为 `{PluginId}-{version}.cvxp`。压缩包内部顶层目录必须是插件 ID，例如 `DemoPlugin/manifest.json`、`DemoPlugin/DemoPlugin.dll`。

## 7. 上传插件市场

需要直接调用脚本时：

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

python .\scripts\package_cvxp.py --config .\pluginkit.config.json --build --upload-url http://your-marketplace-host:9998
```

如果后面你把 `package_cvxp.py` 打包成 exe，对外也只需要发这个 exe 和 `shared_files.json`。

仓库内重新构建 `cvplugin.exe` 时，使用：

```powershell
python -m PyInstaller --noconfirm --clean .\cvplugin.spec
```

## 8. 版本兼容建议

- 插件版本使用四段版本号，例如 `0.1.0.0`。
- `manifest.json` 的 `requires` 写最低支持的 ColorVision 版本。
- 插件更新时保持 `id` 不变。
- 大模型、大样例数据建议放到插件目录下的 `Models/` 或单独下载，不要混入平台主仓。
- 如果插件依赖 ColorVision 已经自带的 DLL，打包脚本会根据 `shared_files.json` 去重，避免 `.cvxp` 过大。

## 9. 最小交付清单

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
