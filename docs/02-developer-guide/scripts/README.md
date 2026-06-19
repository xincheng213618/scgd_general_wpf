# 构建与发布脚本

ColorVision 项目包含一组 Python 脚本，用于构建应用程序、打包插件、发布更新和管理后端上传。

## 脚本概览

| 脚本 | 功能 |
|------|------|
| `build.py` | 构建主程序安装包并发布 |
| `build_update.py` | 构建增量更新包 |
| `build_plugin.py` | 兼容入口，内部转发到 `package_cvxp.py` |
| `generate_shared_files.py` | 扫描宿主输出目录生成 `shared_files.json` |
| `package_cvxp.py` | 基于 `shared_files.json` 剥离并打包/上传插件 |
| `package_plugin.bat` | 仓库内插件一键构建并调用 `package_cvxp.py` |
| `package_project.bat` | 仓库内项目一键构建并调用 `package_cvxp.py` |
| `package_cvxp_demo.bat` | 给外部插件作者的最小打包示例 |
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

### 主程序正式发布流程

正式发布只有一个入口：

```powershell
Scripts\release.bat
```

它会依次执行 `py Scripts\build.py` 和 `py Scripts\build_update.py`，完成主安装包构建上传、`LATEST_RELEASE` 更新、增量更新包构建上传和全量 zip 生成。

发布前只需要确认仓库根目录 `Directory.Build.props` 里的版本号已经提升：

```xml
<VersionPrefix>1.4.10.5</VersionPrefix>
```

`release.bat` 正常发布时本来就会生成本地文件，这不是“只本地打包”。判断是否真的发布，看输出里是否出现：

- `Uploading primary release package`
- `File uploaded successfully`
- `Uploaded LATEST_RELEASE successfully`
- 更新包上传的 `File uploaded successfully`

正式发布不要拆开手工执行 `build.py` 或 `build_update.py`，也不要使用任何跳过构建、跳过上传的调试参数。

发布成功后会有这些本地产物：

- `%USERPROFILE%\Documents\Advanced Installer\Projects\ColorVision\Setup Files\ColorVision-{version}.exe`
- `%USERPROFILE%\Desktop\History\ColorVision-[{version}].zip`
- `%USERPROFILE%\Desktop\History\update\ColorVision-Update-[{version}].cvx`

### 用法

```powershell
# release.bat 的内部步骤；正式发布不要绕过 release.bat 直接运行
py Scripts\build.py
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

## build_plugin.py - 兼容入口

旧的打包实现已经移除。

当前 `build_plugin.py` 只保留为兼容入口，会将常见仓库内调用转发到 `package_cvxp.py`，并输出迁移提示。新脚本不要再以它作为主入口。

### 用法

```powershell
py Scripts\build_plugin.py -t Projects -p ProjectARVR --no-upload
```

### 推荐替代

- 仓库内插件：`Scripts\package_plugin.bat Pattern --no-upload`
- 仓库内项目：`Scripts\package_project.bat ProjectARVR --no-upload`
- 仓库外部：`py Scripts\package_cvxp.py --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows --no-upload`

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

# 仅传编译输出目录，自动推断插件根目录
py Scripts\package_cvxp.py `
    --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows `
    --no-upload
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

### 直接传输出目录

当 `--src-dir` 指向类似 `PluginName/bin/x64/Release/net10.0-windows` 或 `PluginName/bin/Release/net10.0-windows` 的目录时，脚本会自动把 `PluginName` 目录识别为 `plugin_root`，这样即使不传 `--plugin-root`，也仍然可以带上项目根目录里的 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png`。

## package_plugin.bat - 仓库内插件快捷入口

这个批处理只给仓库内插件项目使用。它会自动定位 `.venv`、自动调用 `package_cvxp.py --build`，因此各插件目录下的 `.bat` 文件可以只保留一行转发。

### 用法

```powershell
Scripts\package_plugin.bat Pattern --no-upload
```

## package_project.bat - 仓库内项目快捷入口

这个批处理与 `package_plugin.bat` 类似，但目标目录改为 `Projects/*/*.csproj`。适用于客户项目或项目化插件。

### 用法

```powershell
Scripts\package_project.bat ProjectARVR --no-upload
```

## package_cvxp_demo.bat - 外部交付示例

这个批处理面向仓库外部使用场景。把 `package_cvxp.py`、`shared_files.json` 和这个 demo 放在同一个目录，修改里面的 `SRC_DIR` 后就可以直接打包。

### 用法

```powershell
Scripts\package_cvxp_demo.bat
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
