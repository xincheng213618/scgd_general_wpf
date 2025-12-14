# M_FindLuminousArea 测试 - 编译和调试指南

## 快速开始

### 方法一：使用 Visual Studio（推荐）

1. **打开项目**
   - 用 Visual Studio 2022 打开解决方案：`scgd_general_wpf.sln`
   - 或直接打开：`Test/opencv_helper_test/opencv_helper_test.vcxproj`

2. **设置启动项目**
   - 在解决方案资源管理器中，右键点击 `opencv_helper_test`
   - 选择"设为启动项目"

3. **选择配置**
   - 顶部工具栏选择：`Debug | x64` 或 `Release | x64`
   - 推荐使用 Debug 配置进行调试

4. **编译项目**
   - 按 `F7` 或点击"生成" → "生成解决方案"
   - 确保先编译了 `opencv_helper` 项目（它会自动编译）

5. **运行测试**
   
   **选项 A：运行主测试程序**
   - 直接按 `F5`（调试）或 `Ctrl+F5`（不调试）
   - 这会运行 `opencv_helper_test.cpp` 中的原有测试

   **选项 B：运行 FindLuminousArea 专项测试**
   - 在 `opencv_helper_test.cpp` 文件中，将 `main` 函数改为：
     ```cpp
     #define TEST_FIND_LUMINOUS_AREA
     #include "test_find_luminous_area.cpp"
     ```
   - 或者直接修改项目属性，将 `test_find_luminous_area.cpp` 设为入口点

### 方法二：命令行编译（高级用户）

1. **打开 Developer Command Prompt for VS 2022**

2. **编译 opencv_helper.dll（如果还没编译）**
   ```cmd
   cd Core\opencv_helper
   msbuild opencv_helper.vcxproj /p:Configuration=Debug /p:Platform=x64
   ```

3. **编译测试程序**
   ```cmd
   cd ..\..\Test\opencv_helper_test
   msbuild opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
   ```

4. **运行测试**
   ```cmd
   cd ..\..\x64\Debug
   opencv_helper_test.exe
   ```

## 单独编译和运行 FindLuminousArea 测试

### 创建独立测试程序

我已经创建了 `build_test_find_luminous.bat` 批处理文件，可以快速编译独立测试：

```cmd
# 双击运行或在命令行执行
build_test_find_luminous.bat
```

运行后会在当前目录生成 `test_find_luminous_area.exe`

### 手动编译独立测试

```cmd
cl /EHsc /std:c++17 ^
   /I"..\..\include" ^
   /I"..\..\packages\opencv\include" ^
   /I"..\..\packages\nlohmann\include" ^
   test_find_luminous_area.cpp ^
   /link ^
   /LIBPATH:"..\..\x64\Debug" opencv_helper.lib ^
   /LIBPATH:"..\..\packages\opencv\lib" opencv_world4100d.lib
```

## 调试技巧

### 在 Visual Studio 中调试

1. **设置断点**
   - 在 `test_find_luminous_area.cpp` 的测试函数中点击左侧边栏设置断点
   - 例如在 `testFixedThreshold()` 函数的 `M_FindLuminousArea` 调用处

2. **查看变量**
   - 调试时，鼠标悬停在变量上查看值
   - 使用"监视窗口"添加关注的变量
   - 查看 `result` JSON 字符串的内容

3. **单步调试**
   - `F10`：逐过程（Step Over）
   - `F11`：逐语句（Step Into）- 可以进入 DLL 函数内部
   - `Shift+F11`：跳出（Step Out）

4. **命令行参数调试**
   - 右键项目 → 属性 → 调试 → 命令参数
   - 输入图像路径，例如：`"C:\test\image.png"`
   - 这样可以测试真实图像

### 使用测试图像

测试程序会自动生成合成图像，但你也可以使用真实图像：

```cmd
# 运行时指定图像路径
test_find_luminous_area.exe "C:\path\to\your\image.tif"
```

