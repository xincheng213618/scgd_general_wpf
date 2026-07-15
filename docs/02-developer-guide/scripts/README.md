# 构建与发布脚本

本页只回答四件事：正式发布走哪个入口，插件和项目包怎么打包，上传凭据怎么提供，失败时先查哪里。脚本参数以源码和 `--help` 为准；不要把这里写成每个 Python 函数的说明书。

## 先记住

| 场景 | 使用入口 | 说明 |
| --- | --- | --- |
| 主程序正式发布 | `Scripts\release.bat` | 唯一正常入口，会构建安装包、上传主包、更新 `LATEST_RELEASE`、上传增量包并生成全量 zip |
| 发布插件包 | `Scripts\package_plugin.bat <PluginName>` | 面向 `Plugins/<PluginName>/`，上传成功后删除本地 `.cvxp` |
| 发布项目包 | `Scripts\package_project.bat <ProjectName>` | 面向 `Projects/<ProjectName>/`，上传成功后删除本地 `.cvxp` |
| 发布外部编译产物 | `py Scripts\package_cvxp.py --src-dir <输出目录>` | 适合只拿到插件输出目录的场景 |
| 只校验插件清单 | `py Scripts\package_cvxp.py --project-file <插件.csproj> --validate-only` | 不构建、不打包、不上传 |
| 刷新共享文件表 | `py Scripts\generate_shared_files.py` | 只有宿主输出目录共享 DLL 明显变化时才需要 |

`build.py` 和 `build_update.py` 是 `release.bat` 的内部步骤。正式发布不要绕过 `release.bat` 单独跑它们；`build_update.py` 没有安全的 `--help` 查询模式，直接执行会进入增量包生成和上传流程。

## 正式发布

发布前先提升仓库根目录 `Directory.Build.props` 的版本号，然后只运行：

```powershell
Scripts\release.bat
```

发布成功时，控制台应能看到主包上传、`LATEST_RELEASE` 更新和增量包上传成功。本地安装包、全量 zip、增量包是正常发布产物，不代表“本地-only 发布”。发布失败时先修复失败原因，再重新走 `release.bat`。

## 插件和项目包

| 场景 | 命令 |
| --- | --- |
| 仓库内插件 | `Scripts\package_plugin.bat Spectrum` |
| 仓库内项目包 | `Scripts\package_project.bat ProjectLUX` |
| 外部编译产物 | `py Scripts\package_cvxp.py --src-dir C:\path\to\MyPlugin\bin\x64\Release\net10.0-windows` |

插件和项目包默认上传，并在上传流程结束后删除本地 `.cvxp`。构建和上传前会先校验 `manifest.json`；若声明 `copilot_agents`，还会检查角色 ID、工具名、作用域、只读能力、模式、预算、重复项，以及单插件最多 16 个角色和 8,000 个常驻名称/说明字符的上限。需要在 CI 或发布前单独检查时使用 `--validate-only`。校验通过后，打包再读取 `Scripts/shared_files.json`，剔除宿主已共享文件和 `.pdb`，生成 `.cvxp`。

## 上传配置

远程上传优先使用环境变量，不要在文档或脚本调用里写真实账号密码：

```powershell
$env:COLORVISION_UPLOAD_URL = "http://<host>:<port>"
$env:COLORVISION_UPLOAD_FOLDER = "ColorVision"
$env:COLORVISION_UPLOAD_USERNAME = "<user>"
$env:COLORVISION_UPLOAD_PASSWORD = "<password>"
$env:COLORVISION_UPLOAD_USE_SYSTEM_PROXY = "1"
```

上传脚本会先做后端预检。新后端走 `/api/health` 和 `/api/ready`；旧后端没有这些接口时会按兼容模式继续上传。

## 脚本速查

| 脚本 | 是否日常入口 | 用途 |
| --- | --- | --- |
| `release.bat` | 是 | 主程序正式发布入口 |
| `package_cvxp.py` | 是 | `.cvxp` 打包、上传和本地包清理 |
| `package_plugin.bat` | 是 | 仓库内插件打包快捷入口 |
| `package_project.bat` | 是 | 仓库内项目包打包快捷入口 |
| `clear-bin.ps1`、`clear-artifacts.ps1` | 是 | 清理本地构建产物 |
| `build.py`、`build_update.py` | 否 | `release.bat` 内部构建、上传和增量更新步骤 |
| `backend_client.py`、`file_manager.py` | 否 | 上传认证、预检、流式上传和路径辅助 |
| `generate_shared_files.py` | 偶尔 | 生成宿主共享文件清单 |
| `build_spectrum.py` | 特殊 | Spectrum 插件专用构建入口 |

如果某个脚本不在 `Scripts/` 目录里，就不要在文档里继续引用它。

## 常见失败

| 现象 | 先查 |
| --- | --- |
| 主程序发布没有上传成功证据 | 后端预检、上传地址、账号密码、网络代理、`release.bat` 输出 |
| 增量包失败 | 历史版本 zip 是否存在、当前 `ColorVision.exe` 文件版本、上传返回码 |
| `.cvxp` 包过大 | `shared_files.json` 是否过期，宿主共享 DLL 是否被误带入包 |
| 插件/项目包找不到项目 | 名称是否等于 `Plugins/<Name>/<Name>.csproj` 或 `Projects/<Name>/<Name>.csproj` |
| 上传 401 或连接失败 | 环境变量、后端是否运行、URL 是否正确、代理是否需要启用 |
| 构建失败 | 先单独跑对应 `dotnet build`，再看 MSBuild、Advanced Installer 或外部 DLL |

改正式发布路径时，需要做一次测试环境发布演练。
