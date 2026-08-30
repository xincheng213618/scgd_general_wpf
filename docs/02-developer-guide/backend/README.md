---
knowledge_id: "delivery.backend"
knowledge_type: "topic"
status: "current"
summary: "Flask后端的组成、配置、制品与数据库路径、认证和探测边界；--storage不隔离配置或SQLite。"
aliases: ["插件市场后端","上传401","Flask","marketplace.db","api/ready","create_app_and_context","RuntimeOverrides","AuthPolicy","Session权限","角色权限"]
code_paths: ["Web/Backend/app.py","Web/Backend/app_setup.py","Web/Backend/cli.py","Web/Backend/config_loader.py","Web/Backend/runtime_health.py","Web/Backend/routes/health_api.py","Web/Backend/routes/auth_adapters.py","Web/Backend/services/auth_policy.py","Web/Backend/services/auth_middleware.py","Web/Backend/services/permission_service.py","Web/Backend/marketplace_api_routes.py","Web/Backend/services/marketplace_api.py","Web/Backend/services/scheduler.py","Web/Backend/services/storage_events.py"]
test_paths: ["Web/Backend/test_app.py","Web/Backend/test_app_releases.py","Web/Backend/test_upload_services.py","Web/Backend/test_config_loader.py","Web/Backend/test_auth_policy.py"]
related: ["delivery.scripts","plugins.index","delivery.file-transfer","delivery.plugin-catalog","delivery.backend-accounts","delivery.backend-auth","delivery.artifact-delivery","delivery.backend-public-data","delivery.backend-observability","delivery.backend-jobs","delivery.backend-retention"]
---

# 插件市场后端

`Web/Backend/` 是插件市场、更新包分发和后台管理门户的 Flask 服务。本页负责组成、配置/路径、启动副作用与健康检查。查询与交付分别见[插件目录与索引](./plugin-catalog.md)、[公共站点读模型](./public-data.md)、[HTTP制品交付](./artifact-delivery.md)和[文件中转](./file-transfer.md)；身份见[账号生命周期](./accounts.md)与[HTTP认证/API key/CSRF](./authentication.md)；运行维护见[内置任务](./jobs.md)、[备份与保留](./backup-retention.md)、[访问及性能观测](./observability.md)。`Web/Backend/README.md` 保留模块运行前提以及尚未迁入主题的Copilot、Operations、反馈、部署历史、审计查询和独立CVWindowsService细节，不能用本页入口表代替这些规则。

## 代码责任

| 责任 | 当前入口 |
| --- | --- |
| 创建 Flask、数据库/缓存、服务与依赖容器 | `app_setup.py:create_app_and_context`；`app.py` 调用组成并保留兼容全局变量 |
| 命令参数与一次性操作 | `cli.py`；`app.py` 的主入口决定日志、调度器和监听启动 |
| 配置合并、探测 | `config_loader.py`、`runtime_health.py`、`routes/health_api.py` |
| 认证与授权 | `routes/auth_adapters.py` 负责 Flask 装饰器/响应，`services/auth_policy.py` 负责认证和 scope 判定，`services/permission_service.py` 负责角色权限 |
| 插件列表、详情、包下载和发布、兼容上传 | `marketplace_api_routes.py` 与 `services/marketplace_api.py`，由 `app_setup.py:register_all_blueprints` 装配 |
| 文件中转 | `routes/transfer.py`、`transfer_files.py`；不等同于插件包发布 |

`services/auth_middleware.py` 当前只是旧导入路径的兼容转发，不是独立的认证事实源。不能只按目录名寻找路由：市场核心路由仍在 Backend 根目录的 `marketplace_api_routes.py`。

## 先查什么

| 现象 | 优先看 |
| --- | --- |
| 服务起不来 | 端口、依赖、`config.json`、`storage_path` 权限 |
| `/api/ready` 失败 | 存储与 Plugins 目录、数据库 `SELECT 1`、`upload_auth` 是否非空 |
| 上传 401 | `upload_auth`、脚本环境变量、API key scope |
| 上传成功但市场看不到 | 索引刷新、目录结构、`manifest.json`、`LATEST_RELEASE` |
| 下载 404 | 文件路径、版本号、插件 id 大小写、保留策略 |
| 数据库异常 | `marketplace.db` 权限、schema 迁移、索引重建 |

## 启动与配置

下面分为联网安装依赖和本地启动两个动作。启动会写数据库/缓存、创建或修改制品和日志目录，并可能启动后台任务及监听端口，不是只读诊断。`--storage` 只替换制品根目录，**不隔离配置、账号或 SQLite 数据库**；不要把临时制品目录当成完整测试环境。需要隔离时，应先准备独立的后端副本及专用配置，确认实际数据库路径、监听地址和端口，再执行启动命令。默认 `host=0.0.0.0` 监听所有接口，临时存储路径不会把服务限制为本机访问。

