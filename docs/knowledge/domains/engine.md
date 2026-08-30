---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 设备、服务与结果

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

设备服务、MQTT、模板宿主和结果展示。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [Engine 知识入口](../../04-api-reference/engine-components/README.md) — `engine.index`
  按实际代码职责路由 Engine 的设备、消息、模板、Flow、结果与工程依赖；契约和验证由各主题维护。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [Engine 设备资源与运行装配](../../04-api-reference/engine-components/device-service-chain.md) — `engine.devices`
  设备资源、工厂、运行集合与显示页的装配契约；区分记录存在、默认可见、服务在线和动作完成。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 二进制读取、关联源文件与内嵌通道的区别，以及版本写回、长度校验和失败边界。

- [ColorVision.Engine 工程、资源与依赖](../../04-api-reference/engine-components/ColorVision.Engine.md) — `engine.host`
  ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。

- [MySQL 结果清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 批次与结果表的历史删除、整表截断和SQL备份；统计不是清理预览，无全程事务或自动恢复，主从选择和管理员权限不能只依赖界面提示。

- [MySQL SQL 恢复、重置与资源保留](../../04-api-reference/engine-components/mysql-recovery.md) — `engine.mysql-recovery`
  MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册令牌、启动早到服务快照与连接测试；连接标志不等于设备就绪，测试会影响运行单例，取消不回滚注册或订阅。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [本地相机内存帧预览：实施与验证 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-validation.md) — `engine.camera-preview-validation-plan`
  列出尚未实施的相机内存预览阶段、验收用例和实施前需要重新核对的源码。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  说明 Engine MQTT 连接、设备请求、MsgID 关联、超时和订阅恢复。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) — `engine.native-bindings`
  定位供应商 native DLL 的相机、光谱、XYZ、OLED、PG 与源表绑定契约。

- [本地相机内存帧预览：生命周期与显示语义 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-runtime.md) — `engine.camera-preview-lifecycle-plan`
  记录待实施预览的租约取得、latest-wins、RAW/CIE 模式和内存预算约束。

- [本地相机内存帧预览方案（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  记录待实施的设备级内存帧预览设计，不代表当前 ViewCamera 已支持无文件历史结果。
