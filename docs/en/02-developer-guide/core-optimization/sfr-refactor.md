# SFR Module Refactoring Completion Document

## Refactoring Overview

The SFR module has been refactored from a scattered three-layer structure to a centralized structure, aligned with the Core module's unified architecture design.

## Directory Structure Changes

### Before Refactoring
```
packages/sfr/                  # Scattered algorithm library
├── include/sfr/
│   ├── general.h
│   ├── slanted.h
│   └── cylinder.h
└── src/
    ├── general.cpp
    ├── slanted.cpp
    └── cylinder.cpp

include/
└── opencv_media_export.h      # C interface declarations

Core/opencv_helper/
└── opencv_media_export.cpp    # C interface implementation (calls sfr)
```

### After Refactoring
```
Core/opencv_helper/
├── algorithm/
│   └── sfr/                   # SFR algorithm implementation
│       ├── sfr_base.h/.cpp    # Base utility functions (formerly general)
│       ├── sfr_slanted.h/.cpp # Slanted edge SFR (formerly slanted)
│       └── sfr_cylinder.h/.cpp# Cylinder SFR (formerly cylinder)
│
├── include/cvcore/
│   └── sfr.h                  # Unified interface header
│
└── exports/
    └── sfr_export.cpp         # C interface export implementation

packages/sfr/                  # Marked as deprecated (can be deleted)
```

## Namespace Changes

| Old Namespace | New Namespace | Description |
|-----------|-----------|------|
| `::sfr` | `cvcore::sfr` | Unified into cvcore namespace |

### Backward Compatibility
```cpp
// Alias provided in sfr_base.h
namespace sfr = cvcore::sfr;
```

## API Changes

### C++ Interface

| Old API | New API | Description |
|--------|--------|------|
| `sfr::CalSFR()` | `cvcore::sfr::calculateSlantedEdgeSFR()` | New name is clearer |
| `sfr::circle` | `cvcore::sfr::Circle` | Type name capitalized |
| `sfr::circle_fit()` | `cvcore::sfr::fitCircle()` | Verb-first naming |
| `sfr::esf()` | `cvcore::sfr::cylinderESF()` | Distinguish methods |
| `sfr::lsf()` | `cvcore::sfr::cylinderLSF()` | Distinguish methods |
| `sfr::mtf()` | `cvcore::sfr::cylinderMTF()` | Distinguish methods |

### New C++ Interfaces
```cpp
// Structured result return
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

// New main functions
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

### C Interface (Unchanged)
```cpp
COLORVISIONCORE_API int M_CalSFR(...);
COLORVISIONCORE_API int M_CalSFRMultiChannel(...);
```

## File Mapping

| Original File | New File | Description |
|--------|--------|------|
| `packages/sfr/include/sfr/general.h` | `Core/opencv_helper/algorithm/sfr/sfr_base.h` | Renamed |
| `packages/sfr/include/sfr/slanted.h` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.h` | Renamed |
| `packages/sfr/include/sfr/cylinder.h` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.h` | Renamed |
| `packages/sfr/src/general.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_base.cpp` | Renamed |
| `packages/sfr/src/slanted.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_slanted.cpp` | Renamed |
| `packages/sfr/src/cylinder.cpp` | `Core/opencv_helper/algorithm/sfr/sfr_cylinder.cpp` | Renamed |

## Build Configuration Updates

### vcxproj File Modifications
Need to update `Core/opencv_helper/opencv_helper.vcxproj`:

1. **Add Include Directories**
```xml
<AdditionalIncludeDirectories>
  $(ProjectDir)/include/cvcore;
  $(ProjectDir)/algorithm/sfr;
  %(AdditionalIncludeDirectories)
</AdditionalIncludeDirectories>
```

2. **Add Source Files**
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

3. **Remove Old References**
```xml
<!-- Remove the following -->
<!-- <ClCompile Include="..\..\packages\sfr\src\*.cpp" /> -->
```

### CMakeLists.txt (if using CMake)
```cmake
# SFR Module
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

## Migration Steps

1. **Backup Existing Code**
```bash
git checkout -b sfr-refactor-backup
git add .
git commit -m "Backup before SFR refactor"
```

2. **Verify New Code Compiles**
```bash
# Clean old build cache
dotnet clean

# Rebuild
dotnet build
```

3. **Run Tests**
```bash
# Run SFR-related tests
# Ensure M_CalSFR and M_CalSFRMultiChannel work correctly
```

4. **Delete Old Files**
```bash
# After confirming new code works, delete old directory
rm -rf packages/sfr/
```

## Code Examples

### Using New C++ Interface
```cpp
#include <cvcore/sfr.h>

using namespace cvcore;

// Slanted edge SFR
cv::Mat img = cv::imread("edge.png", cv::IMREAD_GRAYSCALE);
auto result = sfr::calculateSlantedEdgeSFR(img, 1.0, 5, 4);

if (result.isValid()) {
    std::cout << "MTF50: " << result.mtf50_cypix << " cy/pix" << std::endl;
}

// Cylinder SFR
cv::Mat cylinder_img = cv::imread("circle.png", cv::IMREAD_GRAYSCALE);
auto cyl_result = sfr::calculateCylinderSFR(cylinder_img, 80, 15.0f);

if (cyl_result.isValid()) {
    std::cout << "MTF10: " << cyl_result.mtf10 << std::endl;
}
```

### Backward Compatibility (Legacy Code)
```cpp
// Can still use old namespace
#include <cvcore/sfr.h>

// Use old calling convention
sfr::SFRResult result = sfr::CalSFR(img, 1.0, 5, 4, -1);
```

## Advantages

1. **Clear Structure**: All SFR-related code centralized in one directory
2. **Consistent Naming**: Unified use of `cvcore::` namespace
3. **Unified Interface**: Consistent structure with other algorithm modules (e.g., fusion)
4. **Easy Maintenance**: Modify SFR in one place only
5. **Backward Compatible**: Preserves old API, smooth migration

## Notes

1. **Eigen Dependency**: Ensure project can still access Eigen library
2. **OpenCV Version**: Requires OpenCV 4.x
3. **Compiler**: Requires C++17 support