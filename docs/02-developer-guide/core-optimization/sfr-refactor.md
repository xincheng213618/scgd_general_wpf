# SFR 模块重构完成文档

## 重构概述

SFR 模块已从分散的三层结构重构为集中式结构，符合 Core 模块的统一架构设计。

## 目录结构变化

### 重构前
```
packages/sfr/                  # 分散的算法库
├── include/sfr/
│   ├── general.h
│   ├── slanted.h
│   └── cylinder.h
└── src/
    ├── general.cpp
    ├── slanted.cpp
    └── cylinder.cpp

include/
└── opencv_media_export.h      # C 接口声明

Core/opencv_helper/
└── opencv_media_export.cpp    # C 接口实现（调用 sfr）
```

### 重构后
```
Core/opencv_helper/
├── algorithm/
│   └── sfr/                   # SFR 算法实现
│       ├── sfr_base.h/.cpp    # 基础工具函数（原 general）
│       ├── sfr_slanted.h/.cpp # 斜边法 SFR（原 slanted）
│       └── sfr_cylinder.h/.cpp# 圆柱法 SFR（原 cylinder）
│
├── include/cvcore/
│   └── sfr.h                  # 统一接口头文件
│
└── exports/
    └── sfr_export.cpp         # C 接口导出实现

packages/sfr/                  # 标记为废弃（可删除）
```

## 命名空间变更

| 旧命名空间 | 新命名空间 | 说明 |
|-----------|-----------|------|
| `::sfr` | `cvcore::sfr` | 统一到 cvcore 命名空间 |

### 向后兼容
```cpp
// 在 sfr_base.h 中提供别名
namespace sfr = cvcore::sfr;
```

## API 变更

### C++ 接口

| 旧 API | 新 API | 说明 |
|--------|--------|------|
| `sfr::CalSFR()` | `cvcore::sfr::calculateSlantedEdgeSFR()` | 新名称更清晰 |
| `sfr::circle` | `cvcore::sfr::Circle` | 类型名大写 |
| `sfr::circle_fit()` | `cvcore::sfr::fitCircle()` | 动词开头 |
| `sfr::esf()` | `cvcore::sfr::cylinderESF()` | 区分方法 |
| `sfr::lsf()` | `cvcore::sfr::cylinderLSF()` | 区分方法 |
| `sfr::mtf()` | `cvcore::sfr::cylinderMTF()` | 区分方法 |

### 新增 C++ 接口
```cpp
// 结构化返回结果
struct SFRResult {
    double vslope;
    std::vector<double> freq;
    std::vector<double> sfr;
    double mtf10_norm;
    double mtf50_norm;
    double mtf10_cypix;
    double mtf50_cypix;
    
    bool isValid() const;
};

struct CylinderSFRResult {
    Circle circle;
    std::vector<cv::Point2d> esf;
    std::vector<cv::Point2d> lsf;
    std::vector<cv::Point2d> mtf;
    double mtf10;
    double mtf50;
    
    bool isValid() const;
};

// 新的主函数
SFRResult calculateSlantedEdgeSFR(const cv::Mat& img,
                                   double del = 1.0,
                                   int npol = 5,
                                   int nbin = 4,
                                   double vslope = -1);

CylinderSFRResult calculateCylinderSFR(const cv::Mat& mat,
                                        int thresh = 80,
                                        float roi = 15.0f,
                                        float binsize = 0.032f,
                                        int n_fit = 25);
```

### C 接口（保持不变）
```cpp
COLORVISIONCORE_API int M_CalSFR(...);
COLORVISIONCORE_API int M_CalSFRMultiChannel(...);
```

## 文件对应关系

| 原文件 | 新文件 | 说明 |
|--------|--------|------|
| `packages/sfr/include/sfr/general.h` | `Core/opencv_helper/algorithm/sfr/sfr_base.h` | 重命名 |
| `packages/sfr/include/sfr/slanted.h` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.h` | 重命名 |
| `packages/sfr/include/sfr/cylinder.h` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.h` | 重命名 |
| `packages/sfr/src/general.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_base.cpp` | 重命名 |
| `packages/sfr/src/slanted.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.cpp` | 重命名 |
| `packages/sfr/src/cylinder.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.cpp` | 重命名 |

## 编译配置更新

