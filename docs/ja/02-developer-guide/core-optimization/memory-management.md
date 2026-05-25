# コアモジュールのメモリ管理最適化ガイド

## 最適化の概要

この最適化には、主に `opencv_helper` プロジェクトに関係する、コア モジュール内の C++ コードのメモリ リークの修復とメモリ管理の改善が含まれます。

## 問題を修正しました

### 1.custom_file.cpp でのメモリ リーク

#### 問題 1: CVWrite 関数の ostream が解放されない
**場所**: `Native/opencv_helper/custom_file.cpp:87-103`

**元のコード**:

```cpp
char* ostream = (char*)malloc(destLen);
int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
// ... 错误检查 ...
grifMat.destLen = destLen;
outFile.write(ostream, grifMat.destLen);
// 缺少 free(ostream)!
```


**修正**: `MallocGuard` クラスを RAII モードで使用してメモリを自動的に管理する

```cpp
MallocGuard streamGuard(ostream);
// ... 使用 ostream ...
// streamGuard 析构时自动调用 free
```


#### 問題 2: CVRead 関数の o2stream が解放されない
**場所**: `Native/opencv_helper/custom_file.cpp:138-143`

**元のコード**:

```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解压 ...
return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
// o2stream 未释放，且 Mat 不拥有该内存!
```


**修正**: clone() を使用してデータをコピーし、元のバッファを解放します。

```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 解压 ...
cv::Mat result(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
cv::Mat cloned = result.clone();  // 复制数据，OpenCV 拥有新内存
free(o2stream);                   // 释放原始缓冲区
return cloned;
```


#### 問題 3: CVRead 関数のデータが解放されない
**場所**: `Native/opencv_helper/custom_file.cpp:147-151`

**元のコード**:

```cpp
char* data = new char[grifMat.srcLen];
inFile.read(data, grifMat.srcLen);
cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
return mat1;  // data 未释放!
```


**修正**: `ArrayGuard` RAII クラスを使用し、返す前にクローンを作成します。

```cpp
ArrayGuard<char> data(new char[grifMat.srcLen]);
inFile.read(data.get(), grifMat.srcLen);
cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
return mat1.clone();  // 复制后返回，原始 data 自动释放
```


### 2. common.cpp のメモリ管理の改善

#### 問題: UTF8ToGB は生のポインターを使用します
**場所**: `Native/opencv_helper/common.cpp:28-46`

**元のコード**:

```cpp
WCHAR* strSrc = new WCHAR[i + 1];
// ... 使用 ...
LPSTR szRes = new CHAR[i + 1];
// ... 使用 ...
delete[] strSrc;
delete[] szRes;
```


**修正**: `std::vector` を使用してメモリを自動的に管理する

```cpp
std::vector<WCHAR> wideBuffer(wideCharLen);
std::vector<char> multiByteBuffer(multiByteLen);
// 自动释放，无需手动 delete
```


## 新しいツール クラス

### 1.MallocGuard
`malloc/free` によって割り当てられたメモリを管理するために使用されます


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


### 2. ArrayGuard\<T\>
`new[]/delete[]` によって割り当てられたメモリを管理するために使用されます


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


## ベストプラクティス

### 1. 標準コンテナの使用を優先する

```cpp
// 推荐
std::vector<char> buffer(size);

// 避免
char* buffer = new char[size];
// ... 可能提前返回，导致内存泄漏
delete[] buffer;
```


### 2. スマート ポインタを使用してリソースを管理する

```cpp
// 推荐
std::unique_ptr<char[]> buffer(new char[size]);

// 或者使用自定义删除器
std::unique_ptr<char, decltype(&free)> buffer(malloc(size), free);
```


### 3. OpenCV Mat のメモリ管理

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


### 4. エラー処理とリソースの解放

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


## ファイル変更リスト

|ファイル |コンテンツを変更する |
|------|----------|
| `Native/include/custom_file.h` |ツール クラス宣言を追加し、インターフェイスを更新します。
| `Native/opencv_helper/custom_file.cpp` |メモリ リークを修正し、RAII ツール クラスを追加 |
| `Native/opencv_helper/common.cpp` | UTF8ToGB メモリ管理を最適化する |

## 検証の提案

1. **Visual Studio メモリ診断ツールを使用する**
   - デバッグ → ウィンドウ → 診断ツールの表示
   - メモリ使用量の曲線が安定しているかどうかを観察します。

2. **アプリケーション検証ツールを使用する**
   - ヒープ検査を有効にする
   - プログラムを実行し、メモリ リーク レポートがあるかどうかを確認します。

3. **コードレビューの重要なポイント**
   - すべての `malloc`/`free` ペアをチェックします
   - すべての `new`/`delete` ペアをチェックします
   - 異常パスのリソース解放を確認する

## フォローアップの最適化の提案

1. 文字列パス操作の代わりに `std::filesystem` の使用を検討してください。
2. より適切なエラーログを追加する
3. 頻繁に行われる小規模なメモリ割り当てを最適化するためにメモリ プールの使用を検討する
4. 大規模な画像処理に対するメモリ使用量の見積もりと制限を追加する