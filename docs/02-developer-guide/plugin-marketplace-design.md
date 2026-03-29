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

### 1.2 关键文件

```
客户端:
├── UI/ColorVision.UI/Plugins/PluginLoader.cs          # 插件加载
├── UI/ColorVision.UI/Plugins/PluginLoaderrConfig.cs   # 更新地址配置
├── UI/ColorVision.UI/Plugins/PluginManifest.cs        # manifest 结构
├── UI/ColorVision.UI.Desktop/Plugins/PluginManager.cs # 插件管理 UI
├── UI/ColorVision.UI.Desktop/Plugins/PluginInfoVM.cs  # 版本检查逻辑
├── ColorVision/Update/AutoUpdater.cs                  # 应用更新

构建脚本:
├── Scripts/build_plugin.py       # 通用插件打包
├── Scripts/build_spectrum.py     # Spectrum 插件打包
├── Scripts/build_update.py       # 应用更新包
├── Scripts/file_manager.py       # HTTP 文件上传工具
```

### 1.3 核心痛点

1. **N+1 版本检查**: 每个插件独立发 HTTP 请求检查 `LATEST_RELEASE`
2. **无插件发现**: 用户必须知道插件 ID 才能下载，无法浏览/搜索
3. **无下载统计**: 不知道哪些插件被使用，无法做数据驱动决策
4. **无完整性校验**: 下载的 `.cvxp` 没有 hash 验证
5. **无集中元数据**: 插件信息分散在文件系统各处
6. **无安全性**: 硬编码认证，任何人可上传
7. **无兼容性检查**: 客户端需自行验证 `requires` 版本

---

## 2. 目标架构

### 2.1 从"插件管理"到"插件市场"

```
┌─────────────────────────────────────────────────┐
│                  WPF 客户端                       │
│  ┌─────────────┐  ┌───────────────────────────┐ │
│  │ 插件管理器   │  │    插件市场 (新)            │ │
│  │ (已安装插件) │  │  搜索/浏览/分类/下载        │ │
│  └──────┬──────┘  └────────────┬──────────────┘ │
│         │                      │                 │
│         ▼                      ▼                 │
│    IMarketplaceService (统一接口)                 │
│         │                                        │
│    ┌────┴────────────┐                          │
│    │ MarketplaceConfig│ (API URL / 降级到旧模式)  │
│    └─────────────────┘                          │
└──────────────┬──────────────────────────────────┘
               │ HTTP/HTTPS
               ▼
┌─────────────────────────────────────────────────┐
│        Plugin Marketplace Backend (新)            │
│                                                  │
│  ┌──────────┐  ┌──────────┐  ┌───────────────┐ │
│  │ Plugins  │  │ Packages │  │   Versions    │ │
│  │ API      │  │ API      │  │   API         │ │
│  └────┬─────┘  └────┬─────┘  └──────┬────────┘ │
│       │              │               │           │
│       ▼              ▼               ▼           │
│  ┌─────────────────────────────────────────┐    │
│  │         PluginService                    │    │
│  │  (搜索/版本检查/发布/下载统计)            │    │
│  └─────────────┬───────────────────────────┘    │
│                │                                 │
│  ┌─────────────┴──────┐  ┌──────────────────┐  │
│  │  SQLite / PostgreSQL│  │  File Storage    │  │
│  │  (元数据 + 统计)    │  │  (.cvxp 包文件)   │  │
│  └────────────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────┘
               ▲
               │ HTTP PUT / POST
┌──────────────┴──────────────────────────────────┐
│        构建脚本 (CI/CD)                          │
│  build_plugin.py → POST /api/packages/publish    │
│  build_spectrum.py → POST /api/packages/publish  │
│  build_update.py → (应用更新, 暂保留旧模式)        │
└─────────────────────────────────────────────────┘
```

