# ColorVision 项目文档

欢迎访问 ColorVision 项目文档网站！

## 📚 文档导航

### 快速开始
- [入门指南](opendeep/getting-started/入门指南.md) - 快速开始使用 ColorVision
- [项目简介](opendeep/introduction/简介.md) - 了解项目概况和核心功能

### 开发文档  
- [开发者指南](opendeep/developer-guide/开发者指南.md) - 面向开发者的详细指南
- [贡献指南](opendeep/developer-guide/contribution-guidelines/贡献指南.md) - 如何参与项目贡献
- [系统架构](opendeep/introduction/system-architecture/系统架构概览.md) - 系统整体架构设计

### 功能模块
- [用户界面指南](opendeep/user-interface-guide/) - UI 使用指南和主题设置
- [设备管理](opendeep/device-management/) - 设备配置和控制功能
- [插件开发](opendeep/plugins/) - 插件系统和开发指南
- [专用算法](opendeep/specialized-algorithms/) - 算法模块详解

## 🌐 在线访问

本文档网站使用 GitHub Pages 构建，支持以下访问方式：

- **在线查看**：[https://xincheng213618.github.io/scgd_general_wpf/](https://xincheng213618.github.io/scgd_general_wpf/)
- **源代码**：[https://github.com/xincheng213618/scgd_general_wpf](https://github.com/xincheng213618/scgd_general_wpf)

## 🛠️ 本地构建

如需在本地构建文档网站：

### 使用 Jekyll

```bash
# 安装 Jekyll
gem install bundler jekyll

# 进入 docs 目录
cd docs

# 安装依赖
bundle install

# 本地运行
bundle exec jekyll serve

# 访问 http://localhost:4000
```

### 使用简单 HTTP 服务器

```bash
# 进入 docs 目录
cd docs

# Python 3
python -m http.server 8000

# Python 2
python -m SimpleHTTPServer 8000

# Node.js (需要先安装 http-server)
npx http-server .

# 访问 http://localhost:8000
```

## 📝 文档更新

文档采用 Markdown 格式编写，支持：

- ✅ GitHub Flavored Markdown (GFM)
- ✅ Mermaid 图表渲染
- ✅ 代码高亮显示
- ✅ 响应式布局
- ✅ 多语言支持

### 添加新文档

1. 在相应目录下创建 `.md` 文件
2. 在文件开头添加 Front Matter：

```yaml
---
title: 页面标题
description: 页面描述
layout: default
---
```

3. 使用标准 Markdown 语法编写内容
4. 提交到仓库，GitHub Pages 会自动构建

## 🎨 主题和样式

文档网站采用简洁的设计风格：

- **响应式设计**：适配桌面端和移动端
- **代码高亮**：支持多种编程语言语法高亮
- **图表支持**：集成 Mermaid.js 用于渲染流程图和架构图
- **导航便利**：左侧导航栏和面包屑导航
- **搜索优化**：SEO 友好的页面结构

## 📧 联系我们

- **公司**：视彩（上海）光电技术有限公司
- **项目主页**：[GitHub Repository](https://github.com/xincheng213618/scgd_general_wpf)
- **问题反馈**：[Issues](https://github.com/xincheng213618/scgd_general_wpf/issues)

## 📄 许可证

本项目采用 [MIT 许可证](../LICENSE) 进行许可。

---

最后更新：{{ "now" | date: "%Y-%m-%d" }}