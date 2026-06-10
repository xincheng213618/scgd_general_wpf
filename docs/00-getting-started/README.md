# 安装与首次使用

本章节只保留首次接触 ColorVision 时最需要的入口，避免和后续章节重复。

## 建议阅读顺序

1. [什么是 ColorVision](./what-is-colorvision.md)
2. [系统要求](./prerequisites.md)
3. [安装指南](./installation.md)
4. [首次运行](./first-steps.md)
5. [快速上手](./quick-start.md)

## 适用范围

- 新用户想完成安装、启动和基础验证
- 新同事想快速知道主程序、设备、流程和插件分别在哪里
- 开发者想确认源码构建的最短路径

## 你会在这里找到什么

- 产品定位和典型使用场景
- Windows 环境要求与安装前准备
- 主程序首次启动后的基础操作路径
- 从源码运行主程序的最小步骤

## 从源码启动

当前仓库以 Windows WPF 和 x64 为主，建议先完成依赖恢复，再构建主程序：

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

## 继续阅读

- 想先了解客户项目和方案包：前往 [项目说明](../00-projects/README.md)
- 想看界面和日常操作：前往 [使用手册](../01-user-guide/README.md)
- 想看系统设计和模块边界：前往 [架构设计](../03-architecture/README.md)
- 想看仓库目录与模块分工：前往 [项目结构总览](../05-resources/project-structure/README.md)
- 想进行二次开发：前往 [开发手册](../02-developer-guide/README.md)

## 说明

- 旧版入门页中与架构、安装器实现、发布脚本相关的长篇内容，已经收敛到对应章节，不再在这里重复维护。
- 若文档与当前代码行为不一致，以源码和实际构建结果为准。