### 2.2 API 设计

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plugins` | 搜索/列出插件（支持 keyword, category, sort, pagination） |
| GET | `/api/plugins/{pluginId}` | 获取插件详情（含所有版本） |
| GET | `/api/plugins/{pluginId}/latest-version` | 获取最新版本（纯文本，兼容旧客户端） |
| POST | `/api/plugins/batch-version-check` | 批量版本检查（一次请求检查所有插件） |
| GET | `/api/plugins/categories` | 获取所有分类 |
| GET | `/api/packages/{pluginId}/{version}` | 下载插件包 |
| POST | `/api/packages/publish` | 发布插件新版本（multipart: 元数据 + .cvxp） |

---

## 3. 技术选型

### 3.1 后端技术栈

| 层次 | 选型 | 理由 |
|------|------|------|
| **框架** | ASP.NET Core 8.0 Web API | 与 ColorVision 生态一致（C#/.NET），团队熟悉 |
| **ORM** | Entity Framework Core | .NET 标准 ORM，支持 Migration |
| **数据库** | SQLite（初期）→ PostgreSQL（生产） | SQLite 零配置快速启动，后期无缝切换 |
| **文件存储** | 本地文件系统（初期）→ S3/Azure Blob（生产） | StorageService 抽象层，可替换实现 |
| **API 文档** | Swagger / OpenAPI | 自动生成文档，方便调试 |
| **认证** | API Key（初期）→ JWT（生产） | 发布端需认证，下载端可匿名 |
| **容器化** | Docker（可选） | 简化部署 |

### 3.2 为什么选 ASP.NET Core

1. **技术栈统一**: 整个 ColorVision 项目都是 C#/.NET，后端使用同技术栈减少学习成本
2. **DTO 共享**: 后端 DTO 类型可直接被客户端引用（通过 NuGet 包或项目引用）
3. **高性能**: ASP.NET Core 在 TechEmpower 基准测试中性能优异
4. **跨平台**: 可部署在 Windows/Linux
5. **EF Core**: 成熟的 ORM，支持多种数据库

### 3.3 客户端集成

| 组件 | 说明 |
|------|------|
| `IMarketplaceService` | 市场服务接口（在 ColorVision.UI 中） |
| `MarketplaceConfig` | 配置（API URL、是否启用） |
| `MarketplaceClient` | HTTP 客户端实现（在 ColorVision.UI.Desktop 中） |

---

## 4. 数据模型

### 4.1 核心实体

```
Plugin (插件)
├── Id (PK)
├── PluginId (unique, e.g., "Spectrum")
├── Name, Description, Author, Url
├── Category
├── IconPath
├── Readme (markdown)
├── TotalDownloads
├── IsPublished
├── CreatedAt, UpdatedAt
└── Versions[] ──┐
                  │
PluginVersion     │
├── Id (PK)       │
├── PluginId (FK) ┘
├── Version (e.g., "1.3.15.8")
├── RequiresVersion (min engine version)
├── ChangeLog (markdown)
├── PackagePath (.cvxp file)
├── FileSize, FileHash (SHA256)
├── DownloadCount
└── CreatedAt

