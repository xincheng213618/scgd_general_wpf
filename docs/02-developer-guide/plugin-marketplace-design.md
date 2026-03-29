# ColorVision 插件市场后端 — 技术设计文档

## 1. 现状分析

### 1.1 当前架构

现有的插件更新/下载系统基于**静态文件服务器**（`http://xc213618.ddns.me:9999`），没有真正的后端 API：

| 组件 | 当前实现 | 问题 |
|------|---------|------|
| 版本检查 | 读取 `LATEST_RELEASE` 纯文本文件 | 每个插件独立 HTTP 请求（N+1 问题） |
| 插件下载 | 直接下载 `.cvxp` 文件 | 无下载统计、无完整性校验 |
| 插件发现 | 客户端必须知道插件 ID | 无搜索、无分类、无推荐 |
| 插件发布 | Python 脚本 HTTP PUT 上传 | 无版本管理、无元数据验证 |
| 认证 | 硬编码 Basic Auth `1:1` | 无安全性 |
| 插件元数据 | 分散在 manifest.json + 文件系统 | 无集中管理 |

### 1.2 现有文件结构

```
H:\ColorVision\
├── LATEST_RELEASE              # 应用最新版本号
├── CHANGELOG.md                # 应用更新日志
├── History/                    # 历史完整安装包
├── Update/                     # 增量更新包
├── Plugins/                    # 插件目录
│   ├── Spectrum/
│   │   ├── LATEST_RELEASE
│   │   ├── manifest.json
│   │   ├── PackageIcon.png
│   │   ├── README.md
│   │   ├── CHANGELOG.md
│   │   └── Spectrum-1.0.0.1.cvxp
│   ├── EventVWR/
│   ├── ProjectBlackMura/
│   └── ...
└── Tool/                       # 工具下载
    ├── BeyondCompare/
    ├── ImageJ/
    └── ...
```

## 2. 新方案：Python Flask 后端

### 2.1 技术选型

| 层次 | 选型 | 理由 |
|------|------|------|
| **语言** | Python 3 | 与已有 build 脚本统一、开发效率高 |
| **框架** | Flask | 轻量、简单、同时支持 Web UI 和 API |
| **模板** | Jinja2 + Bootstrap 5 | Flask 内置，响应式 UI |
| **数据库** | SQLite (python sqlite3) | 零配置，仅用于下载统计 |
| **文件存储** | 直接使用现有目录结构 | 无需迁移，兼容旧客户端 |
| **依赖** | 仅 `flask` | 极简依赖 |

### 2.2 为什么选 Python Flask 而不是 C# ASP.NET Core

1. **与构建脚本一致**: `build_plugin.py`、`build_spectrum.py`、`build_update.py` 都是 Python
2. **直接使用现有文件结构**: 无需数据迁移、不引入 ORM/EF Core
3. **Web UI 更简单**: Jinja2 模板 vs WPF 无法做 Web UI
4. **依赖更少**: 只需 `pip install flask`（vs EF Core + Swagger + 多个 NuGet 包）
5. **开发效率**: 修改后热重载，无需编译
6. **部署简单**: `python app.py` 即可运行

### 2.3 架构图

```
┌─────────────────────────────────────────────────────┐
│                    浏览器 / 用户                      │
│    ┌───────────┐  ┌──────────┐  ┌────────────────┐ │
│    │ 插件市场   │  │ 文件浏览  │  │ 上传管理        │ │
│    │ /plugins  │  │ /browse   │  │ /upload        │ │
│    └─────┬─────┘  └────┬─────┘  └───────┬────────┘ │
└──────────┼──────────────┼────────────────┼──────────┘
           │              │                │
           ▼              ▼                ▼
┌─────────────────────────────────────────────────────┐
│              Flask 后端 (app.py)                      │
│                                                      │
│  Web UI 路由              REST API 路由               │
│  ├── /               ├── /api/plugins                │
│  ├── /plugins        ├── /api/plugins/{id}           │
│  ├── /plugins/{id}   ├── /api/plugins/{id}/latest    │
│  ├── /upload         ├── /api/plugins/batch-check    │
│  └── /browse         ├── /api/packages/{id}/{ver}    │
│                      ├── /api/packages/publish       │
│  旧版兼容路由          └── /api/stats                 │
│  ├── /D%3A/ColorVision/Plugins/...                   │
│  └── /upload/ColorVision/...  (PUT)                  │
│                                                      │
│  ┌────────────────┐  ┌───────────────────────────┐  │
│  │ SQLite          │  │ 文件系统 (H:\ColorVision)  │  │
│  │ (下载统计)       │  │ (插件包 + 元数据)          │  │
│  └────────────────┘  └───────────────────────────┘  │
└─────────────────────────────────────────────────────┘
           ▲              ▲                ▲
           │              │                │
┌──────────┼──────────────┼────────────────┼──────────┐
│  WPF 客户端              构建脚本                     │
│  (IMarketplaceService)   (publish_plugin.py)         │
└─────────────────────────────────────────────────────┘
```

