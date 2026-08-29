# 部署概览

本页只保留当前仓库仍在使用的部署入口，重点覆盖 Windows 桌面应用、安装器和更新机制。

## 当前部署对象

- `ColorVision/`：主程序本体，当前客户端更新实现位于 `ColorVision/Update/`
- `Scripts/`：构建、打包、发布辅助脚本
- 仓库外的 Advanced Installer `ColorVision.aip`：由 `Scripts\release.bat` 发布链调用，生成完整安装包
- `Plugins/`：运行时加载的插件目录

`src/ColorVisionSetup/` 是历史保留的安装/更新程序源码，未接入当前 `build.sln`、外部 `ColorVision.aip` 或 `Scripts\release.bat` 发布链，不是当前部署入口。当前实现与发布方式分别见 [自动更新系统](./auto-update.md) 和 [构建与发布脚本](../scripts/README.md)。

## 当前推荐路径

### 开发或测试环境

直接从源码构建并运行主程序：

```powershell
dotnet restore .\ColorVision\ColorVision.csproj
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

### 交付环境

- 使用安装器交付完整桌面程序
- 按需携带插件目录和运行时依赖
- 若涉及在线更新，查看 [自动更新系统](./auto-update.md)
- 启动检查与手动检查并发访问主程序版本接口时共享进行中的请求，部署侧无需为同一客户端的重复探测预留额外连接
- 插件详情连接在 2 秒无 HTTP 响应后通过新连接重试；候选插件元数据仍不完整时，主程序与插件更新整轮延期，避免组合更新被拆开
- 主程序增量复制对短暂文件占用最多重试 10 次，最终失败的文件与退出码记录在安装目录对应的更新状态日志中，并由“发送反馈”默认收集最近 7 天记录
- 带清单插件更新前会创建按安装目录隔离的校验备份，并以完整目录事务替换；旧式无清单包仍使用兼容覆盖路径
- 上次启动未完成时会进入独立恢复窗口，用户可先更新/修复主程序，或跳过、禁用、回退插件

## 部署前确认项

- 目标环境为 Windows
- 主应用按 x64 构建
- 运行时依赖和本地 DLL 已正确随包输出
- 需要的配置文件已复制到输出目录

## 配套文档

- [安装与首次使用](../../00-getting-started/README.md)
- [系统要求](../../00-getting-started/prerequisites.md)
- [自动更新系统](./auto-update.md)
- [构建与发布脚本](../scripts/README.md)

## 说明

- 旧的 Docker、云部署、生产集群等说明不再作为默认部署路径。
- 如果某个项目有特殊交付方式，应在对应项目目录或项目文档中单独维护，而不是继续堆在通用部署页里。
