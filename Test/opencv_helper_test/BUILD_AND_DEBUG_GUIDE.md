# opencv_helper_test 构建与调试

完整的构建步骤、专项参数、样本前提、结果判断和故障排查维护在[原生 helper 测试与调试](../../docs/02-developer-guide/engine-development/native-testing.md)。本文件是测试源码旁的入口；单独复制测试目录时，请读取匹配源码版本的完整说明。

## 必要前提

- Windows x64、Visual Studio C++/MSBuild 和 Windows SDK。测试项目当前使用 v145，helper 使用 v143；以各自 `.vcxproj` 为准，不通过随意降级 toolset 绕过缺项。
- x64 OpenCV 属性表使用 `packages/opencv/x64/vc18/bin` / `lib`、版本后缀 4140；Debug 另带 `d`。还需要 `packages/nlohmann.props` 引用的头文件。
- x64 的 main 在 `test_find_luminous_area.cpp`，由项目正常编译；用 `--luminous-v2-only` 等既有参数选择专项，无需更改 main 或链接入口。
- 完整 `scgd_general_wpf.sln` 的 Debug 配置把 helper 映射到 Release、测试映射到 Debug；直接项目构建的配置另行选择，不能混同。
- 构建会写输出并通过 helper 复制 native runtime 到主程序构建目录。测试可能创建/清理临时文件、加载指定 DLL 或调用 GPU；样本和运行环境须符合所选专项的前提。
- 默认运行会跳过缺失的可选样本，普通图片演示不能可靠地以退出码表示算法失败。保留完整命令、退出码、实际 DLL 与跳过信息。

API 和资源释放见[opencv_helper API 参考](../../docs/04-api-reference/engine-components/opencv-helper-api.md)。`build_test_find_luminous.bat` 的单文件/旧依赖编译方式不适用于当前 runner；使用项目构建并按完整指南核对 DLL、PDB 和样本。
