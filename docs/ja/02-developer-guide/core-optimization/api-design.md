#コアモジュール統一インターフェース設計仕様書

## 設計目標

1. **一貫性**: すべての画像処理関数は統一された呼び出し規則に従います。
2. **スケーラビリティ**: 新しい処理アルゴリズムとバックエンドを簡単に追加できます。
3. **型安全性**: 実行時エラーを回避するために強力な型を使用します。
4. **パフォーマンス**: ゼロオーバーヘッド抽象化、複数のバックエンドをサポート
5. **保守性**: 明確なエラー処理とログ記録

## アーキテクチャの概要


```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
├─────────────────────────────────────────────────────────────┤
│  cvcore::Image    cvcore::ImageSequence    cvcore::Result   │
├─────────────────────────────────────────────────────────────┤
│                  Processing Interface                       │
│  adjustWhiteBalance()  focusStacking()  applyGaussianBlur() │
├─────────────────────────────────────────────────────────────┤
│              Backend Abstraction Layer                      │
│     CPU (OpenCV)      CUDA      OpenCL      OpenMP         │
├─────────────────────────────────────────────────────────────┤
│              Platform Abstraction Layer                     │
│         cv::Mat    cuda::GpuMat    cl::Buffer              │
└─────────────────────────────────────────────────────────────┘
```


## コアの種類

### 1. 画像クラス

パッケージ `cv::Mat` は、統合インターフェイスを提供します。


```cpp
namespace cvcore {

class Image {
public:
    // 构造
    Image();
    explicit Image(const cv::Mat& mat);
    Image(int width, int height, int channels, int depth);
    
    // 工厂方法
    static Result<Image> fromFile(const std::string& path);
    static Image zeros(const ImageDescriptor& desc);
    
    // 访问器
    int width() const;
    int height() const;
    int channels() const;
    bool empty() const;
    
    // 转换
    cv::Mat& mat();
    Result<Image> toGray() const;
    Result<Image> convertDepth(int newDepth) const;
    
    // 操作
    Image clone() const;
    Image roi(int x, int y, int w, int h) const;
};

} // namespace cvcore
```


### 2. 結果\<T\> タイプ

エラー処理の場合:


```cpp
template<typename T>
using Result = std::pair<T, std::optional<Error>>;

// 使用示例
auto result = cvcore::adjustWhiteBalance(image, options);
if (result.second) {
    // 错误处理
    std::cerr << "Error: " << result.second->message << std::endl;
    return;
}
Image output = result.first;
```


### 3. 処理オプション

すべてのハンドラー関数のオプション基本クラス:


```cpp
struct ProcessingOptions {
    ProcessingBackend backend = ProcessingBackend::Auto;
    ProcessingContext* context = nullptr;
    bool async = false;
    ProgressCallback progressCallback;
};

// 派生选项
struct WhiteBalanceOptions : ProcessingOptions {
    double redBalance = 1.0;
    double greenBalance = 1.0;
    double blueBalance = 1.0;
};
```


## 処理関数仕様

### 命名規則

|操作タイプ |プレフィックス |例 |
|----------|------|------|
|色調整 | `adjust` | `adjustWhiteBalance` |
|フィルター | `apply` | `applyGaussianBlur` |
|自動処理 | `auto` | `autoLevels` |
|検出 | `find`/`detect` | `findLightBeads` |
|変換 | `to`/`convert` | `toGray`、`convertDepth` |

### パラメータの順序


```cpp
Result<Image> functionName(
    const Image& src,           // 1. 输入图像 (const引用)
    const SpecificOptions& options  // 2. 选项 (const引用)
);

// 带额外参数的变体
Result<Image> functionName(
    const Image& src,
    double param1,              // 3. 主要参数
    int param2,                 // 4. 次要参数
    const ProcessingOptions& options  // 5. 选项
);
```


### エラー処理


```cpp
// 错误码定义
enum class ErrorCode {
    Success = 0,
    InvalidParameter = -1,
    InvalidImage = -2,
    UnsupportedFormat = -3,
    OutOfMemory = -4,
    // ...
};

// 错误信息
struct Error {
    ErrorCode code;
    std::string message;
    std::string function;
    std::string file;
    int line;
};

// 便捷宏
#define CVCORE_CHECK_IMAGE(img) \
    do { \
        if (img.empty()) { \
            return { {}, std::make_optional<Error>( \
                ErrorCode::InvalidImage, "Empty image", \
                __FUNCTION__, __FILE__, __LINE__) }; \
        } \
    } while(0)
```


## バックエンドの選択

### 自動選択ロジック


```cpp
ProcessingBackend selectBackend(ProcessingBackend requested, const Image& img) {
    if (requested != ProcessingBackend::Auto) {
        return requested;
    }
    
    // 优先 CUDA (如果可用且图像足够大)
    #ifdef HAVE_CUDA
    if (cuda::isAvailable() && img.sizeBytes() > 10 * 1024 * 1024) {
        return ProcessingBackend::CUDA;
    }
    #endif
    
    // 其次 OpenCL
    #ifdef HAVE_OPENCL
    if (ocl::isAvailable()) {
        return ProcessingBackend::OpenCL;
    }
    #endif
    
    // 默认 CPU
    return ProcessingBackend::CPU;
}
```


