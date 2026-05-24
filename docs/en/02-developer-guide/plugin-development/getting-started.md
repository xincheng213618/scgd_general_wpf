# Plugin Development Getting Started

This page provides the shortest plugin development path executable in the current repository, no longer using the old generic host, async lifecycle, and `plugin.json` examples.

## Prerequisites

- Windows development environment
- .NET 8.0 SDK
- WPF development toolchain
- Current repository source code and runnable main application output

## Minimal Development Path

### 1. Create Plugin Project

It is recommended to create the plugin project directly under `Plugins/<PluginId>/`, with the target framework kept as `net8.0-windows`. If the plugin has a UI, enable WPF.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />

If you need to explicitly specify the entry type, you can continue adding `entry_point`.

## 4. Copy Output to Main Application Plugin Directory

When the main application runs, it scans `Plugins/` from its own output directory, so during debugging, you need to copy the plugin output there.

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</Target>
```

If your local output directory differs, adjust to the actual main application output path.

## 5. Run and Debug

1. Build the main application.
2. Build the plugin project, confirm DLL and `manifest.json` have been copied to the plugin directory.
3. Launch `ColorVision/ColorVision.csproj`.
4. Verify whether the plugin is loaded in the corresponding menu, tools page, or plugin management interface.

## Recommended Reference Implementations

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/README.md`

These examples already cover two common patterns: basic plugin entry and menu extension.

## Common Issues

### Plugin Not Discovered

- Check whether `manifest.json` exists
- Check whether the DLL pointed to by `dllpath` actually exists
- Check whether the plugin directory has been copied to `Plugins/<PluginId>/` under the main application output directory

### Plugin Discovered but Functionality Not Appearing

- Check whether only the base plugin class is implemented, without implementing the required provider interface
- Check whether the entry type has a public parameterless constructor
- Check whether the type is non-abstract, non-generic open type

### Dependency Conflicts

- Do not repackage platform-built-in `ColorVision.*.dll`
- If the plugin includes `.deps.json`, confirm dependency versions do not exceed the target platform

## Next Steps

- To understand how the platform scans and loads plugins: see [Plugin Lifecycle](./lifecycle.md)
- To understand the overall structure first: see [Plugin Development Overview](./overview.md)