支持的图像格式：
- PNG, JPEG, TIFF, BMP
- 灰度图或彩色图
- 8位或16位深度

## 常见问题

### 问题1：找不到 opencv_helper.dll

**解决方案：**
- 确保已编译 `opencv_helper` 项目
- DLL 应该在：`x64\Debug\opencv_helper.dll` 或 `x64\Release\opencv_helper.dll`
- 将 DLL 复制到测试程序目录，或确保系统能找到它

### 问题2：找不到 OpenCV DLL

**解决方案：**
- OpenCV DLL 应该在 `packages\opencv\bin` 目录
- 将 OpenCV bin 目录添加到系统 PATH
- 或复制所需的 DLL 到测试程序目录

### 问题3：链接错误 LNK2019

**解决方案：**
- 确保配置（Debug/Release）和平台（x64）一致
- 检查 `opencv_helper.lib` 是否存在于 `x64\Debug` 或 `x64\Release`
- 重新编译 `opencv_helper` 项目

### 问题4：无法加载项目

**解决方案：**
- 确保使用 Visual Studio 2022（v143 工具集）
- 或修改项目文件中的 `<PlatformToolset>` 为你的版本（如 v142）

## 性能优化建议

### Debug 配置
- 用于开发和调试
- 包含完整调试信息
- 没有优化，速度较慢
- 可以单步调试进入函数

### Release 配置
- 用于性能测试
- 启用编译器优化
- 速度更快
- 调试信息有限

## 测试输出解析

### 成功输出示例
```
=== 测试2: 自动阈值 (Threshold=-1, OTSU) ===
成功! 返回值: 87
结果JSON: {"X":220,"Y":165,"Width":200,"Height":150}
发光区域: X=220, Y=165, Width=200, Height=150
```

### 错误码说明
- `-1`：输入参数无效（图像为空、config为NULL等）
- `-2`：未找到发光区域（可能图像太暗或阈值不合适）
- `-3`：内存分配失败

## 扩展测试

### 添加自定义测试用例

在 `test_find_luminous_area.cpp` 中添加新的测试函数：

```cpp
void testMyCustomCase()
{
    std::cout << "\n=== 自定义测试 ===" << std::endl;
    
    // 创建或加载你的图像
    cv::Mat testImg = cv::imread("my_image.png", cv::IMREAD_UNCHANGED);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置配置
    json config;
    config["Threshold"] = -1;  // 使用自动阈值
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();
    
    // 调用测试
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, {0,0,0,0}, configStr.c_str(), &result);
    
    // 检查结果
    if (ret > 0 && result != nullptr) {
        std::cout << "结果: " << result << std::endl;
        FreeResult(result);
    }
}

// 在 main 函数中调用
int main()
{
    // ... 其他测试 ...
    testMyCustomCase();
    return 0;
}
```

### 批量测试图像

创建一个测试图像目录，然后批量处理：

```cpp
void testImageDirectory(const std::string& dir)
{
    namespace fs = std::filesystem;
    for (const auto& entry : fs::directory_iterator(dir)) {
        if (entry.path().extension() == ".png" || 
            entry.path().extension() == ".tif") {
            std::cout << "\n测试: " << entry.path() << std::endl;
            testWithRealImage(entry.path().string());
        }
    }
}
```

## 进一步优化

如果需要更高级的调试功能，可以考虑：

1. **添加日志输出**
   - 在测试代码中使用 `spdlog` 记录详细日志
   - 保存测试结果到文件

2. **可视化结果**
   - 使用 OpenCV 的 `cv::imshow` 显示检测结果
   - 在原图上标注发光区域

3. **性能分析**
   - 使用 `std::chrono` 测量执行时间
   - 对比固定阈值和自动阈值的性能差异

4. **单元测试框架**
   - 集成 Google Test 或 Catch2
   - 自动化测试和结果验证

## 联系和反馈

如有问题或建议，请在 PR 中评论或创建 Issue。
