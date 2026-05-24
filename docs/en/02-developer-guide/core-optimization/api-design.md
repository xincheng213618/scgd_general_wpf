# Core Module Unified Interface Design Specification

## Design Goals

1. **Consistency**: All image processing functions follow a unified calling convention
2. **Extensibility**: Easy to add new processing algorithms and backends
3. **Type Safety**: Use strong typing to avoid runtime errors
4. **Performance**: Zero-overhead abstraction, supporting multiple backends
5. **Maintainability**: Clear error handling and logging

## Architecture Overview

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

## Core Types

### 1. Image Class

Encapsulates `cv::Mat`, providing a unified interface:

```cpp
namespace cvcore {

class Image {
public:
    // Construction
    Image();
    explicit Image(const cv::Mat& mat);
    Image(int width, int height, int channels, int depth);
    
    // Factory methods
    static Result<Image> fromFile(const std::string& path);
    static Image zeros(const ImageDescriptor& desc);
    
    // Accessors
    int width() const;
    int height() const;
    int channels() const;
    bool empty() const;
    
    // Conversion
    cv::Mat& mat();
    Result<Image> toGray() const;
    Result<Image> convertDepth(int newDepth) const;
    
    // Operations
    Image clone() const;
    Image roi(int x, int y, int w, int h) const;
};

} // namespace cvcore
```

### 2. Result\<T\> Type

Used for error handling:

```cpp
template<typename T>
using Result = std::pair<T, std::optional<Error>>;

// Usage example
auto result = cvcore::adjustWhiteBalance(image, options);
if (result.second) {
    // Error handling
    std::cerr << "Error: " << result.second->message << std::endl;
    return;
}
Image output = result.first;
```

### 3. ProcessingOptions

Base class for all processing function options:

```cpp
struct ProcessingOptions {
    ProcessingBackend backend = ProcessingBackend::Auto;
    ProcessingContext* context = nullptr;
    bool async = false;
    ProgressCallback progressCallback;
};

// Derived options
struct WhiteBalanceOptions : ProcessingOptions {
    double redBalance = 1.0;
    double greenBalance = 1.0;
    double blueBalance = 1.0;
};
```

## Processing Function Specification

### Naming Convention

| Operation Type | Prefix | Example |
|----------|------|------|
| Color adjustment | `adjust` | `adjustWhiteBalance` |
| Filter | `apply` | `applyGaussianBlur` |
| Auto processing | `auto` | `autoLevels` |
| Detection | `find`/`detect` | `findLightBeads` |
| Conversion | `to`/`convert` | `toGray`, `convertDepth` |

### Parameter Order

```cpp
Result<Image> functionName(
    const Image& src,           // 1. Input image (const reference)
    const SpecificOptions& options  // 2. Options (const reference)
);

// Variant with extra parameters
Result<Image> functionName(
    const Image& src,
    double param1,              // 3. Primary parameter
    int param2,                 // 4. Secondary parameter
    const ProcessingOptions& options  // 5. Options
);
```

### Error Handling

```cpp
// Error code definitions
enum class ErrorCode {
    Success = 0,
    InvalidParameter = -1,
    InvalidImage = -2,
    UnsupportedFormat = -3,
    OutOfMemory = -4,
    // ...
};

// Error information
struct Error {
    ErrorCode code;
    std::string message;
    std::string function;
    std::string file;
    int line;
};

// Convenience macro
#define CVCORE_CHECK_IMAGE(img) \
    do { \
        if (img.empty()) { \
            return { {}, std::make_optional<Error>( \
                ErrorCode::InvalidImage, "Empty image", \
                __FUNCTION__, __FILE__, __LINE__) }; \
        } \
    } while(0)
```

## Backend Selection

### Automatic Selection Logic

```cpp
ProcessingBackend selectBackend(ProcessingBackend requested, const Image& img) {
    if (requested != ProcessingBackend::Auto) {
        return requested;
    }
    
    // Prefer CUDA (if available and image is large enough)
    #ifdef HAVE_CUDA
    if (cuda::isAvailable() && img.sizeBytes() > 10 * 1024 * 1024) {
        return ProcessingBackend::CUDA;
    }
    #endif
    
    // Then OpenCL
    #ifdef HAVE_OPENCL
    if (ocl::isAvailable()) {
        return ProcessingBackend::OpenCL;
    }
    #endif
    
    // Default CPU
    return ProcessingBackend::CPU;
}
```

