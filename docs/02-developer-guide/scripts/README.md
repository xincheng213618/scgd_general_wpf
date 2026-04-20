# 构建与发布脚本

ColorVision 项目包含一组 Python 脚本，用于构建应用程序、打包插件、发布更新和管理后端上传。

## 脚本概览

| 脚本 | 功能 |
|------|------|
| `build.py` | 构建主程序安装包并发布 |
| `build_update.py` | 构建增量更新包 |
| `build_plugin.py` | 构建通用插件包 (.cvxp) |
| `generate_shared_files.py` | 扫描宿主输出目录生成 `shared_files.json` |
| `package_cvxp.py` | 基于 `shared_files.json` 剥离并打包/上传插件 |
| `package_plugin.bat` | 仓库内插件一键构建并调用 `package_cvxp.py` |
| `build_spectrum.py` | 构建 Spectrum 插件 |
| `publish_plugin.py` | 发布插件到市场后端 |
| `backend_client.py` | 后端上传共享模块 |
| `file_manager.py` | 文件管理工具 |

## 环境配置

### 认证配置

脚本使用以下环境变量进行后端认证：

```powershell
# PowerShell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "xincheng"
$env:COLORVISION_UPLOAD_PASSWORD = "xincheng"
```

```bash
# Bash (Git Bash/WSL)
export COLORVISION_UPLOAD_URL="http://xc213618.ddns.me:9998"
export COLORVISION_UPLOAD_USERNAME="xincheng"
export COLORVISION_UPLOAD_PASSWORD="xincheng"
```

::: tip
如果不设置环境变量，脚本将使用默认凭据 `xincheng/xincheng`。
:::

### 可选配置

| 环境变量 | 说明 | 默认值 |
|----------|------|--------|
| `COLORVISION_UPLOAD_URL` | 后端上传地址 | `http://xc213618.ddns.me:9998` |
| `COLORVISION_UPLOAD_FOLDER` | 上传文件夹 | `ColorVision` |
| `COLORVISION_UPLOAD_USERNAME` | 上传用户名 | `xincheng` |
| `COLORVISION_UPLOAD_PASSWORD` | 上传密码 | `xincheng` |
| `COLORVISION_REMOTE_UPLOAD` | 是否启用远程上传 | `1` (启用) |

## build.py - 主程序构建

构建主程序安装包并上传到后端。

### 用法

```powershell
# 完整构建（编译 + 打包 + 上传）
py Scripts\build.py

# 跳过构建，仅上传最新安装包
py Scripts\build.py --skip-build

# 跳过远程上传
py Scripts\build.py --skip-remote-upload
```

### 功能说明

1. 使用 MSBuild 编译解决方案
2. 使用 Advanced Installer 构建安装包
3. 执行后端预检（`/api/health` + `/api/ready`）
4. 上传安装包到后端

### 前置要求

- Visual Studio 2022+ (MSBuild)
- Advanced Installer
- Python 依赖：`requests`, `tqdm`

## build_update.py - 增量更新构建

创建增量更新包（仅包含变更文件）。

### 用法

```powershell
py Scripts\build_update.py
```

### 工作原理

1. 读取 `ColorVision.exe` 获取当前版本
2. 查找历史版本作为基线
3. 对比文件差异生成增量包
4. 上传增量包到 `Update/` 目录

### 输出文件

- `{History}/ColorVision-[{version}].zip` - 完整包
- `{History}/update/ColorVision-Update-[{version}].cvx` - 增量包

## build_plugin.py - 插件构建

通用插件打包工具。

### 用法

```powershell
# 第一次双击/空参数运行：生成配置文件和共享文件表模板
py Scripts\build_plugin.py

# 使用配置文件打包
py Scripts\build_plugin.py

# 外部插件作者：命令行直接传参，生成 cvxp 但不上传
py Scripts\build_plugin.py `
    --project-file C:\src\MyPlugin\MyPlugin.csproj `
    --configuration Release `
    --framework net8.0-windows `
    --no-upload
```

### 参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `-p, --project_name` | 仓库内项目名称 | 自动推断 / `ProjectBase` |
| `-t, --type` | 仓库内项目类型目录 | `Plugins` |
| `--project-file` | 外部插件项目的 `.csproj` 路径 | 空 |
| `--src-dir` | 已编译输出目录 | 自动推断 |
| `--assets-file` | `obj/project.assets.json` 路径 | 自动推断 |
| `--shared-files-manifest` | 宿主共享文件表路径 | `build_plugin.shared_files.json` |
| `-c, --configuration` | 构建配置 | `Release` |
| `-f, --framework` | 目标框架 | `net10.0-windows` |
| `--output-dir` | `.cvxp` 输出目录 | 当前目录 |
| `--config` | 打包配置文件路径 | `Scripts/build_plugin.config.json` |
| `--strip-mode` | 剥离模式：`auto` / `manifest` / `assets` / `none` | `auto` |
| `--shared-prefix` | 追加共享依赖前缀 | 空 |
| `--shared-package` | 追加共享依赖精确包名 | 空 |
| `--no-upload` | 只打包，不走上传 | 关闭 |
| `--upload` | 强制上传，即使配置文件里关闭上传 | 关闭 |
| `--keep-package` | 上传后保留本地 `.cvxp` | 关闭 |