```powershell
Push-Location Web\Backend
python -m pip install -r requirements.txt
$artifactStorage = Join-Path $env:TEMP 'ColorVisionBackend-Local'
# 仅覆盖制品目录；仍使用当前 Backend 的 config.json 与 marketplace.db
python app.py --storage $artifactStorage
Pop-Location
```

`config_loader.py` 固定读取其所在 Backend 目录的 `config.json`，覆盖 `DEFAULT_CONFIG`；`upload_auth` 和 `copilot_sync` 做子字典合并。没有文件时直接使用默认值，不会自动复制 `config.json.example`。示例配置可作为人工配置起点，但不能携带生产凭证进入测试环境。

`app_setup.py` 默认把数据库设为 Backend 目录下的 `marketplace.db`，而非 `{storage_path}/marketplace.db`。`app.py` 在模块导入时已经调用组成，初始化数据库、schema 和相关服务；随后注册路由及预热缓存。只有进入主入口后才解析 `--storage`、`--port`、`--debug`，并检查启动配置。因此 `--help`、一次性 CLI 操作或生产配置被拒绝前，也可能已经发生组成阶段写入；不要通过导入 `app` 来做无副作用探测。

CLI 在非 debug 模式发现默认 session 密钥、默认或空上传凭证等已实现的配置问题时以退出码 `2` 拒绝继续；debug 模式只警告。该检查不是完整安全认证，也不在组成前执行。`--storage` 不改变数据库路径；当前 CLI 没有通用的数据库路径覆盖参数。主入口才负责启动调度器，受 `scheduler_enabled` 与 debug reloader 条件控制，不能把导入 WSGI 应用等同于执行这一启动路径。

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

## 共用认证不是所有 Session 全权

`AuthPolicy.authorize` 区分管理员 Session、普通用户 Session、配置中的 Basic 凭证和 Bearer API key。普通用户 Session 只有在端点允许 `allow_user_session` 时参与授权，并实时读取角色权限；缺少要求的 scope 会拒绝。默认角色初始拥有较广权限，不代表今后不可撤销，也不能把“已登录”当成任意端点可访问。

市场上传装饰器要求 `plugin:publish`；Transfer 独立要求 `file:transfer`；配置 Basic 凭证在允许 Basic 的共用策略路径中取得管理员身份，Bearer 仍检查端点 scope 或 `admin:*`。端点可以限制认证方式，不能把共用策略推广为所有协议都接受 Session/Basic/Bearer。需要改密码的 Session 也不会凭登录状态绕过该限制；显式 Basic/Bearer 的处理仍以具体端点为准。凭据优先级、角色permission与可申请key scope的区别、CSRF分支及401/403详见[HTTP认证](./authentication.md)，账号状态和配置管理员例外见[账号生命周期](./accounts.md)。

## health 与 ready 的不同副作用

| 入口 | 实际行为 | 不能据此推断 |
| --- | --- | --- |
| `GET /api/health` | 返回 `status=ok`、时间、当前存储/数据库路径和 debug 状态 | 不探测数据库连接、目录可写性或索引完整性 |
| `GET /api/ready` | 尝试创建存储根和 `Plugins` 目录，检查目录/可写性，执行数据库 `SELECT 1`，检查上传账号密码非空；不满足时返回 `503` | 不是纯只读探针，不验证默认凭证强度、全部表/业务查询或制品完整性 |

`ready` 中的索引摘要是附加信息：索引未初始化或报告错误本身不加入 `issues`，不决定 `ready` 值。HTTP 方法名也不能代替副作用分析；Flask 自动 HEAD 可能进入相同 handler。诊断时先区分进程存活、基础依赖可用、索引状态和真实业务成功。

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
| 大文件传输 | [文件中转](./file-transfer.md)的整文件/断点上传及分享链，不负责发布插件 |
| 后台索引检查 | 主入口启动调度器后由相应 job 执行；不能把任意模块导入视为后台刷新完成 |
| 发布刷新 | storage event派发索引刷新；插件发布后刷新可best-effort失败，周期插件检查可触发全量刷新，详见[插件索引](./plugin-catalog.md) |

首次部署或大量手工改文件后，可在确认目标制品目录和实际 Backend 数据库后运行 `python app.py --storage <目标目录> --refresh-all-indexes`；该操作会改写当前 Backend 数据库内的索引，不会因为 `--storage` 自动创建一套隔离索引库，不能作为默认只读排查步骤。

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
Push-Location Web\Backend
python -m unittest test_app test_app_releases test_page_contexts test_upload_services
Pop-Location
```

改上传、索引、认证、发布接口或存储路径后，至少跑相关后端测试。构建脚本是发布入口，后端只接收和组织制品；`marketplace.db` 不是插件包内容来源，WPF 客户端行为不在后端文档里展开。

`test_config_loader.py` 覆盖配置默认值、合并和已实现的校验，`test_auth_policy.py` 覆盖认证方式、普通角色参与条件、scope 与改密限制；它们不是实际部署、完整浏览器链或生产权限审计的证明。测试路径表示关联证据，不表示已经执行；本地测试也须先核对测试自己的配置/数据库隔离方式，不能只看命令中的临时制品目录。
