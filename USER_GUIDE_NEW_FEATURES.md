# User Guide - New Image Algorithm Features

## Overview

This guide explains the new features added to the ColorVision Image Editor's algorithm menu.

## How to Access

1. Open an image in the ColorVision Image Editor
2. Right-click on the image
3. Navigate to **图像算法 (Image Algorithms)** menu

## New Features

### 1. White Balance - Now Context-Aware ✨

**What Changed**: 
- White Balance is now **automatically disabled** for grayscale (single-channel) images
- It only appears enabled for color (multi-channel) images

**Why**: 
- White balance adjusts RGB color channels
- It doesn't make sense for grayscale images (which have only one channel)

**How to Test**:
- Open a grayscale image → White Balance menu item is grayed out
- Open a color image → White Balance menu item is clickable

---

### 2. Six New Image Processing Algorithms

#### 🔪 Sharpen (锐化)
- **What it does**: Makes edges and details in your image more crisp
- **How to use**: Click and it applies immediately
- **Best for**: Photos that look slightly blurry or soft

#### 🌫️ Gaussian Blur (高斯模糊)
- **What it does**: Smooths the image by blurring
- **How to use**: 
  1. Click to open adjustment window
  2. Adjust "核大小" (Kernel Size): 1-31 (larger = more blur)
  3. Adjust "Sigma": 0.1-10 (affects blur quality)
  4. See live preview as you adjust
  5. Click "应用" to apply or "取消" to cancel
- **Best for**: Reducing noise, creating artistic effects

#### 🎯 Median Blur (中值滤波)
- **What it does**: Removes "salt and pepper" noise (random black/white dots)
- **How to use**:
  1. Click to open adjustment window
  2. Adjust "核大小" (Kernel Size): 1-15
  3. See live preview
  4. Click "应用" to apply or "取消" to cancel
- **Best for**: Cleaning up noisy images while preserving edges

#### 📐 Edge Detection - Canny (边缘检测)
- **What it does**: Detects and highlights edges in the image
- **How to use**:
  1. Click to open adjustment window
  2. Adjust "低阈值" (Low Threshold): 0-255
  3. Adjust "高阈值" (High Threshold): 0-255
  4. See live preview
  5. Click "应用" to apply or "取消" to cancel
- **Best for**: Computer vision tasks, artistic edge effects
- **Tip**: Keep High Threshold about 2-3x the Low Threshold

#### 📊 Histogram Equalization (直方图均衡化)
- **What it does**: Improves contrast by spreading out intensity values
- **How to use**: Click and it applies immediately
- **Best for**: Low-contrast images, medical images, grayscale photos

#### 🌀 Remove Moire (去除摩尔纹)
- **What it does**: Removes moire patterns (interference patterns)
- **How to use**: Click and it applies immediately
- **Best for**: Photos of screens, scanned images with patterns

---

## Menu Organization

The Image Algorithms menu now contains (in order):

1. 反相 (Invert)
2. 自动色阶调整 (Auto Levels Adjust)
3. 白平衡调整 (White Balance) - *Now context-aware!*
4. 伽马校正 (Gamma Correction)
5. 亮度对比度调整 (Brightness & Contrast)
6. 阈值处理 (Threshold)
7. 去除摩尔纹 (Remove Moire) - *NEW!*
8. 锐化 (Sharpen) - *NEW!*
9. 高斯模糊 (Gaussian Blur) - *NEW!*
10. 中值滤波 (Median Blur) - *NEW!*
11. 边缘检测 (Edge Detection) - *NEW!*
12. 直方图均衡化 (Histogram Equalization) - *NEW!*

---

## Tips for Best Results

### Sharpen
- Don't over-sharpen - it can make images look unnatural
- Apply after resizing images

### Gaussian Blur
- Start with kernel size 5 and sigma 1.5
- Larger kernel sizes give more blur but are slower
- Good for background blur effects

### Median Blur
- Works best with small kernel sizes (3-7)
- Excellent for removing noise while keeping edges sharp
- Too large kernel size can make image look "plastic"

### Edge Detection
- For most images: Low threshold ~50, High threshold ~150
- Adjust thresholds based on image complexity
- Lower thresholds detect more edges
- The result is a binary (black and white) edge map

### Histogram Equalization
- Most effective on grayscale images
- Can make colors look unnatural on color images
- Great for improving visibility in dark images

### Remove Moire
- Specifically designed for moire patterns
- May slightly soften the image
- Most useful for photos of monitors/screens

---

## Workflow Examples

### Example 1: Enhance a Blurry Photo
1. Open image
2. Apply **锐化 (Sharpen)**
3. If still blurry, adjust brightness/contrast

### Example 2: Clean Noisy Image
1. Open image
2. Apply **中值滤波 (Median Blur)** with kernel size 3-5
3. If needed, apply **锐化 (Sharpen)** to restore detail

### Example 3: Create Edge Art
1. Open image
2. Apply **边缘检测 (Edge Detection)**
3. Adjust thresholds until you like the result
4. Result is artistic line drawing of original image

### Example 4: Fix Low Contrast
1. Open image
2. Try **直方图均衡化 (Histogram Equalization)**
3. If colors look bad, undo and use **自动色阶调整** instead

---

## Technical Notes

- All adjustable algorithms show **real-time preview**
- Preview updates are debounced (30-50ms delay) for smooth performance
- **Apply** saves changes to the image
- **Cancel** reverts to original
- Direct-apply algorithms can be undone using standard undo (if available)

---

## Troubleshooting

**Q: White Balance is grayed out, but I have a color image**
A: Check if your image might be saved as grayscale. Try converting it to RGB first.

**Q: Algorithms run slowly on my large image**
A: This is normal. Processing time increases with image size. Consider resizing very large images first.

**Q: Edge Detection gives me all white or all black**
A: Adjust your thresholds. All black = thresholds too high, All white = thresholds too low.

**Q: After Gaussian Blur, my image looks too soft**
A: Reduce the kernel size and/or sigma value. Try starting with kernel=5, sigma=1.0.

---

## Feedback

If you encounter any issues or have suggestions for additional algorithms, please report them in the project's issue tracker.

Enjoy the new image processing capabilities! 🎨