### 打包逻辑

1. 无参数首次运行时，生成 `build_plugin.config.json` 和 `build_plugin.shared_files.json`
2. 后续优先读取 `build_plugin.shared_files.json` 作为共享文件表
3. 如果共享文件表不存在，再从 `obj/project.assets.json` 生成共享文件表
4. 从 `projectFileDependencyGroups` 中找出共享根依赖，默认识别 `ColorVision.*`、`cvColorVision`、`FlowEngineLib`、`ST.Library.UI`
5. 递归收集这些共享依赖的传递依赖，并写入共享文件表
6. 打包时根据共享文件表生成 `stripped_files.json`
7. 排除 `.pdb` 文件，保留插件自身文件和额外元数据文件
8. 生成 `{PluginName}-{version}.cvxp` 包
9. 未指定 `--no-upload` 时，继续走上传流程

### 模式说明

- `auto`: 优先使用共享文件表；若不存在则从 `project.assets.json` 生成；两者都没有时不剥离共享依赖
- `manifest`: 直接按共享文件表剥离共享依赖
- `assets`: 从 `project.assets.json` 重新生成共享文件表并立即打包
- `none`: 不剥离共享依赖，完整打包输出目录

### 推荐流程

- 双击脚本：先生成配置文件模板，填好 `project_file` 或 `src_dir` 后再次运行即可
- 外部插件作者：建议保留 `upload: false`，先生成 `.cvxp`，再调用 `publish_plugin.py` 走插件市场 API
- 如果插件显式依赖了额外宿主共享包，可以通过配置文件或 `--shared-prefix`、`--shared-package` 扩展识别规则

## generate_shared_files.py - 共享文件表生成

扫描宿主程序输出目录，生成 `shared_files.json`。

### 用法

```powershell
py Scripts\generate_shared_files.py

py Scripts\generate_shared_files.py `
    --root-dir C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Release\net10.0-windows `
    --output C:\temp\shared_files.json
```

### 输出内容

- `generated_at`: 生成时间
- `shared_files`: 宿主目录下的全部相对文件路径

### 过滤规则

- 自动忽略 `Plugins` 目录
- 自动忽略 `Log` 目录
- 通常只需要在宿主共享文件发生变化后重新生成一次

## package_cvxp.py - 单文件打包上传

单文件脚本，读取 `shared_files.json`，剔除共享文件和 `.pdb` 后生成 `.cvxp`，并可直接上传。

### 用法

```powershell
# 仅本地打包
py Scripts\package_cvxp.py --project-file Plugins\Pattern\Pattern.csproj --build --no-upload

# 指定编译输出目录
py Scripts\package_cvxp.py `
    --src-dir Plugins\Pattern\bin\x64\Release\net10.0-windows `
    --plugin-root Plugins\Pattern
```

### 参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--src-dir` | 插件编译输出目录 | 空 |
| `--project-file` | 插件 `.csproj` 路径 | 空 |
| `--plugin-root` | 插件根目录，用于补充 `README.md` 等额外文件 | 自动推断 |
| `--plugin-name` | 插件名称 | 自动推断 |
| `--shared-files` | `shared_files.json` 路径；不传时优先读取脚本同目录文件 | 自动查找 |
| `--output-dir` | `.cvxp` 输出目录 | `Scripts/` |
| `--build` | 打包前先执行 `dotnet build` | 关闭 |
| `--dotnet` | `--build` 使用的 `dotnet` 命令 | `dotnet` |
| `--no-upload` | 只打包不上传 | 关闭 |
| `--keep-package` | 上传后保留本地包 | 关闭 |

### 打包逻辑

1. 读取 `shared_files.json`
2. 遍历插件输出目录
3. 过滤所有 `.pdb` 文件
4. 过滤所有存在于 `shared_files.json` 中的共享文件
5. 写入 `stripped_files.json`
6. 打包为 `.cvxp`
7. 未指定 `--no-upload` 时上传包和 `LATEST_RELEASE`

## package_plugin.bat - 仓库内插件快捷入口

这个批处理只给仓库内插件项目使用。它会自动定位 `.venv`、自动调用 `package_cvxp.py --build`，因此各插件目录下的 `.bat` 文件可以只保留一行转发。

### 用法

```powershell
Scripts\package_plugin.bat Pattern --no-upload
```

## build_spectrum.py - Spectrum 插件构建

专门为 Spectrum 插件优化的构建脚本。

