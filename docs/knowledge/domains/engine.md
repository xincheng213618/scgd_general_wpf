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

- [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) — `engine.cv-image-export`
  CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。

- [CVCIE POI 结果数值](../../04-api-reference/engine-components/cvcie-results.md) — `engine.cvcie-results`
  CVCIE POI 的非正值替换、色值重算与生效时机；区分本地测量、历史结果缓存、鼠标探针和原始文件。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库维护窗口与provider能力：表统计不是删除预览；备份默认关闭，备份和清理不是事务且失败不自动恢复；清理、手动优化和迁移边界彼此独立。

- [Engine 设备资源与运行装配](../../04-api-reference/engine-components/device-service-chain.md) — `engine.devices`
  设备工厂、资源重载与显示装配；旧对象释放、集合重建和显示替换并非一个事务，记录存在、默认可见、服务在线和动作完成分别判断。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。

- [ColorVision.Engine 工程、资源与依赖](../../04-api-reference/engine-components/ColorVision.Engine.md) — `engine.host`
  ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。

- [MySQL 结果索引优化、清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 结果表的手动关联索引优化、历史删除、整表截断和SQL备份；在线DDL、并发、部分成功、备份与恢复边界分别说明。

- [MySQL SQL 恢复、重置与资源保留](../../04-api-reference/engine-components/mysql-recovery.md) — `engine.mysql-recovery`
  MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册、服务目录同步、状态快照与连接测试；远端删除不清本地令牌和收发主题，更新可能部分生效，连接或测试成功不等于设备就绪。

- [算法结果交接、展示与导出](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  算法结果接收、历史查询、handler 匹配、缺图回放与数据导出，以及统一 overlay 的文档/revision 生命周期；入库、通知、显示和保存分别判断。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [主程序光谱仪搜索与配置](../../04-api-reference/engine-components/spectrum-device.md) — `engine.spectrum-device`
  主程序光谱仪的全连接方式搜索、设备配置分类和许可证读取入口；区分本机搜索、服务端刷新与实际连接。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  Engine MQTT 的连接与订阅、异步发送、请求状态、迟到回包和 MsgID 复用限制；区分 Flow 客户端池与设备命令链。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) — `engine.native-bindings`
  定位供应商 native DLL 的相机、光谱、XYZ、OLED、PG 与源表绑定契约。

- [opencv\_helper.dll API 参考](../../04-api-reference/engine-components/opencv-helper-api.md) — `engine.opencv-helper-api`
  opencv\_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。

- [设备视图内存预览设计（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  待实施的设备视图无文件预览：明确与本地手动窗口的区别、发布租约之外的读写同步、latest-wins、RAW/CIE显示副本及验收缺口。