## 3. API 设计

### 3.1 Web UI 页面

| 路由 | 功能 |
|------|------|
| `GET /` | 首页 — 存储概览、快速链接 |
| `GET /plugins` | 插件市场 — 搜索、分类、排序 |
| `GET /plugins/{id}` | 插件详情 — 版本列表、README、下载 |
| `GET /upload` | 上传页面 |
| `POST /upload` | 处理上传 |
| `GET /browse[/path]` | 文件浏览器 |

### 3.2 REST API（给 WPF 客户端用）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plugins` | 搜索插件（keyword, category, sort, pagination） |
| GET | `/api/plugins/{id}` | 插件详情 + 所有版本 |
| GET | `/api/plugins/{id}/latest-version` | 纯文本最新版本（兼容 LATEST_RELEASE） |
| POST | `/api/plugins/batch-version-check` | 批量版本检查 |
| GET | `/api/plugins/categories` | 获取所有分类 |
| GET | `/api/packages/{id}/{version}` | 下载插件包（记录统计） |
| POST | `/api/packages/publish` | 发布新版本（multipart） |
| GET | `/api/stats` | 下载统计 |

### 3.3 旧版兼容路由

| 路由模式 | 说明 |
|----------|------|
| `/D%3A/ColorVision/Plugins/{path}` | 兼容旧客户端版本检查和下载 |
| `/D%3A/ColorVision/{path}` | 兼容旧客户端应用更新 |
| `PUT /upload/{path}` | 兼容旧构建脚本上传 |

## 4. 快速启动

```bash
cd Backend/marketplace
pip install -r requirements.txt
python app.py --storage H:\ColorVision --port 9999
```

访问：
- Web UI: http://localhost:9999
- 插件市场: http://localhost:9999/plugins
- API: http://localhost:9999/api/plugins
- 文件浏览: http://localhost:9999/browse

## 5. 项目结构

```
Backend/marketplace/
├── app.py              # Flask 应用（Web UI + API + 旧版兼容）
├── config.json         # 配置文件（storage_path, port 等）
├── requirements.txt    # Python 依赖（仅 flask）
├── marketplace.db      # SQLite 下载统计（自动创建，gitignored）
└── templates/
    ├── base.html       # 布局模板（导航栏 + Bootstrap 5）
    ├── index.html      # 首页
    ├── plugins.html    # 插件市场列表
    ├── plugin_detail.html  # 插件详情
    ├── upload.html     # 上传页面
    └── browse.html     # 文件浏览器

Scripts/
├── publish_plugin.py   # 发布脚本（POST /api/packages/publish）
├── build_plugin.py     # 通用插件打包
├── build_spectrum.py   # Spectrum 插件打包
└── build_update.py     # 应用更新包
```

## 6. 实施路线

### Phase 1: 后端核心 ✅ (当前)

- [x] Flask 应用 + Web UI + REST API
- [x] 直接读取现有 Plugins 目录结构
- [x] 插件市场页面（搜索、分类、排序）
- [x] 插件详情页面（版本列表、下载）
- [x] 文件浏览器
- [x] 上传页面（Web + API）
- [x] 旧版兼容路由
- [x] 下载统计（SQLite）
- [x] publish_plugin.py 发布脚本

### Phase 2: 客户端集成

- [ ] 实现 `MarketplaceClient`（IMarketplaceService 的 HTTP 实现）
- [ ] 修改 `PluginInfoVM.CheckVersion()` 使用批量版本检查
- [ ] 修改 `PluginManager.DownloadPackage()` 使用新 API
- [ ] 添加插件市场浏览 UI（WPF WebView 或原生控件）
- [ ] 降级逻辑：API 不可用时回退到旧文件服务器

### Phase 3: 增强功能

- [ ] 用户认证（API Key / Token）
- [ ] 插件评分和评论
- [ ] 插件依赖自动解析
- [ ] 兼容性矩阵
- [ ] CDN / 镜像加速

### Phase 4: 生产部署

- [ ] Gunicorn / uWSGI 部署
- [ ] Nginx 反向代理
- [ ] HTTPS
- [ ] 日志和监控