### Backend-Specific Implementation

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

## Backward Compatibility

### Preserve Old Interfaces

```cpp
// New interface
CV_CORE_API Result<Image> focusStacking(const ImageSequence& images, 
                                        const FocusStackingOptions& options);

// Old interface (preserved, internally calls new interface)
COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
```

### Adaptation Layer

```cpp
// Old type to new type conversion
Image convertFromHImage(const HImage* hImg);
Error convertToHImage(const Image& img, HImage* out);
```

## Performance Considerations

### 1. Move Semantics

```cpp
// Avoid copying
Image process(Image&& input) {
    // Directly modify input, avoid allocating new memory
    // ...
    return std::move(input);
}
```

### 2. Memory Pool

```cpp
class ProcessingContext {
public:
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;
    
    cv::Mat acquireBuffer(const ImageDescriptor& desc);
    void releaseBuffer(cv::Mat& buffer);
};
```

### 3. Async Processing

```cpp
// Async interface
std::future<Result<Image>> adjustWhiteBalanceAsync(const Image& src, 
                                                    const WhiteBalanceOptions& options);

// Batch async
std::vector<std::future<Result<Image>>> processBatchAsync(
    const ImageSequence& images,
    std::function<Result<Image>(const Image&)> processor);
```

## Example Code

### Basic Usage

```cpp
#include <cvcore/cvcore.h>

using namespace cvcore;

int main() {
    // Load image
    auto result = Image::fromFile("input.jpg");
    if (result.second) {
        std::cerr << "Failed to load: " << result.second->message << std::endl;
        return -1;
    }
    Image img = result.first;
    
    // White balance
    WhiteBalanceOptions wbOptions;
    wbOptions.redBalance = 1.2;
    wbOptions.blueBalance = 0.9;
    wbOptions.backend = ProcessingBackend::CUDA;  // Specify backend
    
    auto wbResult = adjustWhiteBalance(img, wbOptions);
    if (wbResult.second) {
        std::cerr << "White balance failed" << std::endl;
        return -1;
    }
    
    // Save
    Error err = io::writeImage("output.jpg", wbResult.first);
    if (err.isFailure()) {
        std::cerr << "Save failed: " << err.message << std::endl;
    }
    
    return 0;
}
```

### Focus Stacking

```cpp
// Load image sequence
std::vector<std::string> files = {"img1.jpg", "img2.jpg", "img3.jpg"};
auto seqResult = ImageSequence::fromFiles(files);
if (seqResult.second) { /* handle error */ }

// Fusion
FocusStackingOptions fusionOptions;
fusionOptions.step = 2;
fusionOptions.backend = ProcessingBackend::CUDA;
fusionOptions.useMultiStream = true;

auto fusionResult = focusStacking(seqResult.first, fusionOptions);
if (fusionResult.second) { /* handle error */ }

// Save result
io::writeImage("fused.jpg", fusionResult.first);
```

## File Organization

```
include/
└── cvcore/
    ├── cvcore_base.h         # Base types and macros
    ├── cvcore_image.h        # Image and ImageSequence
    ├── cvcore_processing.h   # Processing function declarations
    ├── cvcore_io.h           # I/O operations
    └── cvcore_cuda.h         # CUDA-specific features

Core/
├── opencv_helper/
│   ├── cvcore_image.cpp      # Image implementation
│   ├── cvcore_processing.cpp # Processing function implementation
│   └── cvcore_io.cpp         # I/O implementation
└── opencv_cuda/
    ├── cvcore_cuda.cpp       # CUDA backend implementation
    └── cvcore_cuda_kernels.cu # CUDA kernels
```

## Migration Guide

### Migrating from Old API

| Old API | New API |
|--------|--------|
| `CVRead(path)` | `io::readImage(path)` |
| `AdjustWhiteBalance(src, dst, r, g, b)` | `adjustWhiteBalance(src, options)` |
| `Fusion(imgs, step)` | `focusStacking(sequence, options)` |
| `CM_Fusion(json, out)` | `focusStacking(sequence, options)` + conversion |

### Gradual Migration Strategy

1. **Phase 1**: New code uses new API
2. **Phase 2**: Old API internally calls new API
3. **Phase 3**: Gradually replace old API calls
4. **Phase 4**: Remove old API (keep adaptation layer)