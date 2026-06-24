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
npm run docs:validate
```

生成的站点输出到 `docs/.vitepress/dist/`。`docs:build` 会同时运行 VitePress 严格死链检查、生成自定义索引，并校验旧页面兼容入口不会进入搜索。`docs:validate` 可在已有构建产物上单独复查本地链接、兼容跳转页和搜索索引。

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
| `.vitepress/` | VitePress 配置、导航元数据和索引生成脚本 |

## 语言策略

简体中文是当前唯一维护中的文档。英文、繁体中文、日文、韩文副本不再保留在当前工作树；除非有明确交付需求，否则不恢复多语言目录。需要时从 Git 历史找回后按当前结构重新整理。

新增或移动页面时，更新受影响章节 README 和 `docs/.vitepress/i18n/navigation-data.json`，然后运行 `npm run docs:build`。
