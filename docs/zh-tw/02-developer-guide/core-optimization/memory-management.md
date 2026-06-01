# Core 模組記憶體管理最佳化指南

## 最佳化概述

本次最佳化針對 Core 模組中的 C++ 程式碼進行了記憶體洩漏修復和記憶體管理改進，主要涉及 `opencv_helper` 專案。

## 修復的問題

### 1. custom_file.cpp 中的記憶體洩漏

#### 問題 1: CVWrite 函式中的 ostream 未釋放
**位置**: `Native/opencv_helper/custom_file.cpp:87-103`

**原始程式碼**:
```cpp
char* ostream = (char*)malloc(destLen);
int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
// ... 錯誤檢查 ...
grifMat.destLen = destLen;
outFile.write(ostream, grifMat.destLen);
// 缺少 free(ostream)!
```

**修復方案**: 使用 RAII 模式的 `MallocGuard` 類自動管理記憶體
```cpp
MallocGuard streamGuard(ostream);
// ... 使用 ostream ...
// streamGuard 析構時自動呼叫 free
```

#### 問題 2: CVRead 函式中的 o2stream 未釋放
**位置**: `Native/opencv_helper/custom_file.cpp:138-143`

**原始程式碼**:
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解壓 ...
return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
// o2stream 未釋放，且 Mat 不擁有該記憶體!
```

**修復方案**: 使用 clone() 複製資料，然後釋放原始緩衝區
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解壓 ...
cv::Mat result(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
cv::Mat cloned = result.clone();  // 複製資料，OpenCV 擁有新記憶體
free(o2stream);                   // 釋放原始緩衝區
return cloned;
```

#### 問題 3: CVRead 函式中的 data 未釋放
**位置**: `Native/opencv_helper/custom_file.cpp:147-151`

**原始程式碼**:
```cpp
char* data = new char[grifMat.srcLen];
inFile.read(data, grifMat.srcLen);
cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
return mat1;  // data 未釋放!
```

**修復方案**: 使用 `ArrayGuard` RAII 類，並在返回前 clone
```cpp
ArrayGuard<char> data(new char[grifMat.srcLen]);
inFile.read(data.get(), grifMat.srcLen);
cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
return mat1.clone();  // 複製後返回，原始 data 自動釋放
```

### 2. common.cpp 中的記憶體管理改進

#### 問題: UTF8ToGB 使用裸指標
**位置**: `Native/opencv_helper/common.cpp:28-46`

**原始程式碼**:
```cpp
WCHAR* strSrc = new WCHAR[i + 1];
// ... 使用 ...
LPSTR szRes = new CHAR[i + 1];
// ... 使用 ...
delete[] strSrc;
delete[] szRes;
```

**修復方案**: 使用 `std::vector` 自動管理記憶體
```cpp
std::vector<WCHAR> wideBuffer(wideCharLen);
std::vector<char> multiByteBuffer(multiByteLen);
// 自動釋放，無需手動 delete
```

## 新增的工具類

### 1. MallocGuard
用於管理 `malloc/free` 分配的記憶體

```cpp
class MallocGuard {
    char* m_ptr;
public:
    explicit MallocGuard(char* ptr) : m_ptr(ptr) {}
    ~MallocGuard() { if (m_ptr) free(m_ptr); }
    void release() { m_ptr = nullptr; }
    char* get() const { return m_ptr; }
    // 支援移動語義，禁止複製
};
```

### 2. ArrayGuard\<T\>
用於管理 `new[]/delete[]` 分配的記憶體

```cpp
template<typename T>
class ArrayGuard {
    T* m_ptr;
public:
    explicit ArrayGuard(T* ptr) : m_ptr(ptr) {}
    ~ArrayGuard() { delete[] m_ptr; }
    T* get() const { return m_ptr; }
    T& operator[](size_t idx) { return m_ptr[idx]; }
    // 支援移動語義，禁止複製
};
```

## 最佳實踐

### 1. 優先使用標準容器
```cpp
// 推薦
std::vector<char> buffer(size);

// 避免
char* buffer = new char[size];
// ... 可能提前返回，導致記憶體洩漏
delete[] buffer;
```

### 2. 使用智慧指標管理資源
```cpp
// 推薦
std::unique_ptr<char[]> buffer(new char[size]);

// 或者使用自訂刪除器
std::unique_ptr<char, decltype(&free)> buffer(malloc(size), free);
```

### 3. OpenCV Mat 的記憶體管理
```cpp
// 當使用外部資料建立 Mat 時，Mat 不擁有該記憶體
cv::Mat mat(rows, cols, type, externalData);

// 如果需要 Mat 擁有資料，使用 clone()
cv::Mat owned = mat.clone();

// 或者使用 create + memcpy
cv::Mat owned;
owned.create(rows, cols, type);
memcpy(owned.data, externalData, size);
```

### 4. 錯誤處理與資源釋放
```cpp
// 避免：在錯誤路徑上可能忘記釋放資源
char* buffer = (char*)malloc(size);
if (some_error) {
    return -1;  // 記憶體洩漏!
}
free(buffer);

// 推薦：使用 RAII
MallocGuard buffer((char*)malloc(size));
if (some_error) {
    return -1;  // 自動釋放
}
// 正常流程也會自動釋放
```

## 檔案修改清單

| 檔案 | 修改內容 |
|------|----------|
| `Native/include/custom_file.h` | 新增工具類宣告，更新介面 |
| `Native/opencv_helper/custom_file.cpp` | 修復記憶體洩漏，新增 RAII 工具類 |
| `Native/opencv_helper/common.cpp` | 最佳化 UTF8ToGB 記憶體管理 |

## 驗證建議

1. **使用 Visual Studio 記憶體診斷工具**
   - 除錯 → 視窗 → 顯示診斷工具
   - 觀察記憶體使用曲線是否平穩

2. **使用 Application Verifier**
   - 啟用 Heaps 檢查
   - 執行程式，檢查是否有記憶體洩漏報告

3. **程式碼審查重點**
   - 檢查所有 `malloc`/`free` 配對
   - 檢查所有 `new`/`delete` 配對
   - 檢查異常路徑上的資源釋放

## 後續最佳化建議

1. 考慮使用 `std::filesystem` 替代字串路徑操作
2. 新增更完善的錯誤日誌記錄
3. 考慮使用記憶體池最佳化頻繁的小記憶體分配
4. 對大型影像處理新增記憶體使用預估和限制
