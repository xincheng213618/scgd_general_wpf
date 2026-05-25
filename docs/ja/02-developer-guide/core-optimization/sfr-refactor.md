# SFRモジュールリファクタリング完了文書

## リファクタリングの概要

SFRモジュールは、コアモジュールの統一アーキテクチャ設計に合わせて、分散型の3層構造から集中型構造に再構築されました。

## ディレクトリ構造の変更

### 再建前

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


### 再建後

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


## 名前空間の変更

|古い名前空間 |新しい名前空間 |説明 |
|-----------|-----------|------|
| `::sfr` | `cvcore::sfr` | cvcore 名前空間に統合 |

### 下位互換性

```cpp
// 在 sfr_base.h 中提供别名
namespace sfr = cvcore::sfr;
```


## API の変更

### C++ インターフェース

|古い API |新しい API |説明 |
|--------|--------|------|
| `sfr::CalSFR()` | `cvcore::sfr::calculateSlantedEdgeSFR()` |新しい名前がより明確になりました |
| `sfr::circle` | `cvcore::sfr::Circle` |名前を大文字で入力します |
| `sfr::circle_fit()` | `cvcore::sfr::fitCircle()` |動詞の始まり |
| `sfr::esf()` | `cvcore::sfr::cylinderESF()` |見分け方｜
| `sfr::lsf()` | `cvcore::sfr::cylinderLSF()` |見分け方｜
| `sfr::mtf()` | `cvcore::sfr::cylinderMTF()` |見分け方｜

### C++ インターフェースを追加しました

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


### C インターフェイス (変更されないまま)

```cpp
COLORVISIONCORE_API int M_CalSFR(...);
COLORVISIONCORE_API int M_CalSFRMultiChannel(...);
```


## ファイル対応

|元のファイル |新しいファイル |説明 |
|--------|--------|------|
| `packages/sfr/include/sfr/general.h` | `Core/opencv_helper/algorithm/sfr/sfr_base.h` |名前を変更 |
| `packages/sfr/include/sfr/slanted.h` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.h` |名前を変更 |
| `packages/sfr/include/sfr/cylinder.h` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.h` |名前を変更 |
| `packages/sfr/src/general.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_base.cpp` |名前を変更 |
| `packages/sfr/src/slanted.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.cpp` |名前を変更 |
| `packages/sfr/src/cylinder.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.cpp` |名前を変更 |

## コンパイル設定の更新

### vcxproj ファイルの変更
`Core/opencv_helper/opencv_helper.vcxproj` を更新する必要があります:

1. **インクルード ディレクトリを追加**

```xml
<AdditionalIncludeDirectories>
  $(ProjectDir)/include/cvcore;
  $(ProjectDir)/algorithm/sfr;
  %(AdditionalIncludeDirectories)
</AdditionalIncludeDirectories>
```


2. **ソース ファイルを追加**

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


3. **古い参照を削除**

```xml
<!-- 移除以下内容 -->
<!-- <ClCompile Include="..\..\packages\sfr\src\*.cpp" /> -->
```


### CMakeLists.txt (CMake を使用している場合)

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


## 移行手順

1. **既存のコードをバックアップします**

```bash
git checkout -b sfr-refactor-backup
git add .
git commit -m "Backup before SFR refactor"
```


2. **新しいコードのコンパイルを確認します**

```bash
# 清理旧编译缓存
dotnet clean

# 重新编译
dotnet build
```


3. **テストを実行します**

```bash
# 运行 SFR 相关测试
# 确保 M_CalSFR 和 M_CalSFRMultiChannel 功能正常
```


4. **古いファイルを削除**

```bash
# 确认新代码工作正常后，删除旧目录
rm -rf packages/sfr/
```


## コード例

### 新しい C++ インターフェイスの使用

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


### 下位互換性 (古いコード)

```cpp
// 仍然可以使用旧命名空间
#include <cvcore/sfr.h>

// 使用旧的调用方式
sfr::SFRResult result = sfr::CalSFR(img, 1.0, 5, 4, -1);
```


## 利点

1. **明確な構造**: すべての SFR 関連コードが 1 つのディレクトリに集中されています。
2. **名前の一貫性**: `cvcore::` 名前空間を均一に使用する
3. **インターフェイスの統合**: 他のアルゴリズム モジュール (融合など) と構造が一貫しています。
4. **メンテナンスが簡単**: SFR を 1 か所のみ変更します
5. **下位互換性**: 古い API を保持し、スムーズに移行します。

## 注意事項

1. **Eigen の依存関係**: プロジェクトが引き続き Eigen ライブラリにアクセスできることを確認してください。
2. **OpenCV バージョン**: OpenCV 4.x が必要です
3. **コンパイラ**: C++17 サポートが必要です