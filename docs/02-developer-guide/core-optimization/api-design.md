# Core 模块统一接口设计规范

## 设计目标

1. **一致性**: 所有图像处理函数遵循统一的调用约定
2. **可扩展性**: 易于添加新的处理算法和后端
3. **类型安全**: 使用强类型避免运行时错误
4. **性能**: 零开销抽象，支持多后端
5. **可维护性**: 清晰的错误处理和日志

## 架构概览

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

## 核心类型

### 1. Image 类

封装 `cv::Mat`，提供统一接口：

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

### 2. Result<T> 类型

用于错误处理：

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

### 3. ProcessingOptions

所有处理函数的选项基类：

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

## 处理函数规范

### 命名规范

| 操作类型 | 前缀 | 示例 |
|----------|------|------|
| 颜色调整 | `adjust` | `adjustWhiteBalance` |
| 滤镜 | `apply` | `applyGaussianBlur` |
| 自动处理 | `auto` | `autoLevels` |
| 检测 | `find`/`detect` | `findLightBeads` |
| 转换 | `to`/`convert` | `toGray`, `convertDepth` |

### 参数顺序

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

### 错误处理

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

## 后端选择

### 自动选择逻辑

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

### 后端特定实现

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

## 向后兼容性

### 保留旧接口

```cpp
// 新接口
CV_CORE_API Result<Image> focusStacking(const ImageSequence& images, 
                                        const FocusStackingOptions& options);

// 旧接口 (保留，内部调用新接口)
COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
```

### 适配层

```cpp
// 旧类型到新类型的转换
Image convertFromHImage(const HImage* hImg);
Error convertToHImage(const Image& img, HImage* out);
```

## 性能考虑

### 1. 移动语义

```cpp
// 避免拷贝
Image process(Image&& input) {
    // 直接修改输入，避免分配新内存
    // ...
    return std::move(input);
}
```

### 2. 内存池

```cpp
class ProcessingContext {
public:
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;
    
    cv::Mat acquireBuffer(const ImageDescriptor& desc);
    void releaseBuffer(cv::Mat& buffer);
};
```

### 3. 异步处理

```cpp
// 异步接口
std::future<Result<Image>> adjustWhiteBalanceAsync(const Image& src, 
                                                    const WhiteBalanceOptions& options);

// 批量异步
std::vector<std::future<Result<Image>>> processBatchAsync(
    const ImageSequence& images,
    std::function<Result<Image>(const Image&)> processor);
```

## 示例代码

### 基本使用

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

### 景深融合

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

## 文件组织

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

## 迁移指南

### 从旧 API 迁移

| 旧 API | 新 API |
|--------|--------|
| `CVRead(path)` | `io::readImage(path)` |
| `AdjustWhiteBalance(src, dst, r, g, b)` | `adjustWhiteBalance(src, options)` |
| `Fusion(imgs, step)` | `focusStacking(sequence, options)` |
| `CM_Fusion(json, out)` | `focusStacking(sequence, options)` + 转换 |

### 逐步迁移策略

1. **Phase 1**: 新代码使用新 API
2. **Phase 2**: 旧 API 内部调用新 API
3. **Phase 3**: 逐步替换旧 API 调用
4. **Phase 4**: 移除旧 API (保留适配层)