### バックエンド固有の実装


```cpp
Result<Image> adjustWhiteBalance(const Image& src, const WhiteBalanceOptions& options) {
    CVCORE_CHECK_IMAGE(src);
    
    auto backend = selectBackend(options.backend, src);
    
    switch (backend) {
        case ProcessingBackend::CUDA:
            return adjustWhiteBalanceCUDA(src, options);
        case ProcessingBackend::OpenCL:
            return adjustWhiteBalanceOpenCL(src, options);
        case ProcessingBackend::CPU:
        default:
            return adjustWhiteBalanceCPU(src, options);
    }
}
```


## 下位互換性

### 古いインターフェースを維持する


```cpp
// 新接口
CV_CORE_API Result<Image> focusStacking(const ImageSequence& images, 
                                        const FocusStackingOptions& options);

// 旧接口 (保留，内部调用新接口)
COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
```


### 適応層


```cpp
// 旧类型到新类型的转换
Image convertFromHImage(const HImage* hImg);
Error convertToHImage(const Image& img, HImage* out);
```


## パフォーマンスに関する考慮事項

### 1. 移動セマンティクス


```cpp
// 避免拷贝
Image process(Image&& input) {
    // 直接修改输入，避免分配新内存
    // ...
    return std::move(input);
}
```


### 2. メモリプール


```cpp
class ProcessingContext {
public:
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;
    
    cv::Mat acquireBuffer(const ImageDescriptor& desc);
    void releaseBuffer(cv::Mat& buffer);
};
```


### 3. 非同期処理


```cpp
// 异步接口
std::future<Result<Image>> adjustWhiteBalanceAsync(const Image& src, 
                                                    const WhiteBalanceOptions& options);

// 批量异步
std::vector<std::future<Result<Image>>> processBatchAsync(
    const ImageSequence& images,
    std::function<Result<Image>(const Image&)> processor);
```


## サンプルコード

### 基本的な使い方


```cpp
#include <cvcore/cvcore.h>

using namespace cvcore;

int main() {
    // 加载图像
    auto result = Image::fromFile("input.jpg");
    if (result.second) {
        std::cerr << "Failed to load: " << result.second->message << std::endl;
        return -1;
    }
    Image img = result.first;
    
    // 白平衡
    WhiteBalanceOptions wbOptions;
    wbOptions.redBalance = 1.2;
    wbOptions.blueBalance = 0.9;
    wbOptions.backend = ProcessingBackend::CUDA;  // 指定后端
    
    auto wbResult = adjustWhiteBalance(img, wbOptions);
    if (wbResult.second) {
        std::cerr << "White balance failed" << std::endl;
        return -1;
    }
    
    // 保存
    Error err = io::writeImage("output.jpg", wbResult.first);
    if (err.isFailure()) {
        std::cerr << "Save failed: " << err.message << std::endl;
    }
    
    return 0;
}
```


### 被写界深度の融合


```cpp
// 加载图像序列
std::vector<std::string> files = {"img1.jpg", "img2.jpg", "img3.jpg"};
auto seqResult = ImageSequence::fromFiles(files);
if (seqResult.second) { /* handle error */ }

// 融合
FocusStackingOptions fusionOptions;
fusionOptions.step = 2;
fusionOptions.backend = ProcessingBackend::CUDA;
fusionOptions.useMultiStream = true;

auto fusionResult = focusStacking(seqResult.first, fusionOptions);
if (fusionResult.second) { /* handle error */ }

// 保存结果
io::writeImage("fused.jpg", fusionResult.first);
```


## ファイル構成


```
include/
└── cvcore/
    ├── cvcore_base.h         # 基础类型和宏
    ├── cvcore_image.h        # Image 和 ImageSequence
    ├── cvcore_processing.h   # 处理函数声明
    ├── cvcore_io.h           # I/O 操作
    └── cvcore_cuda.h         # CUDA 特定功能

Core/
├── opencv_helper/
│   ├── cvcore_image.cpp      # Image 实现
│   ├── cvcore_processing.cpp # 处理函数实现
│   └── cvcore_io.cpp         # I/O 实现
└── opencv_cuda/
    ├── cvcore_cuda.cpp       # CUDA 后端实现
    └── cvcore_cuda_kernels.cu # CUDA kernels
```


## 移行ガイド

### 古い API からの移行

|古い API |新しい API |
|----------|----------|
| `CVRead(path)` | `io::readImage(path)` |
| `AdjustWhiteBalance(src, dst, r, g, b)` | `adjustWhiteBalance(src, options)` |
| `Fusion(imgs, step)` | `focusStacking(sequence, options)` |
| `CM_Fusion(json, out)` | `focusStacking(sequence, options)` + 変換 |

### 段階的な移行戦略

1. **フェーズ 1**: 新しいコードは新しい API を使用します
2. **フェーズ 2**: 古い API が内部で新しい API を呼び出します
3. **フェーズ 3**: 古い API 呼び出しを段階的に置き換えます
4. **フェーズ 4**: 古い API を削除します (アダプテーション層を保持します)