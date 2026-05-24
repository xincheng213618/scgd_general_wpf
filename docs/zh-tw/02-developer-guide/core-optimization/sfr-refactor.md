# SFR 模組重構完成文件

## 重構概述

SFR 模組已從分散的三層結構重構為集中式結構，符合 Core 模組的統一架構設計。

## 目錄結構變化

### 重構前
```
packages/sfr/                  # 分散的演算法庫
├── include/sfr/
│   ├── general.h
│   ├── slanted.h
│   └── cylinder.h
└── src/
    ├── general.cpp
    ├── slanted.cpp
    └── cylinder.cpp

include/
└── opencv_media_export.h      # C 介面宣告

Core/opencv_helper/
└── opencv_media_export.cpp    # C 介面實現（呼叫 sfr）
```

### 重構後
```
Core/opencv_helper/
├── algorithm/
│   └── sfr/                   # SFR 演算法實現
│       ├── sfr_base.h/.cpp    # 基礎工具函式（原 general）
│       ├── sfr_slanted.h/.cpp # 斜邊法 SFR（原 slanted）
│       └── sfr_cylinder.h/.cpp# 圓柱法 SFR（原 cylinder）
│
├── include/cvcore/
│   └── sfr.h                  # 統一介面標頭檔案
│
└── exports/
    └── sfr_export.cpp         # C 介面匯出實現

packages/sfr/                  # 標記為廢棄（可刪除）
```

## 名稱空間變更

| 舊名稱空間 | 新名稱空間 | 說明 |
|-----------|-----------|------|
| `::sfr` | `cvcore::sfr` | 統一到 cvcore 名稱空間 |

### 向後相容
```cpp
// 在 sfr_base.h 中提供別名
namespace sfr = cvcore::sfr;
```

## API 變更

### C++ 介面

| 舊 API | 新 API | 說明 |
|--------|--------|------|
| `sfr::CalSFR()` | `cvcore::sfr::calculateSlantedEdgeSFR()` | 新名稱更清晰 |
| `sfr::circle` | `cvcore::sfr::Circle` | 型別名大寫 |
| `sfr::circle_fit()` | `cvcore::sfr::fitCircle()` | 動詞開頭 |
| `sfr::esf()` | `cvcore::sfr::cylinderESF()` | 區分方法 |
| `sfr::lsf()` | `cvcore::sfr::cylinderLSF()` | 區分方法 |
| `sfr::mtf()` | `cvcore::sfr::cylinderMTF()` | 區分方法 |

### 新增 C++ 介面
```cpp
// 結構化返回結果
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

// 新的主函式
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

### C 介面（保持不變）
```cpp
COLORVISIONCORE_API int M_CalSFR(...);
COLORVISIONCORE_API int M_CalSFRMultiChannel(...);
```

## 檔案對應關係

| 原檔案 | 新檔案 | 說明 |
|--------|--------|------|
| `packages/sfr/include/sfr/general.h` | `Core/opencv_helper/algorithm/sfr/sfr_base.h` | 重新命名 |
| `packages/sfr/include/sfr/slanted.h` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.h` | 重新命名 |
| `packages/sfr/include/sfr/cylinder.h` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.h` | 重新命名 |
| `packages/sfr/src/general.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_base.cpp` | 重新命名 |
| `packages/sfr/src/slanted.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.cpp` | 重新命名 |
| `packages/sfr/src/cylinder.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.cpp` | 重新命名 |

## 編譯配置更新

### vcxproj 檔案修改
需要更新 `Core/opencv_helper/opencv_helper.vcxproj`：

1. **新增包含目錄**
```xml
<AdditionalIncludeDirectories>
  $(ProjectDir)/include/cvcore;
  $(ProjectDir)/algorithm/sfr;
  %(AdditionalIncludeDirectories)
</AdditionalIncludeDirectories>
```

2. **新增原始檔**
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

3. **移除舊引用**
```xml
<!-- 移除以下內容 -->
<!-- <ClCompile Include="..\..\packages\sfr\src\*.cpp" /> -->
```

### CMakeLists.txt（如果使用 CMake）
```cmake
# SFR 模組
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

## 遷移步驟

1. **備份現有程式碼**
```bash
git checkout -b sfr-refactor-backup
git add .
git commit -m "Backup before SFR refactor"
```

2. **驗證新程式碼編譯**
```bash
# 清理舊編譯快取
dotnet clean

# 重新編譯
dotnet build
```

3. **執行測試**
```bash
# 執行 SFR 相關測試
# 確保 M_CalSFR 和 M_CalSFRMultiChannel 功能正常
```

4. **刪除舊檔案**
```bash
# 確認新程式碼工作正常後，刪除舊目錄
rm -rf packages/sfr/
```

## 程式碼示例

### 使用新 C++ 介面
```cpp
#include <cvcore/sfr.h>

using namespace cvcore;

// 斜邊法 SFR
cv::Mat img = cv::imread("edge.png", cv::IMREAD_GRAYSCALE);
auto result = sfr::calculateSlantedEdgeSFR(img, 1.0, 5, 4);

if (result.isValid()) {
    std::cout << "MTF50: " << result.mtf50_cypix << " cy/pix" << std::endl;
}

// 圓柱法 SFR
cv::Mat cylinder_img = cv::imread("circle.png", cv::IMREAD_GRAYSCALE);
auto cyl_result = sfr::calculateCylinderSFR(cylinder_img, 80, 15.0f);

if (cyl_result.isValid()) {
    std::cout << "MTF10: " << cyl_result.mtf10 << std::endl;
}
```

### 向後相容（舊程式碼）
```cpp
// 仍然可以使用舊名稱空間
#include <cvcore/sfr.h>

// 使用舊的呼叫方式
sfr::SFRResult result = sfr::CalSFR(img, 1.0, 5, 4, -1);
```

## 優勢

1. **結構清晰**: 所有 SFR 相關程式碼集中在一個目錄
2. **命名一致**: 統一使用 `cvcore::` 名稱空間
3. **介面統一**: 與其他演算法模組（如融合）結構一致
4. **易於維護**: 修改 SFR 只需在一個地方
5. **向後相容**: 保留舊 API，平滑遷移

## 注意事項

1. **Eigen 依賴**: 確保專案仍能訪問 Eigen 庫
2. **OpenCV 版本**: 需要 OpenCV 4.x
3. **編譯器**: 需要 C++17 支援