DownloadRecord (下载记录)
├── Id (PK)
├── PluginVersionId (FK)
├── ClientIpHash (隐私保护)
├── ClientVersion
└── DownloadedAt
```

---

## 5. 逐步实施路线

### Phase 1: 后端核心 API ✅ (当前)

- [x] 创建 ASP.NET Core Web API 项目
- [x] 定义数据模型（Plugin, PluginVersion, DownloadRecord）
- [x] 实现 EF Core DbContext + SQLite
- [x] 实现核心服务（PluginService, StorageService）
- [x] 实现 API 控制器（PluginsController, PackagesController）
- [x] 添加 Swagger 文档
- [x] 创建客户端接口（IMarketplaceService）和配置（MarketplaceConfig）

### Phase 2: 构建脚本集成

- [ ] 修改 `build_plugin.py` 支持 POST 到新 API
- [ ] 修改 `build_spectrum.py` 支持 POST 到新 API  
- [ ] 创建 `Scripts/publish_plugin.py` 新发布脚本
- [ ] 添加数据迁移工具（从旧文件服务器导入已有插件元数据）

### Phase 3: 客户端集成

- [ ] 实现 `MarketplaceClient`（IMarketplaceService 的 HTTP 实现）
- [ ] 修改 `PluginInfoVM.CheckVersion()` 使用批量版本检查 API
- [ ] 修改 `PluginManager.DownloadPackage()` 使用新 API
- [ ] 添加插件市场浏览 UI（搜索、分类、列表）
- [ ] 保持向后兼容：当 API 不可用时降级到旧文件服务器模式

### Phase 4: 增强功能

- [ ] 插件分类管理和标签系统
- [ ] 下载统计仪表板
- [ ] 插件评分和评论系统
- [ ] 插件依赖关系自动解析
- [ ] 兼容性矩阵（哪个插件版本兼容哪个 ColorVision 版本）
- [ ] 增量更新支持（只传输变更部分）

### Phase 5: 生产部署

- [ ] 切换到 PostgreSQL
- [ ] 添加 JWT 认证
- [ ] 添加 API 速率限制
- [ ] Docker 容器化
- [ ] CI/CD 自动构建和发布
- [ ] CDN 加速插件下载
- [ ] HTTPS 强制

---

## 6. 向后兼容策略

新系统必须与现有客户端共存：

1. **`/api/plugins/{pluginId}/latest-version`** 返回纯文本版本字符串，与 `LATEST_RELEASE` 格式完全一致
2. **`MarketplaceConfig.UseMarketplaceApi`** 控制是否使用新 API
3. **旧客户端** 继续通过文件服务器获取更新（不受影响）
4. **新客户端** 优先使用 API，失败时自动降级到旧模式
5. **构建脚本** 可同时上传到旧文件服务器和新 API

---

## 7. 项目结构

```
Backend/
└── ColorVision.PluginMarketplace/
    ├── Controllers/
    │   ├── PluginsController.cs      # 插件搜索/详情/版本检查
    │   └── PackagesController.cs     # 包下载/发布
    ├── Models/
    │   ├── Plugin.cs                 # 插件实体
    │   ├── PluginVersion.cs          # 版本实体
    │   └── DownloadRecord.cs         # 下载记录
    ├── Data/
    │   └── MarketplaceDbContext.cs    # EF Core 上下文
    ├── DTOs/
    │   └── PluginDtos.cs             # 数据传输对象
    ├── Services/
    │   ├── PluginService.cs          # 核心业务逻辑
    │   └── StorageService.cs         # 文件存储服务
    ├── Program.cs                    # 应用入口
    └── appsettings.json              # 配置

UI/ColorVision.UI/Marketplace/
├── IMarketplaceService.cs            # 市场服务接口
└── MarketplaceConfig.cs              # 配置

Scripts/
├── publish_plugin.py                 # 新的发布脚本 (Phase 2)
└── ...existing scripts...
```

---

## 8. 快速启动

### 运行后端

```bash
cd Backend/ColorVision.PluginMarketplace
dotnet run
# API 可在 http://localhost:5000 访问
# Swagger UI: http://localhost:5000/swagger
```

### 发布插件（使用 curl）

```bash
curl -X POST http://localhost:5000/api/packages/publish \
  -F "PluginId=Spectrum" \
  -F "Name=Spectrum" \
  -F "Version=1.0.0.1" \
  -F "Description=Spectrum analysis plugin" \
  -F "Author=xincheng" \
  -F "Category=Analysis" \
  -F "package=@Spectrum-1.0.0.1.cvxp"
```

### 搜索插件

```bash
curl "http://localhost:5000/api/plugins?Keyword=spectrum&Category=Analysis"
```

### 批量版本检查

```bash
curl -X POST http://localhost:5000/api/plugins/batch-version-check \
  -H "Content-Type: application/json" \
  -d '{"PluginIds": ["Spectrum", "EventVWR", "ProjectBlackMura"]}'
```
