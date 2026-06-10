# Engine 变更影响与验收清单

这页用于每次修改 Engine 业务逻辑后的交接验收。它不替代 [Engine 业务场景交接手册](./business-scenario-playbook.md)，而是把“改了什么、影响哪里、要交什么证据”固定成检查表，避免只验证当前按钮能点，却漏掉模板、Flow、结果展示、项目包导出或现场包。

## 使用时机

| 场景 | 使用方式 |
| --- | --- |
| 接到现场缺陷 | 先定位变更类型，再按本页确认影响面和最小证据 |
| 新增设备、模板、节点或结果 handler | 在代码完成前先列验收项，避免只补单点实现 |
| 修改客户项目字段 | 同时核对 Engine 原始结果和项目包导出字段 |
| 准备发布或现场替换 | 用“证据包”章节留存构建、配置、结果和回退证据 |
| 交接给新同事 | 按“先问问题”章节说明改动为什么属于 Engine、项目包或插件 |

## 先问四个问题

| 问题 | 如果答案是 | 归属 |
| --- | --- | --- |
| 这个改动是否改变设备资源、连接、状态或命令 | 是 | `Engine/ColorVision.Engine/Services/` |
| 这个改动是否改变模板参数、Flow 节点或 `.cvflow` 导入导出 | 是 | `Templates/`、`FlowEngineLib/`、`Templates/Flow/NodeConfigurator/` |
| 这个改动是否改变通用结果读取、overlay 或历史结果展示 | 是 | Engine result handler 和 ImageEditor |
| 这个改动是否只改变某客户 CSV/PDF/MES/Socket 字段 | 是 | `Projects/<Project>/Process`、`Recipe`、`Fix`、导出器 |

客户专用规则不要下沉到通用 Engine handler；通用设备、模板、Flow 或结果 handler 也不要只写在项目窗口里。

## 变更影响速查

| 变更类型 | 直接代码 | 必查上游 | 必查下游 | 最小验收 |
| --- | --- | --- | --- | --- |
| 新增设备类型 | `ServiceTypes`、`DeviceServiceFactoryRegistry`、`DeviceXxx` | `SysResourceModel`、设备配置、MQTT/服务端 | 设备页、Flow 节点配置器、项目包调用 | 新建资源、服务生成、设备页打开、最小命令、Flow 绑定 |
| 修改设备命令 | `Services/Devices/*/MQTT*.cs` 或服务方法 | MQTT 连接、设备 Code、服务 token | Flow 状态、结果 ID、项目流程 | 手动命令和 Flow 节点各跑一次 |
| 新增模板参数 | `Template*.cs`、参数模型、编辑控件 | 旧模板数据、默认值、`TemplateDicId` | Flow 节点保存、算法命令、结果 handler | 新建、编辑、保存、复制、导入、旧数据打开 |
| 新增 Flow 节点 | `FlowEngineLib`、`Templates/Flow/Nodes`、`NodeConfigurator` | 节点输入输出、设备/模板列表 | `.cvflow` 保存导入、`FlowCompleted`、项目包读取 | 打开、编辑、保存、重开、导入、执行 |
| 修改结果展示 | `IResultHandleBase`、`IViewResult`、DAO、`ViewHandleXxx` | `ViewResultAlgType`、结果主表、文件路径 | ImageEditor overlay、历史结果页、项目导出 | 历史结果打开、overlay 坐标、表格/侧栏、项目 CSV |
| 修改项目结果字段 | 项目 `Process`、`Recipe`、`Fix`、导出器 | Engine 原始结果、批次/SN、模板名 | CSV/PDF/MES/Socket、客户样例 | 同一 SN 跑完整项目流程并比对客户样例 |
| 修改远程服务链路 | `MQTTControl`、`MqttRCService`、设备 `MQTT*.cs` | broker、topic、服务 token、文件服务器 | 结果查询、Flow 状态、重试/超时 | 断线、超时、服务成功、服务失败四种路径 |
| 修改文件格式或图像读取 | `ColorVision.FileIO`、`cvColorVision`、FileServer、Media | 原始文件、native DLL、缓存目录 | ImageEditor、Shell 缩略图、结果 handler | 打开样例、导出、重新打开、缩略图或 overlay 验证 |

## 证据包模板

每次 Engine 业务变更，交接说明至少留下这些证据：

| 证据 | 内容 | 存放建议 |
| --- | --- | --- |
| 变更说明 | 业务目标、触发入口、影响模块、回退方式 | PR/交接文档 |
| 输入样例 | 设备 Code、模板名、Flow 名、SN、批次号、结果 ID、输入文件 | 测试记录或现场包 |
| 输出样例 | UI 状态、Flow 状态、结果表、overlay、CSV/PDF/MES/Socket 响应 | 发布证据目录 |
| 配置快照 | 设备配置、MQTT、模板 JSON、`.cvflow`、项目 Recipe/Fix | 发布包或配置备份 |
| 构建信息 | `.csproj` 版本、DLL FileVersion、包名、构建命令 | 发布记录 |
| 文档同步 | 本页、链路页、项目页、插件页、使用手册是否更新 | 文档 diff |

