# 构建与发布脚本

ColorVision 项目包含一组 Python 脚本，用于构建应用程序、打包插件、发布更新和管理后端上传。

## 脚本概览

| 脚本 | 功能 |
|------|------|
| `build.py` | 构建主程序安装包并发布 |
| `build_update.py` | 构建增量更新包 |
| `build_plugin.py` | 构建通用插件包 (.cvxp) |
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
# 构建 Plugins 目录下的插件
py Scripts\build_plugin.py -p ProjectName

# 构建其他类型目录下的插件
py Scripts\build_plugin.py -p ProjectName -t Projects
```

### 参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `-p, --project_name` | 项目名称 | `ProjectBase` |
| `-t, --type` | 项目类型 | `Plugins` |

### 打包逻辑

1. 比较 `src_dir` 和 `ref_dir` 的文件差异
2. 排除 `.pdb` 文件
3. 包含 `README.md`, `CHANGELOG.md`, `manifest.json`, `PackageIcon.png`
4. 生成 `{PluginName}-{version}.cvxp` 包
5. 上传到后端 `Plugins/{PluginName}/`

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
