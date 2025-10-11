# 文档组织结构说明

## 概述

本文档说明 ColorVision 项目文档的组织结构和维护指南。

## 文档技术栈

- **VitePress** - 静态站点生成器
- **Vue 3** - 组件框架
- **Markdown** - 文档格式
- **Mermaid** - 图表支持

## 目录结构

```
docs/
├── .vitepress/              # VitePress 配置
│   ├── config.mts          # 主配置文件
│   ├── theme/              # 自定义主题
│   └── dist/               # 构建输出（已忽略）
│
├── introduction/           # 项目介绍
│   ├── 简介.md
│   ├── what-is-colorvision/
│   ├── key-features/
│   └── system-architecture/
│
├── getting-started/        # 入门指南
│   ├── 入门指南.md
│   ├── quick-start/
│   ├── prerequisites/
│   └── installation/
│
├── architecture/           # 架构文档
│   ├── README.md
│   ├── architecture-runtime.md
│   ├── component-interactions.md
│   └── ColorVision.Engine-Refactoring-*.md
│
├── ui-components/          # UI 组件文档
│   ├── UI组件概览.md
│   ├── ColorVision.UI.md
│   └── ...
│
├── engine-components/      # 引擎组件文档
│   ├── Engine组件概览.md
│   ├── ColorVision.Engine.md
│   └── ...
│
├── plugins/               # 插件文档
│   ├── plugin-management/
│   └── using-standard-plugins/
│
├── algorithm-engine-templates/  # 算法引擎和模板
│   ├── flow-engine/
│   ├── template-management/
│   └── ...
│
├── device-management/     # 设备管理
│   ├── device-services-overview/
│   ├── camera-service/
│   └── ...
│
├── user-interface-guide/  # 用户界面指南
│   ├── main-window/
│   ├── image-editor/
│   └── ...
│
├── developer-guide/       # 开发者指南
│   └── api-reference/
│
├── troubleshooting/       # 故障排除
├── performance/           # 性能优化
├── security/              # 安全性
├── rbac/                  # 权限控制
├── deployment/            # 部署文档
├── update/                # 更新系统
├── changelog/             # 更新日志
│   ├── README.md
│   └── CHANGELOG.md -> ../../CHANGELOG.md  # 符号链接
│
├── index.md               # 首页
├── 根目录README.md -> ../README.md        # 符号链接到根 README
└── ...
```

## 关键变更

### 从 Docsify/Jekyll 迁移到 VitePress

1. **移除的文件**:
   - `.nojekyll` - Jekyll 配置文件
   - `_config.yml` - Jekyll 配置
   - `_layouts/` - Jekyll 布局模板
   - `*.docsify` - Docsify 配置文件
   - `_sidebar.md.docsify` - Docsify 侧边栏
   - `_coverpage.md.docsify` - Docsify 封面页
   - `index.html.docsify` - Docsify 入口
   - `_legacy_index_backup.html` - 旧版备份

2. **新增的结构**:
   - `introduction/` - 项目介绍文档
   - `performance/` - 性能优化指南
   - 符号链接用于引用根目录的 README 和 CHANGELOG

3. **符号链接方案**:
   - `docs/根目录README.md` → `../README.md`
   - `docs/changelog/CHANGELOG.md` → `../../CHANGELOG.md`
   
   这样避免了文件重复，保持单一数据源。

## 文档维护指南

### 添加新文档

1. 在相应的目录下创建 Markdown 文件
2. 更新 `docs/.vitepress/config.mts` 的侧边栏配置
3. 使用相对路径引用其他文档

### 更新 README 和 CHANGELOG

直接编辑根目录下的文件：
- `/README.md`
- `/CHANGELOG.md`

文档站点会自动通过符号链接引用这些文件。

### 构建和预览

```bash
# 开发服务器（热重载）
npm run docs:dev

# 构建生产版本
npm run docs:build

# 预览构建结果
npm run docs:preview
```

### Markdown 注意事项

1. **HTML 标签转义**: 
   - 使用 `\<T\>` 而不是 `<T>` 来表示泛型
   - 使用反引号包裹特殊字符：`` `><` ``

2. **图片引用**:
   - 使用绝对路径：`/scgd_general_wpf/assets/image.png`
   - 或相对路径：`./images/image.png`

3. **链接**:
   - 内部链接使用 `/path/to/page` 格式
   - 自动添加 `.html` 扩展名（cleanUrls 配置）

## VitePress 配置

主配置文件：`docs/.vitepress/config.mts`

关键配置项：

```typescript
export default defineConfig({
  title: "ColorVision",
  base: '/scgd_general_wpf/',
  cleanUrls: true,
  ignoreDeadLinks: true,
  
  themeConfig: {
    nav: [...],      // 顶部导航
    sidebar: [...],  // 侧边栏
    search: {...}    // 搜索配置
  }
})
```

## 部署

文档通过 GitHub Actions 自动部署到 GitHub Pages：

- 工作流：`.github/workflows/pages.yml`
- 部署分支：`gh-pages`
- 访问地址：https://xincheng213618.github.io/scgd_general_wpf/

## 相关资源

- [VitePress 官方文档](https://vitepress.dev/)
- [Markdown 扩展语法](https://vitepress.dev/guide/markdown)
- [Mermaid 图表语法](https://mermaid.js.org/)

---

*最后更新: 2025-10-11*
