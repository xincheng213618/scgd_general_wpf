---
knowledge_id: "delivery.backend"
knowledge_type: "topic"
status: "current"
summary: "Flask 后端的组成、配置、CLI 参数、管理入口与探测边界；--storage 不隔离配置或 SQLite，命令退出 0 仍须核对业务结果。"
aliases: ["插件市场后端","上传401","Flask","marketplace.db","api/ready","create_app_and_context","RuntimeOverrides","AuthPolicy","Session权限","角色权限","Backend CLI","命令行参数","--storage","--refresh-all-indexes","--reconcile-history","--reconcile-plugin-history","--prune-updates","--run-job","--create-api-key","cache/status","cache/cleanup","--refresh-index","--refresh-plugin-index","--cleanup-cache","--scopes","--port","--debug","api/stats","后台管理页面"]
code_paths: ["Web/Backend/app.py","Web/Backend/app_setup.py","Web/Backend/cli.py","Web/Backend/config_loader.py","Web/Backend/runtime_health.py","Web/Backend/routes/health_api.py","Web/Backend/routes/auth_adapters.py","Web/Backend/services/auth_policy.py","Web/Backend/services/auth_middleware.py","Web/Backend/services/permission_service.py","Web/Backend/marketplace_api_routes.py","Web/Backend/services/marketplace_api.py","Web/Backend/services/scheduler.py","Web/Backend/services/storage_events.py","Web/Backend/marketplace_services.py","Web/Backend/app_releases.py","Web/Backend/plugin_marketplace.py","Web/Backend/update_retention.py","Web/Backend/routes/admin_api.py","Web/Backend/db_cache.py","Web/Backend/services/artifact_index.py","Web/Backend/routes/public_api.py","Web/Frontend/src/App.tsx"]
test_paths: ["Web/Backend/test_app.py","Web/Backend/test_app_releases.py","Web/Backend/test_upload_services.py","Web/Backend/test_config_loader.py","Web/Backend/test_auth_policy.py","Web/Backend/test_artifact_index.py"]
related: ["delivery.scripts","plugins.index","delivery.file-transfer","delivery.plugin-catalog","delivery.backend-accounts","delivery.backend-auth","delivery.artifact-delivery","delivery.backend-public-data","delivery.backend-observability","delivery.backend-jobs","delivery.backend-retention","delivery.backend-records","delivery.backend-feedback","delivery.backend-copilot-sync","delivery.backend-operations","delivery.cvwindowsservice","platform.web-architecture","delivery.web-deployment","delivery.web-pages"]
---

# 插件市场后端

`Web/Backend/` 是插件市场、更新包分发和后台管理门户的 Flask 服务。本页负责组成、配置/路径、启动副作用与健康检查。查询与交付分别见[插件目录与索引](./plugin-catalog.md)、[公共站点读模型](./public-data.md)、[HTTP制品交付](./artifact-delivery.md)和[文件中转](./file-transfer.md)；身份见[账号生命周期](./accounts.md)与[HTTP认证/API key/CSRF](./authentication.md)；运行维护见[内置任务](./jobs.md)、[备份与保留](./backup-retention.md)、[访问及性能观测](./observability.md)和[审计/部署记录](./management-records.md)。[反馈收件箱](./feedback.md)、[Copilot后端配置交付](./copilot-sync.md)、[Operations中继](./operations-relay.md)、[CVWindowsService服务包发布](./cvwindowsservice.md)各有独立契约。`Web/Backend/README.md` 保留模块运行前提、代码入口及风险提示，详细规则只在对应主题维护。

跨模块依赖、现有持久化接口和演进约束见[Web 架构](../../03-architecture/components/web.md)；本地启动脚本、NAS 更新步骤、备份和失败恢复见[Web 本地启动与 NAS 部署](../deployment/web.md)。React/VitePress 路由、压缩与缓存、文档状态和索引见[Web 页面与文档托管](./web-pages.md)。

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

## 常见问题定位

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
| `storage_path` | 插件包、安装包、更新包和工具文件根目录 |
| `host` / `port` / `debug` | Flask 运行参数 |
| `secret_key` | Web session 密钥，生产环境必须改 |
| `upload_auth` | 构建脚本上传和后台接口 Basic Auth |
| `transfer_upload_dir` | 大文件传输目录，默认相对 `storage_path` |
| `app_release_keep_count` / `plugin_package_keep_count` | 主程序和插件历史包保留数量 |

配置包含上传凭据、会话密钥和可能需要交付给客户端的服务密钥，应按部署环境单独管理。

## 命令行参数

从 `Web/Backend/` 运行 `python .\app.py <参数>`。解析入口是 `cli.build_parser`；主入口先组成应用，再解析参数、预热版本缓存并调用 `handle_cli_args`。因此查看 `--help` 或执行一次性命令，也会经历前述数据库/缓存初始化边界。

| 启动参数 | 当前行为 |
| --- | --- |
| `--storage <目录>` | 覆盖制品根；相对路径按命令工作目录解析，不改变 Backend 的配置或数据库路径 |
| `--port <整数>` | 覆盖配置端口；默认配置为 9998，传入 0 时不覆盖 |
| `--debug` | 将 debug 设为 true；未传入时沿用配置，不能用此开关强制关闭配置中的 debug |
| `--help` / `-h` | 输出 argparse 帮助并退出；不启动监听，但应用组成已发生 |

下列一次性命令会写入数据库或制品目录；使用前确认目标和操作授权，并保留所需恢复副本。每次只指定一个操作。多个操作同时出现时，当前实现按表中顺序执行第一个，然后退出；不是组合事务。

