---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 运行与现场排查

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

安装使用、设备配置、现场故障、日志和数据管理。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [校准服务、本地文件校正与结果持久化](../../01-user-guide/devices/calibration.md) — `operations.calibration`
  校准服务绑定物理相机并执行本地文件或MQTT校正；输出文件、结果显示、历史落库与缓存删除是不同完成边界。

- [相机服务、采集与结果视图](../../01-user-guide/devices/camera.md) — `operations.camera`
  远程取图、本地手动/流程采集与结果视图；明确SaveFiles=false文件显示限制、RAW/CIE帧租约与校正读写、命令完成和设备释放边界。

- [相机参数来源、同步与保存](../../01-user-guide/devices/camera-configuration.md) — `operations.camera-configuration`
  相机参数的编辑入口、同步覆盖与保存；物理配置同步保留本地CameraID，路径移动失败或被拒绝不等于取消路径变更。

- [设备资源配置、保存与重启](../../01-user-guide/devices/configuration.md) — `operations.device-configuration`
  终端与设备配置引用、创建、保存、重启和删除清理；未保存的活对象改动可影响运行，删除不保证显示项和通信对象一并释放。

- [FileServer 设备配置与实现边界](../../01-user-guide/devices/file-server.md) — `operations.file-server`
  FileServer 工厂存在但默认类型树过滤；当前仅有配置与通用 MQTT 包装，未实现远端文件列表、上传或下载操作。

- [FlowDevice 远端服务包装与本地图边界](../../01-user-guide/devices/flow-device.md) — `operations.flow-device`
  Flow 远端设备包装有工厂但默认类型树过滤；它不执行 FlowEngineLib 本地图，也未提供专用运行/停止和完成回执。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口菜单、搜索、状态栏与工作区装配；紧凑主窗口在 Windows build 22000 或更高版本默认启用并保留旧窗口开关，低版本直接使用普通主窗口。

- [电机命令与位置读回](../../01-user-guide/devices/motor.md) — `operations.motor`
  电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。

- [物理相机发现、许可证与资源管理](../../01-user-guide/devices/camera-management.md) — `operations.physical-camera`
  物理相机的扫描、创建、许可证、校正资源和还原点入口；区分扫描结果与缓存列表，创建/导入在唯一物理相机时可批量绑定服务。

- [SMU 参数、结果与输出关闭](../../01-user-guide/devices/smu.md) — `operations.smu`
  SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。

- [终端进程、会话与脚本运行](../../01-user-guide/interface/terminal.md) — `operations.terminal`
  定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。

- [现场操作验收清单](../../01-user-guide/field-operation-acceptance.md) — `operations.acceptance`
  按交付范围验收启动、设备、流程、数据和外部协议；明确通过、失败、未测和不适用，记录同一轮证据及回退材料与演练状态。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。
