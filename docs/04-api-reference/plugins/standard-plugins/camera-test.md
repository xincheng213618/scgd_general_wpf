---
knowledge_id: "plugins.camera-test"
knowledge_type: "topic"
status: "historical"
summary: "CameraTest 相机生产调试已退役。归档标签保留原插件、独立程序、测试与打包资料。"
aliases: ["CameraTest", "相机生产调试", "相机出厂检测", "独立相机测试", "StandaloneCameraSession"]
code_paths: ["Plugins/ARCHIVED.md"]
related: ["plugins.model"]
---

# 相机生产调试已退役

`CameraTest` 于 2026 年 11 月 17 日退役，宿主插件和独立程序均停止维护。当前源码已移除项目及其离线 ZIP 打包入口。更新到包含此变更的宿主后，插件装载器会按目录名或清单 ID 跳过残留的 `CameraTest`。现场已有的插件文件、独立程序、配置和存档由各安装环境保管。

版本 `1.0.0.16` 对应的源码、专用相机会话、测试、打包脚本和原说明可在 Git 标签 `archive/retired-cameratest-2026-11-17` 中查阅。取回方法见仓库中的 `Plugins/ARCHIVED.md`。
