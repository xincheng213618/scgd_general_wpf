# M_FindLuminousArea 自适应阈值测试

这个测试程序用于验证 `M_FindLuminousArea` 函数的自适应阈值功能（使用 Otsu 方法）。

## 快速编译和运行

### 方法一：使用 Visual Studio（最简单）

1. 打开 `scgd_general_wpf.sln` 解决方案
2. 设置 `opencv_helper_test` 为启动项目
3. 选择配置：`Debug | x64`
4. 按 `F5` 开始调试

详细步骤请参考：[BUILD_AND_DEBUG_GUIDE.md](BUILD_AND_DEBUG_GUIDE.md)

### 方法二：使用批处理文件

双击运行 `build_test_find_luminous.bat`，会自动编译独立的测试程序。

**注意**：需要在 "Developer Command Prompt for VS 2022" 中运行。

### 方法三：命令行编译

```cmd
# 在 Developer Command Prompt for VS 2022 中执行
cd Test\opencv_helper_test
msbuild opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
```

## 功能说明

测试程序包含以下测试用例：

1. **测试1: 固定阈值** - 使用指定的阈值值（Threshold=100）
2. **测试2: 自动阈值（显式）** - 设置 Threshold=-1 启用 OTSU 自动阈值
3. **测试3: 自动阈值（省略）** - 完全省略 Threshold 参数，默认使用自动阈值
4. **测试4: 旋转矩形 + 自动阈值** - 使用 UseRotatedRect=true 与自动阈值结合
5. **测试5: ROI + 自动阈值** - 指定感兴趣区域（ROI）并使用自动阈值
6. **测试6: 真实图像测试** - 从文件读取真实图像进行测试

## 运行方法

### 基本测试（使用程序生成的测试图像）

```bash
# Debug 配置
x64\Debug\opencv_helper_test.exe

# Release 配置
x64\Release\opencv_helper_test.exe
```

程序会自动生成测试图像并运行所有测试用例。

### 使用真实图像测试

```bash
opencv_helper_test.exe "C:\path\to\your\image.png"
```

支持的图像格式：PNG, JPEG, TIFF, BMP 等 OpenCV 支持的格式。

## 调试技巧

### Visual Studio 调试

1. 在测试函数中设置断点（例如 `testFixedThreshold()` 函数）
2. 按 `F5` 开始调试
3. 使用 `F10` 单步执行，`F11` 步入函数
4. 在"监视"窗口查看变量值

### VS Code 调试

已提供 `.vscode/launch.json` 配置文件：
- 配置1：调试合成图像测试
- 配置2：调试真实图像测试（需修改图像路径）

按 `F5` 选择配置并开始调试。

## 测试输出说明

每个测试会输出：
- 测试名称和配置
- 函数返回值
- 结果 JSON 字符串
- 解析后的发光区域参数（X, Y, Width, Height 或 Corners）

示例输出：
```
=== 测试2: 自动阈值 (Threshold=-1, OTSU) ===
成功! 返回值: 87
结果JSON: {"X":220,"Y":165,"Width":200,"Height":150}
发光区域: X=220, Y=165, Width=200, Height=150
```

## API 使用示例

### 固定阈值模式

```cpp
json config;
config["Threshold"] = 100;  // 使用固定阈值 100
config["UseRotatedRect"] = false;
std::string configStr = config.dump();

char* result = nullptr;
int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
```

### 自动阈值模式（方法1：显式设置）

```cpp
json config;
config["Threshold"] = -1;  // 设置为 -1 启用自动阈值（OTSU）
config["UseRotatedRect"] = false;
std::string configStr = config.dump();

char* result = nullptr;
int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
```

### 自动阈值模式（方法2：省略参数）

```cpp
json config;
// 不包含 Threshold 参数，默认使用自动阈值
config["UseRotatedRect"] = false;
std::string configStr = config.dump();

char* result = nullptr;
int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
```

## 常见问题

### 问题1：找不到 opencv_helper.dll

确保已编译 `opencv_helper` 项目，DLL 在 `x64\Debug` 或 `x64\Release` 目录。

### 问题2：找不到 OpenCV DLL

将 OpenCV bin 目录添加到系统 PATH，或复制 DLL 到测试程序目录。

### 问题3：编译错误

确保使用 Visual Studio 2022，或修改 `.vcxproj` 中的 `PlatformToolset` 为你的版本。

详细问题解决方案请参考：[BUILD_AND_DEBUG_GUIDE.md](BUILD_AND_DEBUG_GUIDE.md)

## 文件说明

- `test_find_luminous_area.cpp` - 测试程序源代码
- `README_test_find_luminous_area.md` - 本文档
- `BUILD_AND_DEBUG_GUIDE.md` - 详细的编译和调试指南
- `build_test_find_luminous.bat` - 快速编译批处理文件
- `.vscode/launch.json` - VS Code 调试配置

## 注意事项

1. 自动阈值使用 Otsu 方法，适用于双峰直方图的图像
2. 任何负数阈值值都会触发自动阈值检测
3. 省略 Threshold 参数等同于设置 Threshold=-1
4. 返回值大于 0 表示成功，返回的是结果 JSON 字符串的长度
5. 使用完结果字符串后，必须调用 `FreeResult(result)` 释放内存

## 依赖项

- OpenCV 4.x
- nlohmann/json
- opencv_helper.dll（包含 M_FindLuminousArea 实现）

## 更新历史

- 2024-12-14: 创建测试程序，验证自适应阈值功能
- 2024-12-14: 添加编译脚本和调试配置，优化开发体验

