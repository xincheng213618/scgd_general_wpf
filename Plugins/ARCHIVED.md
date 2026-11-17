# 已退役插件

| 插件 | 退役日期 | 归档标签 | 归档源码版本 | 清单最低宿主版本 |
| --- | --- | --- | --- | --- |
| `CameraTest`（相机生产调试） | 2026-11-17 | `archive/retired-cameratest-2026-11-17` | `1.0.0.16` | `1.4.16.5` |

CameraTest 的宿主插件和独立程序停止维护。归档标签保留项目源码、专用相机会话、测试、离线 ZIP 打包脚本和原使用说明。在仓库根目录可直接查看归档文档：

```powershell
git show archive/retired-cameratest-2026-11-17:docs/04-api-reference/plugins/standard-plugins/camera-test.md
```

需要重现历史版本时，可在归档标签处创建维护分支。归档仍需对应的工具链、共享依赖和相机驱动；插件运行需选用兼容的旧版宿主。
