# 部署概览

本页只保留当前仓库仍在使用的部署入口，重点覆盖 Windows 桌面应用、安装器和更新机制。

## 当前部署对象

- `ColorVision/`：主程序本体
- `ColorVisionSetup/`：安装与更新相关程序
- `Scripts/`：构建、打包、发布辅助脚本
- `Plugins/`：运行时加载的插件目录

## 当前推荐路径

### 开发或测试环境

直接从源码构建并运行主程序：

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

### 交付环境

- 使用安装器交付完整桌面程序
- 按需携带插件目录和运行时依赖
- 若涉及在线更新，查看 [自动更新系统](./auto-update.md)

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
