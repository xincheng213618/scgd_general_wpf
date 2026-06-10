---
layout: home

hero:
  name: "ColorVision"
  text: "光电检测平台文档中心"
  tagline: 按项目、使用、开发、插件开发和现有插件能力组织的交接文档。先看业务项目，再进入操作和开发细节。
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: 项目说明
      link: /00-projects/README
    - theme: alt
      text: 使用手册
      link: /01-user-guide/README
    - theme: alt
      text: 插件能力
      link: /04-api-reference/plugins/README

features:
  - title: 项目说明
    details: 当前客户项目和方案包的业务定位、流程组织、协议入口、结果导出和交接顺序。
    link: /00-projects/README
  - title: 使用手册
    details: 安装、首次运行、主窗口、设备、流程执行、数据管理和常见问题，面向实际操作人员。
    link: /01-user-guide/README
  - title: 开发手册
    details: Engine 扩展、UI DLL 发布、部署更新、构建脚本、后端服务和项目交付流程。
    link: /02-developer-guide/README
  - title: 插件开发手册
    details: 插件接口、manifest、装载流程、构建复制、打包发布和现有插件参考。
    link: /02-developer-guide/plugin-development/README
  - title: 现有插件能力说明
    details: Conoscope、Spectrum、SystemMonitor、EventVWR、WindowsServicePlugin 的能力、入口和边界。
    link: /04-api-reference/plugins/README
  - title: 模块参考
    details: UI DLL、Engine 业务链路、算法模板、扩展点和源码锚点。
    link: /04-api-reference/README
  - title: 附录索引
    details: 仓库结构、模块文档对照表、许可协议和长期稳定资料。
    link: /05-resources/README
---

## 先按目标选入口

| 你现在要做什么 | 先看哪里 | 读完应该知道什么 |
| --- | --- | --- |
| 接手某个客户项目 | [项目说明](/00-projects/README) | 每个项目的业务定位、流程组织、协议入口、结果导出和维护边界 |
| 安装、第一次运行或日常操作 | [使用手册](/01-user-guide/README) | 系统要求、主窗口、设备、图像编辑器、流程执行、数据和故障排查 |
| 改代码、构建包、交付版本 | [开发手册](/02-developer-guide/README) | Engine/UI/部署/脚本/后端的开发入口和交付检查 |
| 新增或维护插件 | [插件开发手册](/02-developer-guide/plugin-development/README) | 插件接口、manifest、装载、复制、打包和发布约定 |
| 查当前插件能做什么 | [现有插件能力说明](/04-api-reference/plugins/README) | 每个现有插件的能力、入口、依赖、构建和维护风险 |
| 查源码模块 | [模块参考](/04-api-reference/README) | UI、Engine、算法模板、扩展点对应的职责、入口和关键类 |
| 快速定位目录 | [附录与资源](/05-resources/README) | 仓库目录、文档映射和稳定附录 |

## 主线五大模块

文档按“先理解交付对象，再进入操作和开发”的顺序组织：

| 模块 | 目录 | 解决的问题 |
| --- | --- | --- |
| 项目说明 | `00-projects/`、`04-api-reference/projects/` | 这个客户项目是什么、怎么触发、怎么运行流程、怎么导出结果、怎么交接 |
| 使用手册 | `00-getting-started/`、`01-user-guide/` | 操作者如何安装、启动、连接设备、执行流程、查看数据和排查现场问题 |
| 开发手册 | `02-developer-guide/`、`03-architecture/`、`04-api-reference/engine-components/`、`04-api-reference/ui-components/` | 开发人员如何理解 Engine 业务链、UI DLL、构建、测试、部署和发布 |
| 插件开发手册 | `02-developer-guide/plugin-development/` | 如何新增、装载、调试、打包和发布通用插件 |
| 现有插件能力说明 | `04-api-reference/plugins/` | 当前真实存在的插件能做什么、入口在哪里、依赖什么、如何验收和排障 |

## 支撑资料

以下内容为五大模块服务，不作为第一阅读入口：

- `04-api-reference/`：模块参考，承接 UI DLL、Engine、算法模板、扩展点、插件和项目详页。
- `05-resources/`：项目结构、模块文档对照、许可协议和长期稳定附录。
- 多语言目录：中文稳定后，再按中文结构同步英文、繁体中文、日文和韩文。

## 维护原则

- 用户手册只写操作者能看到和能执行的内容。
- 模块参考必须能回到当前源码目录、项目文件、manifest 或关键类。
- UI 模块重点说明 DLL/NuGet 发布、依赖关系和引用方式。
- Engine 模块重点说明设备、模板、流程、MQTT、数据和结果展示的业务链路。
- 插件和项目包只以当前仓库真实存在的目录为准，历史缺失目录不再写成当前功能。
