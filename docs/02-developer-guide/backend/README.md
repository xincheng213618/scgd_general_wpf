# 插件市场后端 (Plugin Marketplace Backend)

ColorVision 插件市场后端是一个基于 Python Flask 的轻量级服务，用于管理插件的发布、下载和版本控制。

## 功能概述

后端服务提供以下核心功能：

- **Web 管理界面** - 浏览、搜索、下载和上传插件
- **REST API** - 为 WPF 桌面客户端提供接口
- **旧版兼容** - 支持与旧版本客户端的兼容路由
- **下载统计** - 基于 SQLite 的下载统计

## 项目结构

```
Backend/marketplace/
├── app.py              # Flask 应用主入口 (Web UI + API + 旧版兼容)
├── app_changelog.py    # 更新日志管理模块
├── app_releases.py     # 应用版本发布管理
├── catalog_view_models.py  # 插件目录视图模型
├── config.json         # 配置文件
├── download_stats.py   # 下载统计模块
├── feedback_service.py # 用户反馈服务
├── marketplace.db      # SQLite 数据库 (自动创建，gitignored)
├── marketplace_services.py # 市场数据服务
├── package_publish.py  # 包发布验证和处理
├── page_contexts.py    # 页面上下文构建
├── plugin_marketplace.py   # 插件市场核心逻辑
├── plugin_queries.py   # 插件查询接口
├── requirements.txt    # Python 依赖
├── runtime_health.py   # 运行时健康检查
├── storage_browser.py  # 存储浏览器
├── storage_paths.py    # 存储路径管理
├── storage_uploads.py  # 上传处理
├── update_retention.py # 更新包保留策略
├── static/             # 静态资源
└── templates/          # Jinja2 模板文件
    ├── base.html
    ├── index.html
    ├── plugins.html
    ├── plugin_detail.html
    ├── upload.html
    └── browse.html
```

## 安装和运行

### 环境要求

- Python 3.9+
- pip

### 安装依赖

```bash
cd Backend/marketplace
pip install -r requirements.txt
```

### 配置文件

编辑 `config.json`：

```json
{
    "storage_path": "H:\\ColorVision",
    "host": "0.0.0.0",
    "port": 9998,
    "debug": false,
    "secret_key": "your-secret-key",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "upload_auth": {
        "username": "admin",
        "password": "admin"
    }
}
```

配置项说明：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `storage_path` | 插件和应用的存储路径 | `storage/` |
| `host` | 监听地址 | `0.0.0.0` |
| `port` | 监听端口 | `9998` |
| `debug` | 调试模式 | `false` |
| `secret_key` | Flask 密钥 | 需修改 |
| `upload_auth` | 上传认证凭据 | 需修改 |

### 启动服务

```bash
# 使用默认配置
python app.py

# 指定存储路径
python app.py --storage H:\ColorVision

# 指定端口
python app.py --port 9999
```

## API 接口

### Web UI 路由

| 路由 | 功能 |
|------|------|
| `GET /` | 首页 — 存储概览、快速链接 |
| `GET /plugins` | 插件市场 — 搜索、分类、排序 |
| `GET /plugins/{id}` | 插件详情 — 版本列表、README、下载 |
| `GET /upload` | 上传页面 |
| `POST /upload` | 处理上传 |
| `GET /browse[/path]` | 文件浏览器 |
| `GET /releases` | 发布版本列表 |
| `GET /updates` | 更新包列表 |
| `GET /tools` | 工具下载列表 |

### REST API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plugins` | 搜索插件（keyword, category, sort, pagination） |
| GET | `/api/plugins/{id}` | 插件详情 + 所有版本 |
| GET | `/api/plugins/{id}/latest-version` | 纯文本最新版本 |
| POST | `/api/plugins/batch-version-check` | 批量版本检查 |
| GET | `/api/plugins/categories` | 获取所有分类 |
| GET | `/api/packages/{id}/{version}` | 下载插件包 |
| POST | `/api/packages/publish` | 发布新版本（需 Basic Auth） |
| GET | `/api/stats` | 下载统计 |
| GET | `/api/health` | 健康检查端点 |
| GET | `/api/ready` | 就绪检查端点 |

### 旧版兼容路由

| 路由模式 | 说明 |
|----------|------|
| `PUT /upload/{path}` | 兼容旧构建脚本上传 |
| `/D%3A/ColorVision/Plugins/{path}` | 兼容旧客户端版本检查和下载 |

## 认证

上传接口使用 HTTP Basic Auth 进行保护：

```bash
# 使用 curl 示例
curl -u username:password -X POST http://localhost:9998/api/packages/publish \
  -F "PluginId=Spectrum" \
  -F "Version=1.0.0.1" \
  -F "package=@Spectrum-1.0.0.1.cvxp"
```

## 存储结构

后端直接使用现有的文件系统结构：

```
{storage_path}/
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
│   └── ...
└── Tool/                       # 工具下载
```

## 测试

后端包含完整的测试套件：

```bash
# 运行所有测试
python -m pytest

# 运行特定测试文件
python test_app.py
python test_app_releases.py
python test_page_contexts.py
python test_upload_services.py
```

## 与构建脚本集成

后端与 `Scripts/` 目录下的构建脚本集成：

- `publish_plugin.py` - 使用 `/api/packages/publish` 发布插件
- `build.py` - 上传主程序安装包
- `build_update.py` - 上传增量更新包
- `build_spectrum.py` - 上传 Spectrum 插件

## 技术栈

| 层次 | 选型 | 版本 |
|------|------|------|
| 语言 | Python | 3.9+ |
| 框架 | Flask | >=3.0 |
| 模板引擎 | Jinja2 | 内置 |
| CSS 框架 | Bootstrap 5 | 5.x |
| 数据库 | SQLite | 内置 |
| Markdown 渲染 | markdown | >=3.8 |

## 访问地址

服务启动后：

- Web UI: http://localhost:9998
- 插件市场: http://localhost:9998/plugins
- API: http://localhost:9998/api/plugins
- 文件浏览: http://localhost:9998/browse

## 部署建议

### 生产环境部署

1. **使用 Gunicorn/uWSGI**

```bash
pip install gunicorn
gunicorn -w 4 -b 0.0.0.0:9998 app:app
```

2. **Nginx 反向代理**

```nginx
server {
    listen 80;
    server_name marketplace.example.com;

    location / {
        proxy_pass http://localhost:9998;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

3. **启用 HTTPS**

使用 Let's Encrypt 或自签名证书启用 HTTPS。

4. **监控和日志**

- 配置日志轮转
- 设置监控告警
- 定期检查磁盘空间

## 故障排查

### 服务无法启动

检查端口是否被占用：
```bash
netstat -an | findstr 9998
```

### 上传失败

- 确认 `upload_auth` 配置正确
- 检查存储路径权限
- 查看日志错误信息

### 数据库错误

删除自动生成的 `marketplace.db` 文件，重启服务后会自动重建。
