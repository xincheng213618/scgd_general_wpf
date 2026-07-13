# 项目说明

`Projects/` 目录放客户项目包、业务方案包和对接示例。这里先回答“这个项目是什么、入口在哪里、怎么构建和交付”，更细的源码说明放在 [项目包总览](../04-api-reference/projects/README.md) 和各项目页。

## 当前项目

| 项目 | 业务定位 | 文档 |
| --- | --- | --- |
| ProjectARVRLite | AR/VR 轻量测试，可配置测试类型、预处理、Socket 切图和 CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | AR/VR 专业流程组、Recipe 和 Socket 对接 | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | 面向客户或上位机的 TCP/JSON 对接示例 | [Integration Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectKB | 键盘背光测试，Modbus 自动触发、MES DLL、自动修正和 CSV/summary | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX 亮度、色彩、MTF、畸变自动化测试 | [ProjectLUX](../04-api-reference/projects/project-lux.md) |

已停用项目的源码快照和恢复方式记录在仓库的 `Projects/ARCHIVED.md`。

## 项目页应该说明什么

- 客户场景和测试对象。
- 主程序中的入口窗口或菜单。
- 外部触发方式，例如 Socket、MES、串口、Modbus 或本地按钮。
- 流程组、Recipe/Fix、模板绑定和结果判定。
- 导出结果，例如 CSV、XLSX、PDF、SQLite 或 Socket 返回字段。
- manifest、README、CHANGELOG、配置、资源和依赖 DLL。

## 构建和打包

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectLUX
```

## 维护原则

- 新增 `Projects/<Name>/` 时，补项目 README、CHANGELOG、manifest 和 docs 项目页。
- 如果改协议、流程组、Recipe/Fix、导出字段或 manifest，同步更新对应项目页。
- 客户口头流程不要写成当前系统承诺；文档必须能回到源码、配置或 manifest。
