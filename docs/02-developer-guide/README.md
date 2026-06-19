# 开发手册

本章节回答“怎么改、怎么扩展、怎么构建、怎么交付”。如果目标是新增或维护插件，直接进入 [插件开发手册](./plugin-development/README.md)；如果目标是接手客户项目，先进入 [项目说明](../00-projects/README.md)。

## 按任务进入

| 任务 | 先看 | 然后看 |
| --- | --- | --- |
| 发布 UI DLL/NuGet | [UI DLL 发布场景手册](../04-api-reference/ui-components/ui-dll-release-playbook.md) | [UI DLL 发布手册](../04-api-reference/ui-components/publishing.md)、[UI 组件与 DLL 发布](../04-api-reference/ui-components/README.md) |
| 维护 UI 菜单/设置/运行时组件 | [UI 运行时组件交接手册](../04-api-reference/ui-components/ui-runtime-handoff.md) | [UI 组件目录](../04-api-reference/ui-components/control-catalog.md)、对应 UI DLL 页 |
| 接手 Engine 业务逻辑 | [Engine 业务场景交接手册](../04-api-reference/engine-components/business-scenario-playbook.md) | [Engine 业务交接手册](../04-api-reference/engine-components/business-handoff.md)、[Engine 开发指南](./engine-development/README.md) |
| 新增或维护插件 | [插件运行与交接场景手册](../04-api-reference/plugins/plugin-handoff-playbook.md) | [现有插件现场验收与交接清单](../04-api-reference/plugins/plugin-field-acceptance.md)、[插件开发手册](./plugin-development/README.md)、[现有插件能力说明](../04-api-reference/plugins/README.md) |
| 维护客户项目包 | [项目包运行与交接场景手册](../04-api-reference/projects/project-package-playbook.md) | [项目说明](../00-projects/README.md)、对应项目页和项目 README |
| 选择测试和验收命令 | [测试与验证交接手册](./testing.md) | `Test/ColorVision.UI.Tests/`、`Test/opencv_helper_test/`、后端和脚本测试 |
| 修改 Flow 节点 | [FlowEngineLib 节点扩展](../04-api-reference/extensions/flow-node.md) | [FlowEngineLib 架构](../03-architecture/components/engine/flow-engine.md) |
| 构建安装包或更新包 | [部署概览](./deployment/overview.md) | [构建与发布脚本](./scripts/README.md) |
| 维护插件市场后端 | [插件市场后端](./backend/README.md) | `Backend/marketplace/README` 和测试脚本 |

## 开发前必须确认

- 当前目标框架是 Windows WPF，主线为 `net10.0-windows` 或 `net10.0-windows7.0`，部分 UI 包仍同时支持 net8。
- 根目录有 `ColorVision.snk` 时构建会启用强名称签名。
- 主程序、插件和项目包主要以 x64 交付。
- 插件和项目包运行时进入 `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<Name>/`。
- `Scripts/package_plugin.bat` 和 `Scripts/package_project.bat` 生成 `.cvxp` 包。
- 文档、运行时 README、manifest 和 `VersionPrefix` 需要一起维护。

## 常见开发流程

### 改 UI 类库

1. 找到对应 UI 模块页。
2. 如果改的是菜单、设置、PropertyGrid、ImageEditor 工具、Socket/调度窗口或插件市场，先读 [UI 运行时组件交接手册](../04-api-reference/ui-components/ui-runtime-handoff.md)，确认运行时发现方式。
3. 确认包发布规则和依赖方向。
4. 修改代码和 README。
5. Release x64 构建。
6. 检查 `.nupkg`、`.snupkg`、native runtime 和主程序输出。

### 改 Engine 或模板

1. 先读 [Engine 业务场景交接手册](../04-api-reference/engine-components/business-scenario-playbook.md)，按具体需求确认代码落点和验收步骤。
2. 再读 [Engine 业务交接手册](../04-api-reference/engine-components/business-handoff.md)，理解完整执行链。
3. 确认修改点属于设备、模板、Flow、MQTT、结果展示还是项目判定。
4. 在最小边界内改代码。
5. 用现有流程模板或项目包验证端到端结果。
6. 更新对应 Engine/算法/项目文档。

### 改插件

1. 先读 [插件运行与交接场景手册](../04-api-reference/plugins/plugin-handoff-playbook.md)，确认这次是加载、入口、打包、依赖、权限还是 Socket 变更。
2. 再读 [插件开发手册](./plugin-development/README.md)。
3. 确认 `manifest.json`、入口类、依赖 DLL 和 PostBuild 复制规则。
4. 修改插件 README 和 docs 站点对应能力页。
5. 用 `Scripts\package_plugin.bat <Name> --no-upload` 打包。
6. 按 [现有插件现场验收与交接清单](../04-api-reference/plugins/plugin-field-acceptance.md) 启动主程序确认菜单、窗口、README/CHANGELOG、依赖版本、业务烟测和回退材料。

### 改项目包

1. 先读 [项目包运行与交接场景手册](../04-api-reference/projects/project-package-playbook.md)，确认问题属于入口、外部触发、流程模板、判定配置、结果输出、设备/MES 还是打包交付。
2. 再读 [项目说明](../00-projects/README.md) 和对应项目页。
3. 确认项目的流程组、Recipe/Fix、Socket/MES 协议和结果导出。
4. 修改项目 README 和 docs 站点对应项目页。
5. 用 `Scripts\package_project.bat <Name> --no-upload` 打包。
6. 启动主程序确认项目入口、配置、流程执行和结果输出。

### 选择测试

1. 先读 [测试与验证交接手册](./testing.md)，确认这次变更应该覆盖的测试层级。
2. UI、主程序运行时、配置、日志、MCP/Copilot、PropertyGrid 和列表编辑优先从 `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` 开始。
3. Native/OpenCV helper、亮区查找和 C++ 依赖验证进入 `Test/opencv_helper_test/`。
4. 插件市场后端、构建脚本和文档站分别执行对应 Python 测试、脚本测试和 `npm run docs:build`。

## 继续阅读

- [扩展性概览](./core-concepts/extensibility.md)
- [插件开发手册](./plugin-development/README.md)
- [Engine 开发指南](./engine-development/README.md)
- [测试与验证交接手册](./testing.md)
- [部署概览](./deployment/overview.md)
- [构建与发布脚本](./scripts/README.md)