### vcxproj 文件修改
需要更新 `Core/opencv_helper/opencv_helper.vcxproj`：

1. **添加包含目录**
```xml
<AdditionalIncludeDirectories>
  $(ProjectDir)/include/cvcore;
  $(ProjectDir)/algorithm/sfr;
  %(AdditionalIncludeDirectories)
</AdditionalIncludeDirectories>
```

2. **添加源文件**
```xml
<ItemGroup>
  <ClCompile Include="algorithm\sfr\sfr_base.cpp" />
  <ClCompile Include="algorithm\sfr\sfr_slanted.cpp" />
  <ClCompile Include="algorithm\sfr\sfr_cylinder.cpp" />
  <ClCompile Include="exports\sfr_export.cpp" />
</ItemGroup>

<ItemGroup>
  <ClInclude Include="include\cvcore\sfr.h" />
  <ClInclude Include="algorithm\sfr\sfr_base.h" />
  <ClInclude Include="algorithm\sfr\sfr_slanted.h" />
  <ClInclude Include="algorithm\sfr\sfr_cylinder.h" />
</ItemGroup>
```

3. **移除旧引用**
```xml
<!-- 移除以下内容 -->
<!-- <ClCompile Include="..\..\packages\sfr\src\*.cpp" /> -->
```

### CMakeLists.txt（如果使用 CMake）
```cmake
# SFR 模块
set(SFR_SOURCES
    algorithm/sfr/sfr_base.cpp
    algorithm/sfr/sfr_slanted.cpp
    algorithm/sfr/sfr_cylinder.cpp
    exports/sfr_export.cpp
)

set(SFR_HEADERS
    include/cvcore/sfr.h
    algorithm/sfr/sfr_base.h
    algorithm/sfr/sfr_slanted.h
    algorithm/sfr/sfr_cylinder.h
)

target_sources(opencv_helper PRIVATE ${SFR_SOURCES} ${SFR_HEADERS})

target_include_directories(opencv_helper PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include/cvcore
    ${CMAKE_CURRENT_SOURCE_DIR}/algorithm/sfr
)
```

## 迁移步骤

1. **备份现有代码**
```bash
git checkout -b sfr-refactor-backup
git add .
git commit -m "Backup before SFR refactor"
```

2. **验证新代码编译**
```bash
# 清理旧编译缓存
dotnet clean

# 重新编译
dotnet build
```

3. **运行测试**
```bash
# 运行 SFR 相关测试
# 确保 M_CalSFR 和 M_CalSFRMultiChannel 功能正常
```

4. **删除旧文件**
```bash
# 确认新代码工作正常后，删除旧目录
rm -rf packages/sfr/
```

## 代码示例

### 使用新 C++ 接口
```cpp
#include <cvcore/sfr.h>

using namespace cvcore;

// 斜边法 SFR
cv::Mat img = cv::imread("edge.png", cv::IMREAD_GRAYSCALE);
auto result = sfr::calculateSlantedEdgeSFR(img, 1.0, 5, 4);

if (result.isValid()) {
    std::cout << "MTF50: " << result.mtf50_cypix << " cy/pix" << std::endl;
}

// 圆柱法 SFR
cv::Mat cylinder_img = cv::imread("circle.png", cv::IMREAD_GRAYSCALE);
auto cyl_result = sfr::calculateCylinderSFR(cylinder_img, 80, 15.0f);

if (cyl_result.isValid()) {
    std::cout << "MTF10: " << cyl_result.mtf10 << std::endl;
}
```

### 向后兼容（旧代码）
```cpp
// 仍然可以使用旧命名空间
#include <cvcore/sfr.h>

// 使用旧的调用方式
sfr::SFRResult result = sfr::CalSFR(img, 1.0, 5, 4, -1);
```

## 优势

1. **结构清晰**: 所有 SFR 相关代码集中在一个目录
2. **命名一致**: 统一使用 `cvcore::` 命名空间
3. **接口统一**: 与其他算法模块（如融合）结构一致
4. **易于维护**: 修改 SFR 只需在一个地方
5. **向后兼容**: 保留旧 API，平滑迁移

## 注意事项

1. **Eigen 依赖**: 确保项目仍能访问 Eigen 库
2. **OpenCV 版本**: 需要 OpenCV 4.x
3. **编译器**: 需要 C++17 支持
