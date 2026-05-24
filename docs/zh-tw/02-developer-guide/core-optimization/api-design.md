# Core 模組統一介面設計規範

## 設計目標

1. **一致性**: 所有影像處理函式遵循統一的呼叫約定
2. **可擴充套件性**: 易於新增新的處理演算法和後端
3. **型別安全**: 使用強型別避免執行時錯誤
4. **效能**: 零開銷抽象，支援多後端
5. **可維護性**: 清晰的錯誤處理和日誌

## 架構概覽

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

## 核心型別

### 1. Image 類

封裝 `cv::Mat`，提供統一介面：

```cpp
namespace cvcore {

class Image {
public:
    // 構造
    Image();
    explicit Image(const cv::Mat& mat);
    Image(int width, int height, int channels, int depth);
    
    // 工廠方法
    static Result<Image> fromFile(const std::string& path);
    static Image zeros(const ImageDescriptor& desc);
    
    // 訪問器
    int width() const;
    int height() const;
    int channels() const;
    bool empty() const;
    
    // 轉換
    cv::Mat& mat();
    Result<Image> toGray() const;
    Result<Image> convertDepth(int newDepth) const;
    
    // 操作
    Image clone() const;
    Image roi(int x, int y, int w, int h) const;
};

} // namespace cvcore
```

### 2. Result\<T\> 型別

用於錯誤處理：

```cpp
template<typename T>
using Result = std::pair<T, std::optional<Error>>;

// 使用示例
auto result = cvcore::adjustWhiteBalance(image, options);
if (result.second) {
    // 錯誤處理
    std::cerr << "Error: " << result.second->message << std::endl;
    return;
}
Image output = result.first;
```

### 3. ProcessingOptions

所有處理函式的選項基類：

```cpp
struct ProcessingOptions {
    ProcessingBackend backend = ProcessingBackend::Auto;
    ProcessingContext* context = nullptr;
    bool async = false;
    ProgressCallback progressCallback;
};

// 派生選項
struct WhiteBalanceOptions : ProcessingOptions {
    double redBalance = 1.0;
    double greenBalance = 1.0;
    double blueBalance = 1.0;
};
```

## 處理函式規範

### 命名規範

| 操作型別 | 字首 | 示例 |
|----------|------|------|
| 顏色調整 | `adjust` | `adjustWhiteBalance` |
| 濾鏡 | `apply` | `applyGaussianBlur` |
| 自動處理 | `auto` | `autoLevels` |
| 檢測 | `find`/`detect` | `findLightBeads` |
| 轉換 | `to`/`convert` | `toGray`, `convertDepth` |

### 參數順序

```cpp
Result<Image> functionName(
    const Image& src,           // 1. 輸入影像 (const引用)
    const SpecificOptions& options  // 2. 選項 (const引用)
);

// 帶額外參數的變體
Result<Image> functionName(
    const Image& src,
    double param1,              // 3. 主要參數
    int param2,                 // 4. 次要參數
    const ProcessingOptions& options  // 5. 選項
);
```

### 錯誤處理

```cpp
// 錯誤碼定義
enum class ErrorCode {
    Success = 0,
    InvalidParameter = -1,
    InvalidImage = -2,
    UnsupportedFormat = -3,
    OutOfMemory = -4,
    // ...
};

// 錯誤資訊
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

## 後端選擇

### 自動選擇邏輯

```cpp
ProcessingBackend selectBackend(ProcessingBackend requested, const Image& img) {
    if (requested != ProcessingBackend::Auto) {
        return requested;
    }
    
    // 優先 CUDA (如果可用且影像足夠大)
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
    
    // 預設 CPU
    return ProcessingBackend::CPU;
}
```

### 後端特定實現

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

## 向後相容性

### 保留舊介面

```cpp
// 新介面
CV_CORE_API Result<Image> focusStacking(const ImageSequence& images, 
                                        const FocusStackingOptions& options);

// 舊介面 (保留，內部呼叫新介面)
COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
```

### 適配層

```cpp
// 舊型別到新型別的轉換
Image convertFromHImage(const HImage* hImg);
Error convertToHImage(const Image& img, HImage* out);
```

## 效能考慮

### 1. 移動語義

```cpp
// 避免複製
Image process(Image&& input) {
    // 直接修改輸入，避免分配新記憶體
    // ...
    return std::move(input);
}
```

### 2. 記憶體池

```cpp
class ProcessingContext {
public:
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;
    
    cv::Mat acquireBuffer(const ImageDescriptor& desc);
    void releaseBuffer(cv::Mat& buffer);
};
```

### 3. 非同步處理

```cpp
// 非同步介面
std::future<Result<Image>> adjustWhiteBalanceAsync(const Image& src, 
                                                    const WhiteBalanceOptions& options);

// 批次非同步
std::vector<std::future<Result<Image>>> processBatchAsync(
    const ImageSequence& images,
    std::function<Result<Image>(const Image&)> processor);
```

## 示例程式碼

### 基本使用

```cpp
#include <cvcore/cvcore.h>

using namespace cvcore;

int main() {
    // 載入影像
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
    wbOptions.backend = ProcessingBackend::CUDA;  // 指定後端
    
    auto wbResult = adjustWhiteBalance(img, wbOptions);
    if (wbResult.second) {
        std::cerr << "White balance failed" << std::endl;
        return -1;
    }
    
    // 儲存
    Error err = io::writeImage("output.jpg", wbResult.first);
    if (err.isFailure()) {
        std::cerr << "Save failed: " << err.message << std::endl;
    }
    
    return 0;
}
```

### 景深融合

```cpp
// 載入影像序列
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

// 儲存結果
io::writeImage("fused.jpg", fusionResult.first);
```

## 檔案組織

```
include/
└── cvcore/
    ├── cvcore_base.h         # 基礎型別和宏
    ├── cvcore_image.h        # Image 和 ImageSequence
    ├── cvcore_processing.h   # 處理函式宣告
    ├── cvcore_io.h           # I/O 操作
    └── cvcore_cuda.h         # CUDA 特定功能

Core/
├── opencv_helper/
│   ├── cvcore_image.cpp      # Image 實現
│   ├── cvcore_processing.cpp # 處理函式實現
│   └── cvcore_io.cpp         # I/O 實現
└── opencv_cuda/
    ├── cvcore_cuda.cpp       # CUDA 後端實現
    └── cvcore_cuda_kernels.cu # CUDA kernels
```

## 遷移指南

### 從舊 API 遷移

| 舊 API | 新 API |
|--------|--------|
| `CVRead(path)` | `io::readImage(path)` |
| `AdjustWhiteBalance(src, dst, r, g, b)` | `adjustWhiteBalance(src, options)` |
| `Fusion(imgs, step)` | `focusStacking(sequence, options)` |
| `CM_Fusion(json, out)` | `focusStacking(sequence, options)` + 轉換 |

### 逐步遷移策略

1. **Phase 1**: 新程式碼使用新 API
2. **Phase 2**: 舊 API 內部呼叫新 API
3. **Phase 3**: 逐步替換舊 API 呼叫
4. **Phase 4**: 移除舊 API (保留適配層)
