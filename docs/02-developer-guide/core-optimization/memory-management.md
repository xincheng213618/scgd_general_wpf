# Core 模块内存管理优化指南

## 优化概述

本次优化针对 Core 模块中的 C++ 代码进行了内存泄漏修复和内存管理改进，主要涉及 `opencv_helper` 项目。

## 修复的问题

### 1. custom_file.cpp 中的内存泄漏

#### 问题 1: CVWrite 函数中的 ostream 未释放
**位置**: `Core/opencv_helper/custom_file.cpp:87-103`

**原始代码**:
```cpp
char* ostream = (char*)malloc(destLen);
int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
// ... 错误检查 ...
grifMat.destLen = destLen;
outFile.write(ostream, grifMat.destLen);
// 缺少 free(ostream)!
```

**修复方案**: 使用 RAII 模式的 `MallocGuard` 类自动管理内存
```cpp
MallocGuard streamGuard(ostream);
// ... 使用 ostream ...
// streamGuard 析构时自动调用 free
```

#### 问题 2: CVRead 函数中的 o2stream 未释放
**位置**: `Core/opencv_helper/custom_file.cpp:138-143`

**原始代码**:
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解压 ...
return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
// o2stream 未释放，且 Mat 不拥有该内存!
```

**修复方案**: 使用 clone() 复制数据，然后释放原始缓冲区
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解压 ...
cv::Mat result(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
cv::Mat cloned = result.clone();  // 复制数据，OpenCV 拥有新内存
free(o2stream);                   // 释放原始缓冲区
return cloned;
```

#### 问题 3: CVRead 函数中的 data 未释放
**位置**: `Core/opencv_helper/custom_file.cpp:147-151`

**原始代码**:
```cpp
char* data = new char[grifMat.srcLen];
inFile.read(data, grifMat.srcLen);
cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
return mat1;  // data 未释放!
```

**修复方案**: 使用 `ArrayGuard` RAII 类，并在返回前 clone
```cpp
ArrayGuard<char> data(new char[grifMat.srcLen]);
inFile.read(data.get(), grifMat.srcLen);
cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
return mat1.clone();  // 复制后返回，原始 data 自动释放
```

### 2. common.cpp 中的内存管理改进

#### 问题: UTF8ToGB 使用裸指针
**位置**: `Core/opencv_helper/common.cpp:28-46`

**原始代码**:
```cpp
WCHAR* strSrc = new WCHAR[i + 1];
// ... 使用 ...
LPSTR szRes = new CHAR[i + 1];
// ... 使用 ...
delete[] strSrc;
delete[] szRes;
```

**修复方案**: 使用 `std::vector` 自动管理内存
```cpp
std::vector<WCHAR> wideBuffer(wideCharLen);
std::vector<char> multiByteBuffer(multiByteLen);
// 自动释放，无需手动 delete
```

## 新增的工具类

### 1. MallocGuard
用于管理 `malloc/free` 分配的内存

```cpp
class MallocGuard {
    char* m_ptr;
public:
    explicit MallocGuard(char* ptr) : m_ptr(ptr) {}
    ~MallocGuard() { if (m_ptr) free(m_ptr); }
    void release() { m_ptr = nullptr; }
    char* get() const { return m_ptr; }
    // 支持移动语义，禁止拷贝
};
```

### 2. ArrayGuard<T>
用于管理 `new[]/delete[]` 分配的内存

```cpp
template<typename T>
class ArrayGuard {
    T* m_ptr;
public:
    explicit ArrayGuard(T* ptr) : m_ptr(ptr) {}
    ~ArrayGuard() { delete[] m_ptr; }
    T* get() const { return m_ptr; }
    T& operator[](size_t idx) { return m_ptr[idx]; }
    // 支持移动语义，禁止拷贝
};
```

## 最佳实践

### 1. 优先使用标准容器
```cpp
// 推荐
std::vector<char> buffer(size);

// 避免
char* buffer = new char[size];
// ... 可能提前返回，导致内存泄漏
delete[] buffer;
```

### 2. 使用智能指针管理资源
```cpp
// 推荐
std::unique_ptr<char[]> buffer(new char[size]);

// 或者使用自定义删除器
std::unique_ptr<char, decltype(&free)> buffer(malloc(size), free);
```

### 3. OpenCV Mat 的内存管理
```cpp
// 当使用外部数据创建 Mat 时，Mat 不拥有该内存
cv::Mat mat(rows, cols, type, externalData);

// 如果需要 Mat 拥有数据，使用 clone()
cv::Mat owned = mat.clone();

// 或者使用 create + memcpy
cv::Mat owned;
owned.create(rows, cols, type);
memcpy(owned.data, externalData, size);
```

### 4. 错误处理与资源释放
```cpp
// 避免：在错误路径上可能忘记释放资源
char* buffer = (char*)malloc(size);
if (some_error) {
    return -1;  // 内存泄漏!
}
free(buffer);

// 推荐：使用 RAII
MallocGuard buffer((char*)malloc(size));
if (some_error) {
    return -1;  // 自动释放
}
// 正常流程也会自动释放
```

## 文件修改清单

| 文件 | 修改内容 |
|------|----------|
| `include/custom_file.h` | 添加工具类声明，更新接口 |
| `Core/opencv_helper/custom_file.cpp` | 修复内存泄漏，添加 RAII 工具类 |
| `Core/opencv_helper/common.cpp` | 优化 UTF8ToGB 内存管理 |

## 验证建议

1. **使用 Visual Studio 内存诊断工具**
   - 调试 → 窗口 → 显示诊断工具
   - 观察内存使用曲线是否平稳

2. **使用 Application Verifier**
   - 启用 Heaps 检查
   - 运行程序，检查是否有内存泄漏报告

3. **代码审查重点**
   - 检查所有 `malloc`/`free` 配对
   - 检查所有 `new`/`delete` 配对
   - 检查异常路径上的资源释放

## 后续优化建议

1. 考虑使用 `std::filesystem` 替代字符串路径操作
2. 添加更完善的错误日志记录
3. 考虑使用内存池优化频繁的小内存分配
4. 对大型图像处理添加内存使用预估和限制