| 一次性参数 | 作用与输出 |
| --- | --- |
| `--reconcile-history` | 按 `app_release_keep_count`（默认 5）整理主程序根目录候选包到 History，输出已处理的文件映射 |
| `--reconcile-plugin-history` | 按 `plugin_package_keep_count`（默认 3）把插件当前包整理到 History/Plugins，输出插件/文件数量 |
| `--prune-updates` | 先修复旧更新目录布局，再删除不保留的规范 `.cvx`；保留全局最新版本和版本第四段为 1 的包，输出 retained/deleted |
| `--refresh-index` | 刷新全部插件索引，输出 indexed/deleted/duration/errors；errors 非空仍可能退出 0 |
| `--refresh-plugin-index <PluginId>` | 刷新单插件索引；“Plugin not found”也按已处理命令退出 0 |
| `--refresh-all-indexes` | 依次刷新 plugins、releases、updates、tools，输出各作用域摘要；不包含独立 docs 索引 |
| `--cleanup-cache` | 删除到期缓存条目，输出数量 |
| `--run-job <JobId>` | 同步执行一个已登记任务，输出 status/duration/summary/error；具体写入或清理由[任务契约](./jobs.md)决定 |
| `--create-api-key <名称>` | 在本地数据库创建 key，向终端输出完整明文；它直接调用服务，不经过 HTTP Session/Bearer 鉴权 |
| `--scopes <scope1,scope2>` | 仅配合创建 key；省略时默认 `admin:*`。按允许列表验证，非法项退出 1；应显式给出所需最小范围，语义见[API key](./authentication.md) |

归档不是单纯复制：可移动原文件，也可删除被识别为已有归档副本的源文件。插件归档的同名去重仅比较文件大小；不能把归档数量当成逐字节完整性验证。更新清理可能忽略单个删除错误，实际文件仍须按输出核对。未识别的更新文件不进入规范包删除集合。

这些命令没有统一的“业务成功退出码”：完成分支通常退出 0，任务结果仍可能为 failed，索引也可能只部分刷新。读完具体摘要/错误后，再核对目标状态；未捕获异常仍会终止命令。CLI 的非 debug 配置校验失败退出 2。没有一次性操作时才继续注册内置任务、按配置启动调度线程并监听服务。

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
| `GET /api/stats` | 插件下载统计；与 HTTP/SPA 访问统计分开，完成计数语义见[制品交付](./artifact-delivery.md) |

## 管理页面入口

以下是 React `App.tsx` 注册的页面地址；页面可打开与具体 API 可执行仍由[认证与权限](./authentication.md)分别决定。

| 地址 | 用途与说明 |
| --- | --- |
| `/admin` | 管理概览 |
| `/admin/publish` | 主程序/插件等制品发布入口；交付见[制品接口](./artifact-delivery.md) |
| `/admin/files` | 管理存储文件，目录与路径规则见[公共存储](./public-data.md) |
| `/admin/cache` | 缓存与索引 |
| `/admin/jobs` | [内置任务](./jobs.md) |
| `/admin/deployments` | [部署历史](./management-records.md) |
| `/admin/operations/hosts` | [终端运维](./operations-relay.md) |
| `/admin/feedback` | [反馈收件箱](./feedback.md) |
| `/admin/users` / `/admin/login-security` | [账号管理与账号安全](./accounts.md) |
| `/admin/permissions` | [角色权限](./authentication.md) |
| `/admin/api-keys` | [API key](./authentication.md) |
| `/admin/copilot` | [Copilot 配置交付](./copilot-sync.md) |
| `/admin/audit` | [审计日志](./management-records.md) |
| `/admin/traffic` | [HTTP/SPA 访问与体验统计](./observability.md) |
| `/admin/settings` | 浏览器外观、[注册策略](./accounts.md)和[六项保留设置](./backup-retention.md)；受保护或需重启的配置仍由部署配置管理 |

## 缓存管理接口

这些接口由 `routes/admin_api.py` 提供，认证方式与 permission/scope 判定见[HTTP 认证](./authentication.md)。

| 接口 | 所需范围 | 结果 |
| --- | --- | --- |
| `GET /api/admin/cache/status` | `cache:read` | 数据库状态、storage_path、Plugins 目录存在性与插件目录缓存摘要 |
| `POST /api/admin/cache/cleanup` | `cache:refresh` | 删除过期缓存后写审计，返回 `deleted_count` |
| `GET /api/admin/index/status` | `cache:read` | 各索引的数量、状态、耗时和错误；刷新行为见[插件索引](./plugin-catalog.md)与[公共读模型](./public-data.md) |

cache/status 包含实际数据库/存储路径；数据库状态读取失败可在 200 响应中携带 `error`。`cleanup_expired_cache` 捕获异常、打印后返回 0，因此 `deleted_count = 0` 既可能没有过期项，也可能清理失败。审计写入失败另行打印且不抛出，200 不证明审计已落盘。

## 测试与边界

```powershell
Push-Location Web\Backend
python -m unittest test_app test_app_releases test_page_contexts test_upload_services
Pop-Location
```

`test_artifact_index.py` 覆盖部分缓存接口认证及权限；当前未登记 CLI 解析和全部一次性分支的专门测试，命令行参考按 `cli.py` 与所调用服务核对。改上传、索引、认证、发布接口或存储路径后，至少跑相关后端测试。构建脚本是发布入口，后端只接收和组织制品；`marketplace.db` 不是插件包内容来源，WPF 客户端行为不在后端文档里展开。

`test_config_loader.py` 覆盖配置默认值、合并和已实现的校验，`test_auth_policy.py` 覆盖认证方式、普通角色参与条件、scope 与改密限制；它们不是实际部署、完整浏览器链或生产权限审计的证明。测试路径表示关联证据，不表示已经执行；本地测试也须先核对测试自己的配置/数据库隔离方式，不能只看命令中的临时制品目录。
