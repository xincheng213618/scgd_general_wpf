# 文档目录说明

本目录包含 ColorVision 的详细文档和 VitePress 站点源码。

## 从这里开始

| 目标 | 入口 |
| --- | --- |
| 查看文档首页 | [index.md](./index.md) |
| 安装或快速试用 | [快速上手](./00-getting-started/quick-start.md) |
| 日常操作 | [使用手册](./01-user-guide/README.md) |
| 交付或维护客户项目 | [项目说明](./00-projects/README.md) |
| 开发、构建、测试或发布 | [开发手册](./02-developer-guide/README.md) |
| 查源码模块和 API 参考 | [参考资料](./04-api-reference/README.md) |

## 站点命令

```powershell
npm install
npm run docs:dev
npm run docs:build
```

生成的站点输出到 `docs/.vitepress/dist/`。

## 目录结构

| 路径 | 作用 |
| --- | --- |
| `00-getting-started/` | 安装和首次使用入口 |
| `00-projects/` | 客户项目和项目包入口 |
| `01-user-guide/` | 日常操作、设备、流程、数据和故障排查 |
| `02-developer-guide/` | 开发、测试、部署、脚本、后端和插件开发 |
| `03-architecture/` | 系统架构和运行时设计说明 |
| `04-api-reference/` | 源码/模块参考、算法、Engine、UI、插件、项目和扩展点 |
| `05-resources/` | 稳定附录，例如项目结构、模块映射和法律文本 |
| `en/` | 精简英文入口页 |
| `.vitepress/` | VitePress 配置、导航、语言元数据和索引生成脚本 |

## 语言策略

简体中文是完整且维护中的文档。英文只保留精简入口页。繁体中文、日文、韩文副本已从当前工作树移除；除非有明确交付需求，否则不恢复全量维护。需要时从 Git 历史找回后按当前结构重新整理。

新增或移动页面时，更新受影响章节 README 和 `docs/.vitepress/i18n/navigation-data.json`，然后运行 `npm run docs:build`。
