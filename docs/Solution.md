# 解决方案文件说明

## 概述

ColorVision 使用 Visual Studio 解决方案 (.sln) 文件来组织项目结构。

## 解决方案结构

主解决方案文件：`ColorVision.sln`

### 项目组织

解决方案包含以下主要项目组：

#### 1. 核心项目

- **ColorVision** - 主程序入口
- **ColorVision.Engine** - 核心引擎
- **cvColorVision** - 视觉处理核心

#### 2. UI 项目

- **ColorVision.UI** - 主界面框架
- **ColorVision.Themes** - 主题系统
- **ColorVision.Common** - 通用组件

#### 3. 引擎组件

- **FlowEngineLib** - 流程引擎库
- **ColorVision.FileIO** - 文件 I/O
- **ColorVision.Engine.Templates** - 模板系统

#### 4. 插件项目

- **Plugins/** 目录下的各个插件项目

#### 5. 客户定制项目

- **Projects/** 目录下的定制项目

## 构建配置

### Debug 配置

用于开发和调试：
- 包含调试符号
- 禁用优化
- 启用断言

### Release 配置

用于生产部署：
- 移除调试符号
- 启用优化
- 生成紧凑的二进制文件

## 项目依赖关系

\`\`\`mermaid
graph TD
    A[ColorVision 主程序] --> B[ColorVision.UI]
    A --> C[ColorVision.Engine]
    B --> D[ColorVision.Common]
    C --> E[FlowEngineLib]
    C --> F[cvColorVision]
\`\`\`

## 构建顺序

1. 基础库（Common, Core）
2. 引擎组件（Engine, FlowEngineLib）
3. UI 组件（UI, Themes）
4. 主程序（ColorVision）
5. 插件（Plugins）

## 开发环境要求

- **Visual Studio 2022** 或更高版本
- **.NET 8 SDK**
- **C# 12** 语言支持

## 快速开始

### 克隆仓库

\`\`\`bash
git clone https://github.com/xincheng213618/scgd_general_wpf.git
cd scgd_general_wpf
\`\`\`

### 恢复依赖

\`\`\`bash
dotnet restore
\`\`\`

### 构建解决方案

\`\`\`bash
dotnet build
\`\`\`

### 运行主程序

\`\`\`bash
dotnet run --project ColorVision/ColorVision.csproj
\`\`\`

## 相关文档

- [项目简介](/introduction/简介)
- [系统架构](/introduction/system-architecture/系统架构概览)
- [入门指南](/getting-started/入门指南)
