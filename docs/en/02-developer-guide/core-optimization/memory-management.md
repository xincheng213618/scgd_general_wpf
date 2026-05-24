# Core Module Memory Management Optimization Guide

## Optimization Overview

This optimization addresses memory leak fixes and memory management improvements for C++ code in the Core module, primarily involving the `opencv_helper` project.

## Fixed Issues

### 1. Memory Leaks in custom_file.cpp

#### Issue 1: ostream not freed in CVWrite function
**Location**: `Native/opencv_helper/custom_file.cpp:87-103`

**Original Code**:
```cpp
char* ostream = (char*)malloc(destLen);
int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
// ... error checking ...
grifMat.destLen = destLen;
outFile.write(ostream, grifMat.destLen);
// Missing free(ostream)!
```

**Fix**: Use RAII `MallocGuard` class for automatic memory management
```cpp
MallocGuard streamGuard(ostream);
// ... use ostream ...
// streamGuard destructor automatically calls free
```

#### Issue 2: o2stream not freed in CVRead function
**Location**: `Native/opencv_helper/custom_file.cpp:138-143`

**Original Code**:
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... decompress ...
return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
// o2stream not freed, and Mat does not own that memory!
```

**Fix**: Use clone() to copy data, then free original buffer
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... decompress ...
cv::Mat result(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
cv::Mat cloned = result.clone();  // Copy data, OpenCV owns new memory
free(o2stream);                   // Free original buffer
return cloned;
```

#### Issue 3: data not freed in CVRead function
**Location**: `Native/opencv_helper/custom_file.cpp:147-151`

**Original Code**:
```cpp
char* data = new char[grifMat.srcLen];
inFile.read(data, grifMat.srcLen);
cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
return mat1;  // data not freed!
```

**Fix**: Use `ArrayGuard` RAII class, clone before returning
```cpp
ArrayGuard<char> data(new char[grifMat.srcLen]);
inFile.read(data.get(), grifMat.srcLen);
cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
return mat1.clone();  // Return clone, original data auto-released
```

### 2. Memory Management Improvements in common.cpp

#### Issue: UTF8ToGB uses raw pointers
**Location**: `Native/opencv_helper/common.cpp:28-46`

**Original Code**:
```cpp
WCHAR* strSrc = new WCHAR[i + 1];
// ... use ...
LPSTR szRes = new CHAR[i + 1];
// ... use ...
delete[] strSrc;
delete[] szRes;
```

**Fix**: Use `std::vector` for automatic memory management
```cpp
std::vector<WCHAR> wideBuffer(wideCharLen);
std::vector<char> multiByteBuffer(multiByteLen);
// Automatic release, no manual delete needed
```

## New Utility Classes

### 1. MallocGuard
Manages memory allocated by `malloc/free`

```cpp
class MallocGuard {
    char* m_ptr;
public:
    explicit MallocGuard(char* ptr) : m_ptr(ptr) {}
    ~MallocGuard() { if (m_ptr) free(m_ptr); }
    void release() { m_ptr = nullptr; }
    char* get() const { return m_ptr; }
    // Supports move semantics, prohibits copy
};
```

### 2. ArrayGuard\<T\>
Manages memory allocated by `new[]/delete[]`

```cpp
template<typename T>
class ArrayGuard {
    T* m_ptr;
public:
    explicit ArrayGuard(T* ptr) : m_ptr(ptr) {}
    ~ArrayGuard() { delete[] m_ptr; }
    T* get() const { return m_ptr; }
    T& operator[](size_t idx) { return m_ptr[idx]; }
    // Supports move semantics, prohibits copy
};
```

## Best Practices

### 1. Prefer Standard Containers
```cpp
// Recommended
std::vector<char> buffer(size);

// Avoid
char* buffer = new char[size];
// ... possible early return, causing memory leak
delete[] buffer;
```

### 2. Use Smart Pointers for Resource Management
```cpp
// Recommended
std::unique_ptr<char[]> buffer(new char[size]);

// Or use custom deleter
std::unique_ptr<char, decltype(&free)> buffer(malloc(size), free);
```

### 3. OpenCV Mat Memory Management
```cpp
// When creating Mat from external data, Mat does not own the memory
cv::Mat mat(rows, cols, type, externalData);

// If Mat needs to own data, use clone()
cv::Mat owned = mat.clone();

// Or use create + memcpy
cv::Mat owned;
owned.create(rows, cols, type);
memcpy(owned.data, externalData, size);
```

### 4. Error Handling and Resource Release
```cpp
// Avoid: may forget to release resources on error path
char* buffer = (char*)malloc(size);
if (some_error) {
    return -1;  // Memory leak!
}
free(buffer);

// Recommended: use RAII
MallocGuard buffer((char*)malloc(size));
if (some_error) {
    return -1;  // Automatic release
}
// Normal flow also auto-releases
```

## File Modification List

| File | Modification |
|------|----------|
| `Native/include/custom_file.h` | Add utility class declarations, update interface |
| `Native/opencv_helper/custom_file.cpp` | Fix memory leaks, add RAII utility classes |
| `Native/opencv_helper/common.cpp` | Optimize UTF8ToGB memory management |

## Verification Recommendations

1. **Use Visual Studio Diagnostic Tools**
   - Debug → Windows → Show Diagnostic Tools
   - Observe whether memory usage curve is stable

2. **Use Application Verifier**
   - Enable Heaps checking
   - Run program, check for memory leak reports

3. **Code Review Focus Areas**
   - Check all `malloc`/`free` pairs
   - Check all `new`/`delete` pairs
   - Check resource release on exception paths

## Subsequent Optimization Suggestions

1. Consider using `std::filesystem` to replace string path operations
2. Add more comprehensive error logging
3. Consider using memory pools to optimize frequent small memory allocations
4. Add memory usage estimation and limits for large image processing