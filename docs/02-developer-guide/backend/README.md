# 插件市场后端

`Web/Backend/` 是插件市场、更新包分发和后台管理门户的 Flask 服务。维护时先看启动、配置、存储目录、上传认证、索引刷新和故障分流。

## 先查什么

| 现象 | 优先看 |
| --- | --- |
| 服务起不来 | 端口、依赖、`config.json`、`storage_path` 权限 |
| `/api/ready` 失败 | `upload_auth`、存储目录可写性 |
| 上传 401 | `upload_auth`、脚本环境变量、API key scope |
| 上传成功但市场看不到 | 索引刷新、目录结构、`manifest.json`、`LATEST_RELEASE` |
| 下载 404 | 文件路径、版本号、插件 id 大小写、保留策略 |
| 数据库异常 | `marketplace.db` 权限、schema 迁移、索引重建 |

## 启动与配置

```powershell
cd Web\Backend
pip install -r requirements.txt
python app.py
```

配置优先看 `Web/Backend/config.json`，没有时复制 `config.json.example`。

| 项 | 说明 |
| --- | --- |
| `--storage H:\ColorVision` / `storage_path` | 插件包、安装包、更新包和工具文件根目录 |
| `--port 9999` / `host` / `port` / `debug` | Flask 运行参数 |
| `--refresh-all-indexes` / `--refresh-plugin-index Spectrum` | 重建全部或单个插件索引 |
| `secret_key` | Web session 密钥，生产环境必须改 |
| `upload_auth` | 构建脚本上传和后台接口 Basic Auth |
| `transfer_upload_dir` | 大文件传输目录，默认相对 `storage_path` |
| `app_release_keep_count` / `plugin_package_keep_count` | 主程序和插件历史包保留数量 |

不要在公开文档里写真实账号、密码或 API key。

## 存储模型

制品来源是文件系统，`marketplace.db` 只保存索引、缓存、统计、用户、API key、审计和任务历史。

```text
{storage_path}/
  LATEST_RELEASE
  CHANGELOG.md
  History/
  Update/
  Plugins/<PluginId>/
    LATEST_RELEASE
    manifest.json
    README.md
    CHANGELOG.md
    <PluginId>-<version>.cvxp
  Tool/
  Transfer/
```

## 发布与索引

| 场景 | 入口或行为 |
| --- | --- |
| 主程序发布 | `Scripts\release.bat` -> `/upload/...` |
| 插件包发布 | `Scripts\package_plugin.bat <PluginName>` 或 `package_cvxp.py` |
| API 发布插件 | `POST /api/packages/publish` |
| 后台管理 | `/admin` |
| 大文件传输 | `/api/transfer/files/<filename>` |
| 启动刷新 | 索引为空时后台刷新；已有索引时做签名检查 |
| 发布刷新 | 插件发布、`/upload/...` 或目录签名变化后刷新对应索引 |

首次部署或大量手工改文件后，运行 `python app.py --refresh-all-indexes`。

## 常用页面和接口

| 接口 | 用途 |
| --- | --- |
| `/` / `/plugins` / `/admin` / `/browse` | 首页、插件市场、后台管理和存储浏览 |
| `GET /api/plugins` / `GET /api/plugins/<id>` | 插件列表、搜索和详情 |
| `POST /api/plugins/batch-version-check` | 客户端批量版本检查 |
| `GET /api/packages/<id>/<version>` | 下载插件包 |
| `POST /api/packages/publish` | 发布插件包 |
| `PUT /upload/<path>` | 构建脚本兼容上传 |
| `GET /api/health` / `GET /api/ready` | 健康和就绪检查 |

## 测试与边界

```powershell
cd Web\Backend
python test_app.py
python test_app_releases.py
python test_page_contexts.py
python test_upload_services.py
python -m pytest
```

改上传、索引、认证、发布接口或存储路径后，至少跑相关后端测试。构建脚本是发布入口，后端只接收和组织制品；`marketplace.db` 不是插件包内容来源，WPF 客户端行为不在后端文档里展开。
