---
layout: home

hero:
  name: "ColorVision"
  text: "光电技术与色彩管理一体化平台"
  tagline: 基于 Windows WPF 技术的专业应用程序，专注于提供先进的色彩管理和光电技术解决方案。支持多设备集成、流程自动化、插件扩展，满足光电技术研发与工业自动化需求。
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: 开始安装
      link: /00-getting-started/README
    - theme: alt
      text: 日常使用
      link: /01-user-guide/README
    - theme: alt
      text: 设计与架构
      link: /03-architecture/README

features:
  - icon: 🚀
    title: 安装与首次使用
    details: 先确认系统要求，再完成安装、首次启动和最小闭环体验
    link: /00-getting-started/README
  
  - icon: 📖
    title: 日常使用
    details: 按界面、设备、工作流程和故障排查组织的用户文档入口
    link: /01-user-guide/README
  
  - icon: 🧩
    title: 开发与交付
    details: 面向插件、Engine、部署、更新和构建脚本的开发者入口
    link: /02-developer-guide/README
  
  - icon: 🏗️
    title: 设计与架构
    details: 从系统级视角理解运行时、组件交互和模板系统设计
    link: /03-architecture/README
  
  - icon: 📚
    title: API 与源码导读
    details: 查接口、看模块入口、定位模板和插件实现位置
    link: /04-api-reference/README
  
  - icon: 🗂️
    title: 结构与附录
    details: 用项目结构总览和模块文档对照表快速定位仓库内容
    link: /05-resources/README
---

## 📚 如何选文档

| 如果你现在想做什么 | 应该先看哪里 | 说明 |
|------|------|----------|
| 安装程序或确认机器能不能跑 | [入门指南](/00-getting-started/README) | 覆盖系统要求、安装、首次运行和快速上手 |
| 已经装好程序，想学界面和操作 | [用户指南](/01-user-guide/README) | 面向日常使用，不展开内部实现 |
| 要改代码、做插件、打包发布 | [开发指南](/02-developer-guide/README) | 面向扩展点、构建、部署和交付流程 |
| 想理解系统为什么这样设计 | [架构设计](/03-architecture/README) | 关注运行时关系、组件边界和设计思路 |
| 想直接查模块、接口和实现入口 | [API 参考](/04-api-reference/README) | 面向源码导航，不是用户手册 |
| 想看仓库目录、附录和文档映射 | [附录与资源](/05-resources/README) | 用于快速定位资料，不承载主教程 |

## 当前整理原则

- 安装与首次使用，集中放在 `00-getting-started/`
- 日常操作，集中放在 `01-user-guide/`
- 二次开发、部署和交付，集中放在 `02-developer-guide/`
- 设计边界与运行时理解，集中放在 `03-architecture/`
- 模块、接口和实现导读，集中放在 `04-api-reference/`
- 项目结构、附录和稳定索引，集中放在 `05-resources/`

## 技术栈

![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)
![License](https://img.shields.io/github/license/xincheng213618/scgd_general_wpf.svg)
![Stars](https://img.shields.io/github/stars/xincheng213618/scgd_general_wpf.svg)
