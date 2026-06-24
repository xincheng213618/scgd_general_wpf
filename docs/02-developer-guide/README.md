# 开发手册

本章节回答“怎么改代码、怎么构建、怎么测试、怎么交付”。根目录 [README](../../README.md) 是仓库第一入口；这里保留开发专题和更细的模块入口。

## 按任务进入

| 任务 | 入口 |
| --- | --- |
| 选择构建、测试和验收命令 | [测试与验证](./testing.md) |
| 构建安装包、更新包或发布包 | [部署概览](./deployment/overview.md)、[构建与发布脚本](./scripts/README.md) |
| 新增或维护插件 | [插件开发](./plugin-development/README.md)、[现有插件能力](../04-api-reference/plugins/README.md) |
| 维护客户项目包 | [项目说明](../00-projects/README.md)、[项目包总览](../04-api-reference/projects/README.md) |
| 修改 Engine、设备、模板或 Flow | [Engine 开发](./engine-development/README.md)、[Engine 组件](../04-api-reference/engine-components/README.md) |
| 修改 UI 类库、菜单、设置或图像编辑器 | [UI 组件](../04-api-reference/ui-components/README.md) |
| 新增 Flow 节点或扩展点 | [扩展点](../04-api-reference/extensions/README.md)、[Flow 节点扩展](../04-api-reference/extensions/flow-node.md) |
| 维护插件市场后端 | [插件市场后端](./backend/README.md) |

## 开发前确认

- 当前主线是 Windows WPF，目标框架以 `net10.0-windows` 为主。
- 常规桌面交付以 x64 为主。
- 根目录存在 `ColorVision.snk` 时构建会启用强名称签名。
- 插件和项目包运行时进入主程序输出目录的 `Plugins/<Name>/`。
- 修改公开行为时，同步更新对应 README 或 `docs/` 页面。
- 修改打包/发布逻辑时，优先更新脚本文档和根目录贡献说明。

## 常用命令

```powershell
dotnet restore
dotnet build build.sln -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64
npm run docs:build
```

插件和项目包打包：

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
Scripts\package_project.bat ProjectARVR --no-upload
```

正式发布：

```powershell
Scripts\release.bat
```

## 目录说明

| 目录 | 内容 |
| --- | --- |
| `core-concepts/` | 扩展性、MCP/Copilot 等核心概念 |
| `engine-development/` | Engine、服务、模板、MQTT、OpenCV 接入 |
| `plugin-development/` | 插件接口、manifest、生命周期、打包 |
| `deployment/` | 安装器、自动更新和交付路径 |
| `scripts/` | 构建、打包、上传、发布脚本 |
| `backend/` | 插件市场后端 |

## 维护原则

- 开发文档写“怎么做”和“在哪里改”，不堆历史会议材料。
- 细节能回到源码、项目文件、manifest、脚本或测试命令。
- 证据表、覆盖清单、临时路线图不作为长期文档保留；需要时从 Git 历史或发布记录找回。