没有批次号、SN、模板名或结果 ID 的验收记录，通常不能证明 Engine 链路真的跑通。

## 分层验收清单

### 设备服务

| 验收项 | 通过标准 |
| --- | --- |
| 资源创建 | 数据库资源存在，`type` 能映射到 `ServiceTypes` |
| 服务生成 | `ServiceManager.DeviceServices` 中出现目标服务 |
| 显示页 | 设备显示页可打开，状态可刷新 |
| 命令 | 手动命令返回成功；失败时日志包含设备 Code 和错误原因 |
| Flow 绑定 | 节点属性能选择设备，保存重开不丢 |
| 项目包 | 如果项目调用该设备，项目最小流程能完成一次 |

### 模板和 Flow

| 验收项 | 通过标准 |
| --- | --- |
| 模板扫描 | `TemplateControl` 能加载目标模板 |
| 参数编辑 | PropertyGrid 或专用编辑器能解释字段含义 |
| 保存重开 | 保存模板和 Flow 后重开不丢参数 |
| 导入导出 | `.cvflow` 导出后导入到干净环境，关联模板可恢复 |
| 执行 | `FlowControl.FlowCompleted` 返回状态、SN 和参数 |
| 旧数据 | 旧模板、旧 Flow 和旧项目配置仍可打开 |

### 结果展示

| 验收项 | 通过标准 |
| --- | --- |
| 结果主表 | 能根据批次或结果 ID 查到 `ViewResultAlg` |
| handler 匹配 | `CanHandle` / `CanHandle1` 命中目标 `ViewResultAlgType` |
| 明细读取 | DAO 或模型能填充 `IViewResult` 集合 |
| 图像路径 | ImageEditor 能打开对应图像或结果文件 |
| overlay | 坐标、缩放、ROI/POI/曲线在当前图像尺寸下正确 |
| 项目映射 | 项目包读取相同结果后导出字段不为空 |

### 项目包输出

| 验收项 | 通过标准 |
| --- | --- |
| SN/批次 | 项目流程生成的 SN 和 Engine 批次一致 |
| Process | 项目 `Process` 读取正确模板名、key 和结果类型 |
| Recipe/Fix | 修正和判定规则有版本记录，失败原因可追溯 |
| 导出 | CSV/PDF/XLSX 字段顺序和客户样例一致 |
| 外部响应 | Socket/MES 返回在最终结果完成后发出 |
| 回归 | 至少覆盖 PASS、NG、算法失败、外部超时 |

## 不同角色的交接重点

| 接手角色 | 重点看 | 不应承担 |
| --- | --- | --- |
| Engine 开发 | 设备、模板、Flow、结果 handler、DAO | 客户协议字段顺序 |
| 项目包开发 | Process、Recipe、Fix、导出、Socket/MES | 通用设备工厂和通用 overlay 规则 |
| UI 开发 | 设备页、模板编辑器、ImageEditor overlay、状态栏 | 算法服务端业务判定 |
| 插件开发 | manifest、菜单入口、插件依赖、打包 | Engine 主业务初始化 |
| 现场交付 | 配置、包、样例、回退、验收记录 | 私自修改模板/数据库结构 |

## 文档同步规则

| 改动 | 必须同步 |
| --- | --- |
| 新增设备或服务类型 | [设备服务链路](./device-service-chain.md)、[业务链路矩阵](./business-flow-matrix.md)、使用手册设备页 |
| 新增模板或 Flow 节点 | [模板与 Flow 链路](./template-flow-chain.md)、[算法与模板](../algorithms/README.md)、扩展点文档 |
| 新增结果 handler | [结果展示与项目交接链路](./result-handoff-chain.md)、UI ImageEditor 文档 |
| 修改项目字段 | [项目说明](../../00-projects/README.md)、对应项目页、项目能力矩阵 |
| 修改插件或发布脚本 | [插件开发手册](../../02-developer-guide/plugin-development/README.md)、[现有插件能力说明](../plugins/README.md) |
| 修改 UI DLL 发布 | [UI DLL 发布手册](../ui-components/publishing.md)、[UI 组件发布矩阵](../ui-components/release-matrix.md) |

## 最后一公里检查

提交或交接前按顺序确认：

1. 变更类型已经归属到 Engine、项目包、插件或 UI。
2. 最小流程跑过，不只是单个类编译通过。
3. 有 SN、批次、模板名、结果 ID 或文件路径作为证据。
4. UI 展示和项目导出都核对过。
5. 发布包包含必要配置、native DLL、模板、`.cvflow` 或 README/CHANGELOG。
6. 本页和对应链路页已经更新。
7. 文档站运行 `npm run docs:build` 通过。

## 继续阅读

- [Engine 业务链路矩阵](./business-flow-matrix.md)
- [Engine 业务场景交接手册](./business-scenario-playbook.md)
- [Engine 业务交接手册](./business-handoff.md)
- [Engine 设备服务链路](./device-service-chain.md)
- [Engine 模板与 Flow 链路](./template-flow-chain.md)
- [Engine 结果展示与项目交接链路](./result-handoff-chain.md)
- [项目包能力与交接矩阵](../projects/project-capability-matrix.md)