### 用法

```powershell
# 构建并上传
py Scripts\build_spectrum.py --upload

# 仅构建不上传
py Scripts\build_spectrum.py
```

### 特性

- 支持 .zip 和 .cvxp 两种格式输出
- .cvxp 包复制到映射的插件服务器路径
- .zip 包使用认证上传

## publish_plugin.py - 插件发布

通过 API 发布插件包到插件市场。

### 用法

```powershell
# 基本发布
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp

# 完整参数
py Scripts\publish_plugin.py `
  -p Spectrum `
  -v 1.0.0.1 `
  -f Spectrum-1.0.0.1.cvxp `
  -n "Spectrum Plugin" `
  -d "光谱分析插件" `
  -a "Author Name" `
  -c "Analysis" `
  --changelog CHANGELOG.md `
  --icon PackageIcon.png

# 指定后端地址
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://localhost:9999
```

### 参数

| 参数 | 说明 | 必需 |
|------|------|------|
| `-p, --plugin-id` | 插件唯一 ID | 是 |
| `-v, --version` | 版本号 (如 1.0.0.1) | 是 |
| `-f, --file` | 包文件路径 | 是 |
| `-n, --name` | 显示名称 | 否 |
| `-d, --description` | 描述 | 否 |
| `-a, --author` | 作者 | 否 |
| `-c, --category` | 分类 | 否 |
| `-r, --requires` | 最低引擎版本 | 否 |
| `--changelog` | 更新日志文件或文本 | 否 |
| `--icon` | 图标文件路径 | 否 |
| `--api-url` | 后端地址 | 否 |
| `--username` | 用户名 | 否 |
| `--password` | 密码 | 否 |

### 认证

发布接口需要 Basic Auth 认证：

```powershell
# 方式1: 环境变量
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

# 方式2: 命令行参数
py Scripts\publish_plugin.py ... --username your-user --password your-password
```

## backend_client.py - 后端客户端

共享的后端上传模块，为其他脚本提供认证和上传功能。

### 主要功能

- 认证凭据解析（环境变量 -> 默认值）
- 上传 URL 构建
- 后端预检（健康检查 + 就绪检查）
- 流式 PUT 上传
- 认证 multipart POST

### 使用示例

```python
from backend_client import (
    RemoteUploadSettings,
    preflight_remote_upload,
    upload_file,
    resolve_upload_credentials,
)

# 解析凭据
username, password = resolve_upload_credentials()

# 配置上传设置
settings = RemoteUploadSettings(
    base_url="http://localhost:9998",
    folder_name="Plugins/MyPlugin",
    username=username,
    password=password,
)

# 预检
if preflight_remote_upload(settings):
    # 上传文件
    upload_file(settings, "path/to/file.cvxp")
```

### 预检逻辑

上传前会进行两步检查：

1. **健康检查** (`GET /api/health`) - 确认后端服务可用
2. **就绪检查** (`GET /api/ready`) - 确认后端已准备好接收上传

如果后端返回 404（旧版本后端），则视为兼容模式继续上传。

## file_manager.py - 文件管理

文件管理工具类。

### 功能

- 文件上传管理
- 路径处理
- 进度显示

### 使用示例

```python
from file_manager import FileManager

fm = FileManager()

# 上传文件
fm.upload_file("path/to/file.zip", "ColorVision/Update")
```

## 脚本测试

每个脚本都有对应的测试文件：

| 测试文件 | 说明 |
|----------|------|
| `test_backend_client.py` | 后端客户端测试 |
| `test_build.py` | 构建脚本测试 |
| `test_file_manager.py` | 文件管理测试 |
| `test_build_update.py` | 更新构建测试 |
| `test_publish_plugin.py` | 插件发布测试 |

### 运行测试

```powershell
# 运行单个测试
python Scripts\test_backend_client.py

# 使用 pytest
pytest Scripts\test_*.py -v
```

## 故障排查

### 上传失败 (401 Unauthorized)

- 检查环境变量或默认凭据是否正确
- 确认后端 `config.json` 中的 `upload_auth` 配置

### 上传失败 (Connection Error)

- 检查后端服务是否运行
- 确认网络连接
- 验证 `COLORVISION_UPLOAD_URL` 配置

### 构建失败

- 确认 MSBuild 路径正确
- 检查 Advanced Installer 是否安装
- 验证解决方案是否能正常编译

### 版本号读取失败

- 确认目标 DLL/EXE 存在
- 检查文件版本信息是否正确嵌入

## 最佳实践

1. **使用环境变量** - 避免在脚本中硬编码敏感信息
2. **预检失败处理** - 脚本会在后端不可用时提供清晰的错误信息
3. **版本号管理** - 确保 DLL/EXE 的版本信息与发布版本一致
4. **测试先行** - 在正式发布前使用测试脚本验证功能
