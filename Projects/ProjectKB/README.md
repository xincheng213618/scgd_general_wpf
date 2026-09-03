# ProjectKB — 键盘背光检测项目

ProjectKB 将 FlowEngine 的 KB/POI 结果、Recipe 判定、背光修正、Modbus 触发与 MES/CSV 留痕连接起来。项目身份、版本及依赖以随版本维护的 `ProjectKB.csproj`、`manifest.json` 和实际交付产物为准。

## 运行与交付边界

- 可由 ColorVision 宿主加载 `ProjectKB.dll`，也有 `App.xaml.cs` 的独立启动入口；后者仍显式加载 `ColorVision.Engine.dll` 并执行初始化器，不是免依赖的独立交付。
- 运行需相应 Engine/UI 程序集、配置和运行环境；启用 MES 还需匹配的 `FunTestDll.dll`、`FunTestDllConfig.INI`。依赖不能由 README 中的功能名称推断。
- 启动与测试可能连接 Modbus/MES、触发流程、写入结果或上传测试数据，须确认现场配置与当前任务授权；源码问答或文档验证不需要启动本项目。

## 源码知识入口

[ProjectKB 权威主题](../../docs/04-api-reference/projects/project-kb.md)集中维护运行链、MES 返回码、Recipe 判定、CSV 字段边界、生产统计与运行内查询记忆、最小本地验证与发布约束。

该链接相对于源码仓库。此 README 会嵌入并复制到输出；若正在交付包中阅读，包内不保证包含 `docs/`，应在与包版本匹配的源码仓库中读取 `docs/04-api-reference/projects/project-kb.md`，不能用另一版本的文档代替当前交付契约。
