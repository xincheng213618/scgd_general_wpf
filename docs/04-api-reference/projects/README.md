---
knowledge_id: "projects.index"
knowledge_type: "index"
status: "current"
summary: "按客户业务代码、独立对接示例、旧项目归档与构建发布边界定位 Projects 的权威主题。"
aliases: ["应该改哪个客户项目","客户项目包源码在哪里","客户项目如何构建","项目包和插件有什么区别","ProjectARVRPro","ProjectARVRPro.IntegrationDemo","ProjectKB","ProjectLUX","ProcessGroup","ProcessMeta","归档项目","已停用项目","ProjectARVRLite","ProjectARVR","ProjectBlackMura","ProjectHeyuan","ProjectShiyuan"]
code_paths: ["Projects/","PluginProject.HostCopy.targets","Scripts/package_project.bat","Scripts/package_cvxp.py"]
test_paths: ["Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj","Test/ProjectKB.Tests/ProjectKB.Tests.csproj","Test/ProjectLUX.Tests/ProjectLUX.Tests.csproj"]
related: ["projects.capabilities","projects.arvr-pro","projects.arvr-pro-demo","projects.kb","projects.lux","plugins.model","plugins.getting-started"]
---

# 客户项目与对接示例入口

`Projects/` 保存客户专用流程、判定、配置和结果出口。本页将问题路由到拥有这些代码的项目主题；具体协议字段、完成判据与验证缺口只在对应主题维护，不因“业务使用”和“源码开发”再保留两份正文。

## 按代码责任定位

| 责任与问题 | 项目主题 | 首查入口 |
| --- | --- | --- |
| AR/VR 流程组、Recipe、历史图像回退 | [ProjectARVRPro](./project-arvr-pro.md) | `Projects/ProjectARVRPro/ARVRWindow.xaml.cs`、`ResultImagePresentation.cs`；宿主入口为 `PluginConfig/ProjectARVRWindowHost.cs` |
| ARVRPro TCP/JSON 客户端、RunAll ACK 与最终结果 | [Integration Demo](./project-arvr-pro-integration-demo.md) | `Projects/ProjectARVRPro.IntegrationDemo/Program.cs`、`MainWindow.xaml.cs`、`Contracts/` |
| 键盘背光、Modbus 触发、MES 返回码与键位结果 | [ProjectKB](./project-kb.md) | `Projects/ProjectKB/ProjectKBWindow.xaml.cs`；宿主入口为 `PluginConfig/KBProjectPlugin.cs` |
| LUX 文本 Socket、Recipe/Fix 与结果导出 | [ProjectLUX](./project-lux.md) | `Projects/ProjectLUX/LUXWindow.xaml.cs`、`Services/SocketControl.cs`；宿主入口为 `PluginConfig/ProjectLUXPlugin.cs` |

尚未确定业务归属，或要比较协议、触发与结果出口时，查独立的[项目横向速查](./project-capability-matrix.md)（`projects.capabilities`），再进入具体项目主题。`ProjectARVR`、`ProjectARVRLite`、`ProjectBlackMura`、`ProjectHeyuan` 和 `ProjectShiyuan` 属于已停用项目，当前分支不包含这些项目目录。归档标签、原版本和源码取回步骤统一见仓库 `Projects/ARCHIVED.md`；恢复源码不包含外部依赖或运行环境，也不代表旧包兼容当前宿主。

## 项目包与独立示例的边界

`ProjectARVRPro`、`ProjectKB`、`ProjectLUX` 各有自己的 `.csproj`、`manifest.json` 和 `PluginConfig/` 宿主入口，manifest 的 `dllpath` 指向各自主 DLL。它们通过插件装载机制进入宿主，但业务所有权仍在对应项目，不是另一套“项目装载器”。运行装载及兼容门禁见[插件装载契约](../../02-developer-guide/plugin-development/overview.md)。

`ProjectARVRPro.IntegrationDemo` 则是独立的 `net48` WPF/命令行对接程序，项目文件没有 ColorVision 项目引用，不按 `.cvxp` 插件交付。其公开 `Contracts/`、样例、参数和结果解析属于客户端契约，不能因类型命名相似而直接引入 Engine、数据库或客户项目内部实现。

客户专用流程组织、Recipe/Fix、外部触发、结果判定与导出字段留在拥有该业务的项目中；仅当多个项目确实复用且形成稳定边界时，再考虑下沉 Engine/UI。并非每个项目都有相同的 `ProcessGroup`、`ProcessMeta` 或目录结构，不能把一个项目的流程模型套给另一个。

## 本地构建与发布不是同一动作

各项目页保留自己的构建、测试和交付命令，本入口不复制命令。三个项目包的 `VersionPrefix` 独立于宿主版本；打包器按已构建主 DLL 的文件版本同步 manifest，不能以主程序版本或 manifest 文本单独证明当前交付内容。

- 本地构建不上传，但会写入编译产物；KB/LUX 的 `PluginProject.HostCopy.targets` 在 `SolutionDir` 有效时复制宿主输出，ARVRPro 自有 PostBuild 还可从项目路径回推仓库目录。不要把构建理解为不会触碰宿主 `bin/.../Plugins/`。
- 常规 `Scripts/package_project.bat <ProjectName>` 调用强制加 `--build`，随后打包并上传包与版本文件；`--no-upload` 被拒绝。进入上传阶段后，`package_cvxp.py` 在 `finally` 清理本地 `.cvxp`，上传失败也可能清理。只有明确发布授权才走此入口，问答、排障和本地编译不构成授权。
- Demo 的客户产物使用其专题中的 `dotnet publish` 路径，不调用项目包上传 wrapper。真实 Socket、Modbus、MES 或设备联调要另行确认操作范围，不能为了验证文档执行。

包结构、依赖裁剪与安装/恢复边界见[插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md)；发布是否完成仍需核对对应项目要求的远端元数据和可下载产物，不能只凭本地构建成功。

## 维护与验证责任

- 项目主题维护客户对象、实际入口、外部触发、流程/模板绑定、判定与结果出口，并提供具体源码和测试定位；客户口头要求或未落地方案不得写成当前实现承诺。
- 新增可装载项目包时维护 `.csproj`、README、CHANGELOG、manifest、配置/资源和 docs 主题；独立示例按自身产物与公开契约维护，不强行为它添加插件 manifest。
- 修改协议、流程组、Recipe/Fix、导出字段、依赖或打包行为时，同步对应项目主题和项目自有说明；跨项目变化再更新 `projects.capabilities`，不要在本入口复制实现细节。

本页的三个测试项目路径只是验证入口，不代表所有业务、协议或现场设备链路均被覆盖。具体自动化范围和人工缺口以各项目主题及测试实现为准；目录存在、知识校验通过或发布成功都不能替代业务验收。
