# opencv_helper.dll API Documentation

## Overview

`opencv_helper.dll` is a C++ dynamic link library that provides computer vision and image processing functions for the ColorVision WPF application. It serves as a bridge between OpenCV algorithms and C# code via P/Invoke.

---

## Table of Contents

1. [Data Structures](#data-structures)
2. [Image Processing Functions](#image-processing-functions)
3. [SFR (Spatial Frequency Response) Functions](#sfr-spatial-frequency-response-functions)
4. [Focus Evaluation Functions](#focus-evaluation-functions)
5. [Detection Functions](#detection-functions)
6. [Video Processing Functions](#video-processing-functions)
7. [Utility Functions](#utility-functions)
8. [Error Codes](#error-codes)

---

## Data Structures

### HImage

Image data structure used for passing images between C# and C++.

```cpp
struct HImage {
    int rows;           // Image height
    int cols;           // Image width
    int channels;       // Number of channels (1, 3, or 4)
    int depth;          // Bit depth (8, 16, 32)
    int stride;         // Bytes per row
    bool isDispose;     // Whether to dispose memory
    unsigned char* pData;  // Pointer to pixel data
};
```

**C# Equivalent:**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HImage : IDisposable
{
    public int rows;
    public int cols;
    public int channels;
    public int depth;
    public int stride;
    public bool isDispose;
    public IntPtr pData;
    // ... methods
}
```

### RoiRect

Region of Interest rectangle structure.

```cpp
struct RoiRect {
    int x;      // Top-left X coordinate
    int y;      // Top-left Y coordinate
    int width;  // Rectangle width
    int height; // Rectangle height
};
```

**C# Equivalent:**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RoiRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
}
```

### FocusAlgorithm (Enum)

Focus evaluation algorithm types.

```cpp
enum FocusAlgorithm {
    Variance = 0,              // Variance of pixel values
    StandardDeviation = 1,     // Standard deviation
    Tenengrad = 2,             // Tenengrad (Sobel-based)
    Laplacian = 3,             // Laplacian operator
    VarianceOfLaplacian = 4,   // Variance of Laplacian (recommended)
    EnergyOfGradient = 5,      // Energy of gradient
    SpatialFrequency = 6       // Spatial frequency
};
```

### VideoInfo

Video file information structure.

```cpp
struct VideoInfo {
    int totalFrames;    // Total frame count
    double fps;         // Frames per second
    int width;          // Frame width
    int height;         // Frame height
};
```

---

## Image Processing Functions

### M_AutoLevelsAdjust

Automatic levels adjustment using histogram stretching.

```cpp
COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - Input image (must be 3-channel)
- `outImage` - Output image pointer

**Returns:** 0 on success, -1 on error

**C# Usage:**
```csharp
[DllImport(LibPath, CharSet = CharSet.Unicode)]
public static extern int M_AutoLevelsAdjust(HImage image, out HImage hImage);
```

---

### M_AutomaticColorAdjustment

Automatic color balance adjustment in Lab color space.

```cpp
COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - Input image (must be 3-channel)
- `outImage` - Output image pointer

**Returns:** 0 on success, -1 on error

---

### M_AutomaticToneAdjustment

Automatic tone adjustment using histogram clipping.

```cpp
COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - Input image (must be 3-channel)
- `outImage` - Output image pointer

**Returns:** 0 on success, -1 on error

---

### M_PseudoColor

Apply pseudo-color (false color) mapping to grayscale image.

```cpp
COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, 
    uint min, uint max, cv::ColormapTypes types, int channel);
```

**Parameters:**
- `img` - Input image
- `outImage` - Output image pointer
- `min` - Minimum threshold for colormap
- `max` - Maximum threshold for colormap
- `types` - OpenCV colormap type (COLORMAP_JET, COLORMAP_HOT, etc.)
- `channel` - Channel to extract (-1 for grayscale conversion)

**Returns:** 0 on success, -1 on error

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern int M_PseudoColor(HImage image, out HImage hImage, 
    uint min, uint max, ColormapTypes colormapTypes, int channel);
```

---

### M_PseudoColorAutoRange

Apply pseudo-color with automatic range stretching.

```cpp
COLORVISIONCORE_API int M_PseudoColorAutoRange(HImage img, HImage* outImage, 
    uint min, uint max, cv::ColormapTypes types, int channel, 
    uint dataMin, uint dataMax);
```

**Parameters:**
- `dataMin` - Actual data minimum for scaling
- `dataMax` - Actual data maximum for scaling

---

### M_GetMinMax

Get minimum and maximum pixel values from image.

```cpp
COLORVISIONCORE_API int M_GetMinMax(HImage img, uint* outMin, uint* outMax, int channel);
```

**Parameters:**
- `img` - Input image
- `outMin` - Output minimum value
- `outMax` - Output maximum value
- `channel` - Channel to analyze (-1 for all)

**Returns:** 0 on success, -1 on error

---

### M_ExtractChannel

Extract a specific channel from multi-channel image.

```cpp
COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel);
```

**Parameters:**
- `img` - Input image
- `outImage` - Output single-channel image
- `channel` - Channel index to extract (0, 1, 2)

**Returns:** 0 on success, -1 on error

---

### M_GetWhiteBalance

Apply white balance correction with RGB gain factors.

```cpp
COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, 
    double redBalance, double greenBalance, double blueBalance);
```

**Parameters:**
- `redBalance` - Red channel gain
- `greenBalance` - Green channel gain
- `blueBalance` - Blue channel gain

---

### M_ApplyGammaCorrection

Apply gamma correction to image.

```cpp
COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma);
```

**Parameters:**
- `gamma` - Gamma value (typically 0.5-2.5)

---

### M_AdjustBrightnessContrast

Adjust brightness and contrast using linear transformation.

```cpp
COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, 
    double alpha, double beta);
```

**Parameters:**
- `alpha` - Contrast factor (gain)
- `beta` - Brightness offset

**Formula:** `output = alpha * input + beta`

---

### M_InvertImage

Invert image colors (bitwise NOT operation).

```cpp
COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage);
```

---

### M_Threshold

Apply binary threshold to image.

```cpp
COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, 
    double thresh, double maxval, int type);
```

**Parameters:**
- `thresh` - Threshold value
- `maxval` - Maximum value for binary thresholding
- `type` - Threshold type (THRESH_BINARY, THRESH_BINARY_INV, etc.)

---

### M_RemoveMoire

Remove moire patterns from image using multi-scale processing.

```cpp
COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage);
```

**Algorithm:** Gaussian blur → Downsample → Blur → Upsample → Sharpen

---

### M_ConvertImage

Convert image to downsampled grayscale byte array for display.

```cpp
COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, 
    int* length, int* scaleFactout, int targetPixelsX, int targetPixelsY);
```

**Parameters:**
- `rowGrayPixels` - Output byte array pointer (must be freed by caller)
- `length` - Output array length
- `scaleFactout` - Actual scale factor used
- `targetPixelsX/Y` - Target display dimensions

---

### M_ConvertGray32Float

Convert 32-bit float grayscale image to 16-bit.

```cpp
COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage);
```

---

### M_DrawPoiImage

Draw circles at specified points on image.

```cpp
COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage, 
    int radius, int* points, int pointCount, int thickness);
```

**Parameters:**
- `radius` - Circle radius
- `points` - Array of [x1, y1, x2, y2, ...] coordinates
- `pointCount` - Number of points (pairs)
- `thickness` - Line thickness

---

### M_StitchImages

Stitch multiple images horizontally.

```cpp
COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage);
```

**Config JSON Format:**
```json
{"ImageFiles": ["path1.jpg", "path2.jpg", "path3.jpg"]}
```

**Returns:** StitchingErrorCode enum value

---

### M_Fusion

Multi-focus image fusion using focus measure algorithm.

```cpp
COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage);
```

**Config JSON Format:**
```json
["image1.jpg", "image2.jpg", "image3.jpg"]
```

---

## Filter Functions

### M_ApplyGaussianBlur

Apply Gaussian blur filter.

```cpp
COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage, 
    int kernelSize, double sigma);
```

**Parameters:**
- `kernelSize` - Must be odd number (3, 5, 7, ...)
- `sigma` - Standard deviation (0 for auto)

---

### M_ApplyMedianBlur

Apply median blur filter (salt-and-pepper noise removal).

```cpp
COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize);
```

**Parameters:**
- `kernelSize` - Must be odd number

---

### M_ApplySharpen

Apply sharpening filter using Laplacian kernel.

```cpp
COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage);
```

**Kernel:**
```
[ 0 -1  0]
[-1  5 -1]
[ 0 -1  0]
```

---

### M_ApplyCannyEdgeDetection

Apply Canny edge detection.

```cpp
COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage, 
    double threshold1, double threshold2);
```

---

### M_ApplyHistogramEqualization

Apply histogram equalization (grayscale only).

```cpp
COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage);
```

---

## SFR (Spatial Frequency Response) Functions

### M_CalSFRMultiChannel

Calculate SFR using slanted-edge method (ISO 12233) for multiple channels.

```cpp
COLORVISIONCORE_API int M_CalSFRMultiChannel(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,
    double* sfr_r,
    double* sfr_g,
    double* sfr_b,
    double* sfr_l,
    int    maxLen,
    int*   outLen,
    int*   channelCount,
    double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
    double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
    double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
    double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l);
```

**Parameters:**
- `del` - Pixel pitch (micrometers per pixel)
- `roi` - Region of interest containing slanted edge
- `freq` - Output frequency array (cy/pixel)
- `sfr_r/g/b/l` - SFR curves for R/G/B/Luminance channels
- `maxLen` - Maximum output array length
- `outLen` - Actual output length
- `channelCount` - Number of channels calculated (1 or 4)
- `mtf10_norm/mtf50_norm` - Normalized MTF frequencies (0-1)
- `mtf10_cypix/mtf50_cypix` - MTF frequencies in cy/pixel

**Returns:**
- 0 on success
- -1 on parameter error
- -2 on empty image
- -3 on calculation failure

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern int M_CalSFRMultiChannel(
    HImage img, double del, RoiRect roi,
    [Out] double[] freq, [Out] double[] sfr_r, [Out] double[] sfr_g, 
    [Out] double[] sfr_b, [Out] double[] sfr_l,
    int maxLen, out int outLen, out int channelCount,
    out double mtf10_norm_r, out double mtf50_norm_r, 
    out double mtf10_cypix_r, out double mtf50_cypix_r,
    // ... repeat for g, b, l channels
);
```

---

### C++ Interface (Advanced)

```cpp
// Slanted-edge SFR
COLORVISIONCORE_API sfr::SFRResult CalSFR_CPP(const cv::Mat& img,
    double del, int npol, int nbin, double vslope);

// Cylinder target SFR
COLORVISIONCORE_API sfr::CylinderSFRResult CalCylinderSFR_CPP(const cv::Mat& mat,
    int thresh, float roi, float binsize, int n_fit);
```

---

## Focus Evaluation Functions

### M_CalArtculation

Calculate image sharpness/focus measure using various algorithms.

```cpp
COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, RoiRect roi);
```

**Parameters:**
- `img` - Input image
- `type` - Focus algorithm type (see FocusAlgorithm enum)
- `roi` - Region of interest (width/height=0 for full image)

**Returns:** Focus measure value (higher = sharper)

**Algorithms:**

| Type | Description | Best For |
|------|-------------|----------|
| Variance | Variance of pixel values | General purpose |
| StandardDeviation | Std dev of pixel values | General purpose |
| Tenengrad | Sobel gradient magnitude | Edge detection |
| Laplacian | Laplacian operator mean | Fine detail |
| VarianceOfLaplacian | **Recommended** - Variance of Laplacian | General autofocus |
| EnergyOfGradient | Gradient energy | Texture analysis |
| SpatialFrequency | Row/Column frequency | Periodic patterns |

**C# Usage:**
```csharp
[DllImport(LibPath, EntryPoint = "M_CalArtculation", 
    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
public unsafe static extern double M_CalArtculation(
    HImage image, FocusAlgorithm evaFunc, RoiRect roi);
```

---

## Detection Functions

### M_FindLuminousArea

Detect luminous area in image using threshold and contour analysis.

```cpp
COLORVISIONCORE_API int M_FindLuminousArea(HImage img, RoiRect roi, 
    const char* config, char** result);
```

**Config JSON Format:**
```json
{
    "Threshold": -1,        // -1 for auto (Otsu), or specific value
    "UseRotatedRect": false // true for 4 corners, false for bounding box
}
```

**Output JSON Format (UseRotatedRect=false):**
```json
{"X": 100, "Y": 50, "Width": 200, "Height": 150}
```

**Output JSON Format (UseRotatedRect=true):**
```json
{"Corners": [[x1,y1], [x2,y2], [x3,y3], [x4,y4]]}
```

**Returns:** Result string length on success, negative on error

**Memory:** Caller must free result string using `FreeResult()`

---

### M_FindLightBeads

Detect LED/light bead positions in grid pattern.

```cpp
COLORVISIONCORE_API int M_FindLightBeads(HImage img, RoiRect roi, 
    const char* config, char** result);
```

**Config JSON Format:**
```json
{
    "Threshold": 20,    // Detection threshold
    "MinSize": 2,       // Minimum bead size
    "MaxSize": 20,      // Maximum bead size
    "Rows": 650,        // Expected grid rows
    "Cols": 850         // Expected grid columns
}
```

**Output JSON Format:**
```json
{
    "Centers": [[x1,y1], [x2,y2], ...],
    "CenterCount": 100,
    "BlackCenters": [[x3,y3], ...],  // Missing beads
    "BlackCenterCount": 5,
    "ExpectedCount": 552500,
    "MissingCount": 5
}
```

---

### M_DetectKeyRegions

Automatically detect keyboard key regions in image.

```cpp
COLORVISIONCORE_API int M_DetectKeyRegions(HImage img, RoiRect roi, 
    const char* config, char** result);
```

**Config JSON Format:**
```json
{
    "Threshold": -1,       // -1 for Otsu auto-threshold
    "MinArea": 500,        // Minimum key area in pixels
    "MaxArea": 0,          // 0 = auto (25% of image)
    "MarginRatio": 0.05    // Shrink ROI by this margin
}
```

**Output JSON Format:**
```json
{
    "KeyRegions": [
        {"X": 10, "Y": 20, "Width": 50, "Height": 50},
        // ... sorted by row then column
    ],
    "Count": 104
}
```

---

## Video Processing Functions

### M_VideoOpen

Open video file for playback.

```cpp
COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info);
```

**Returns:** Handle (positive) on success, -1 on error

---

### M_VideoReadFrame

Read single frame from video.

```cpp
COLORVISIONCORE_API int M_VideoReadFrame(int handle, HImage* outImage);
```

---

### M_VideoSeek

Seek to specific frame.

```cpp
COLORVISIONCORE_API int M_VideoSeek(int handle, int frameIndex);
```

---

### M_VideoGetCurrentFrame

Get current frame index.

```cpp
COLORVISIONCORE_API int M_VideoGetCurrentFrame(int handle);
```

---

### M_VideoSetPlaybackSpeed

Set playback speed multiplier.

```cpp
COLORVISIONCORE_API int M_VideoSetPlaybackSpeed(int handle, double speed);
```

---

### M_VideoSetResizeScale

Set display resize scale for performance.

```cpp
COLORVISIONCORE_API int M_VideoSetResizeScale(int handle, double scale);
```

**Scale values:** 1.0, 0.5, 0.25, 0.125

---

### M_VideoPlay

Start video playback with callbacks.

```cpp
// Callback types
typedef void (*VideoFrameCallback)(int handle, HImage* frame, 
    int currentFrame, int totalFrames, void* userData);
typedef void (*VideoStatusCallback)(int handle, int status, void* userData);

COLORVISIONCORE_API int M_VideoPlay(int handle, 
    VideoFrameCallback frameCallback,
    VideoStatusCallback statusCallback, 
    void* userData);
```

**Status codes:** 0=Paused, 1=Playing, 2=Ended

---

### M_VideoPause

Pause video playback.

```cpp
COLORVISIONCORE_API int M_VideoPause(int handle);
```

---

### M_VideoClose

Close video and release resources.

```cpp
COLORVISIONCORE_API int M_VideoClose(int handle);
```

---

## Utility Functions

### FreeResult

Free memory allocated for JSON result strings.

```cpp
COLORVISIONCORE_API int FreeResult(char* result);
```

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern void FreeResult(IntPtr str);

// Usage
IntPtr resultPtr;
int len = M_FindLuminousArea(img, roi, config, out resultPtr);
string json = Marshal.PtrToStringAnsi(resultPtr);
FreeResult(resultPtr);  // Must free!
```

---

### FreeHImageData

Free image data allocated by DLL.

```cpp
void FreeHImageData(unsigned char* data);
```

---

## Error Codes

### General Errors

| Code | Meaning |
|------|---------|
| 0 | Success |
| -1 | Invalid parameter or general error |
| -2 | Invalid image data |
| -3 | Memory allocation failed |

### Stitching Errors

```cpp
enum StitchingErrorCode {
    SUCCESS = 0,
    EMPTY_INPUT = 1,
    FILE_NOT_FOUND = 2,
    DIFFERENT_DIMENSIONS = 3,
    NO_VALID_IMAGES = 4
};
```

### SFR Errors

| Code | Meaning |
|------|---------|
| -1 | Null pointer parameter |
| -2 | Empty image |
| -3 | SFR calculation failed |

---

## Thread Safety

- **Video functions:** Thread-safe with internal mutex protection
- **Image processing:** Not thread-safe; process one image at a time per instance
- **SFR calculations:** Thread-safe for independent images

---

## Memory Management

### C# Side Responsibilities

1. **Allocating HImage:** Use `Marshal.AllocHGlobal()` for `pData`
2. **Freeing HImage:** Call `HImage.Dispose()` or `Marshal.FreeHGlobal()`
3. **Freeing Results:** Always call `FreeResult()` for JSON output strings
4. **Freeing Byte Arrays:** Call `M_SetHImageData()` or free manually

### Example Memory Lifecycle

```csharp
// Create HImage
HImage img = new HImage
{
    rows = height,
    cols = width,
    channels = 3,
    depth = 8,
    pData = Marshal.AllocHGlobal(width * height * 3)
};

try
{
    // Copy data to unmanaged memory
    Marshal.Copy(pixelData, 0, img.pData, width * height * 3);
    
    // Process
    HImage outImg;
    M_AutoLevelsAdjust(img, out outImg);
    
    // Use result...
    FreeHImageData(outImg.pData);  // Free output
}
finally
{
    img.Dispose();  // Free input
}
```

---

## Build Information

- **Target:** x64 Windows
- **Runtime Dependencies:** 
  - OpenCV 4.x
  - Visual C++ Redistributable
- **Output Location:** `ColorVision/bin/<Config>/net8.0-windows/opencv_helper.dll`

---

## See Also

- [OpenCV Documentation](https://docs.opencv.org/)
- [ISO 12233 SFR Standard](https://www.iso.org/standard/71616.html)
- ColorVision C# Wrapper: `UI/ColorVision.Core/OpenCVMediaHelper.cs`
