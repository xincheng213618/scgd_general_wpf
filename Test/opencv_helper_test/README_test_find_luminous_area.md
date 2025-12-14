# M_FindLuminousArea 自适应阈值测试

这个测试程序用于验证 `M_FindLuminousArea` 函数的自适应阈值功能（使用 Otsu 方法）。

## 功能说明

测试程序包含以下测试用例：

1. **测试1: 固定阈值** - 使用指定的阈值值（Threshold=100）
2. **测试2: 自动阈值（显式）** - 设置 Threshold=-1 启用 OTSU 自动阈值
3. **测试3: 自动阈值（省略）** - 完全省略 Threshold 参数，默认使用自动阈值
4. **测试4: 旋转矩形 + 自动阈值** - 使用 UseRotatedRect=true 与自动阈值结合
5. **测试5: ROI + 自动阈值** - 指定感兴趣区域（ROI）并使用自动阈值
6. **测试6: 真实图像测试** - 从文件读取真实图像进行测试

## 编译方法

### 使用 Visual Studio 2022

1. 打开 `opencv_helper_test.vcxproj`
2. 将 `test_find_luminous_area.cpp` 添加到项目中
3. 确保已配置 OpenCV 和 nlohmann/json 库路径
4. 编译项目

### 手动编译（命令行）

```bash
cl /EHsc test_find_luminous_area.cpp /I"..\..\include" /I"<opencv_path>\include" /link opencv_helper.lib
```

## 运行方法

### 基本测试（使用程序生成的测试图像）

```bash
test_find_luminous_area.exe
```

程序会自动生成测试图像并运行所有测试用例。

### 使用真实图像测试

```bash
test_find_luminous_area.exe "C:\path\to\your\image.png"
```

支持的图像格式：PNG, JPEG, TIFF, BMP 等 OpenCV 支持的格式。

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
