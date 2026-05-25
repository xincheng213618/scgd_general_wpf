# 코어 모듈 메모리 관리 최적화 가이드

## 최적화 개요

이 최적화에는 주로 `opencv_helper` 프로젝트와 관련된 Core 모듈의 C++ 코드에 대한 메모리 누수 복구 및 메모리 관리 개선이 포함됩니다.

## 해결된 문제

### 1. custom_file.cpp의 메모리 누수

#### 문제 1: CVWrite 함수의 ostream이 해제되지 않습니다.
**위치**: `Native/opencv_helper/custom_file.cpp:87-103`

**원본 코드**:
```cpp
char* ostream = (char*)malloc(destLen);
int res = 압축((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
// ... 오류 확인 중 ...
grifMat.destLen = destLen;
outFile.write(ostream, grifMat.destLen);
// 무료(ostream)가 누락되었습니다!
```
**수정**: RAII 모드에서 `MallocGuard` 클래스를 사용하여 자동으로 메모리 관리
```cpp
MallocGuard streamGuard(ostream);
// ...ostream 사용 ...
// streamGuard가 파괴되면 자동으로 free 호출
```
#### 문제 2: CVRead 함수의 o2stream이 해제되지 않습니다.
**위치**: `Native/opencv_helper/custom_file.cpp:138-143`

**원본 코드**:
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 압축을 푼다 ...
return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
// o2stream은 해제되지 않았고 Mat는 메모리를 소유하지 않습니다!
```
**수정**: clone()을 사용하여 데이터를 복사한 다음 원래 버퍼를 해제합니다.
```cpp
char* o2stream = (char*)malloc(grifMat.srcLen);
// ... 압축을 푼다 ...
cv::Mat 결과(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
cv::Mat cloned = result.clone(); // 데이터 복사, OpenCV에 새 메모리가 있음
무료(o2stream); // 원본 버퍼를 해제합니다.
복제된 반환;
```
#### 문제 3: CVRead 함수의 데이터가 공개되지 않습니다.
**위치**: `Native/opencv_helper/custom_file.cpp:147-151`

**원본 코드**:
```cpp
char* 데이터 = 새로운 char[grifMat.srcLen];
inFile.read(data, grifMat.srcLen);
cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
mat1을 반환합니다. // 데이터가 공개되지 않았습니다!
```
**수정**: `ArrayGuard` RAII 클래스를 사용하고 반환하기 전에 복제
```cpp
ArrayGuard<char> data(new char[grifMat.srcLen]);
inFile.read(data.get(), grifMat.srcLen);
cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
mat1.clone()을 반환합니다. // 복사 후 복귀하면 원본 데이터가 자동으로 해제됩니다.
```
### 2. common.cpp의 메모리 관리 개선

#### 문제: UTF8ToGB는 원시 포인터를 사용합니다.
**위치**: `Native/opencv_helper/common.cpp:28-46`

**원본 코드**:
```cpp
WCHAR* strSrc = 새로운 WCHAR[i + 1];
// ... 사용 ...
LPSTR szRes = 새로운 CHAR[i + 1];
// ... 사용 ...
삭제[] strSrc;
삭제[] szRes;
```
**수정**: `std::Vector`를 사용하여 자동으로 메모리 관리
```cpp
std::벡터<WCHAR> wideBuffer(wideCharLen);
std::벡터<char> multiByteBuffer(multiByteLen);
// 자동으로 해제되므로 수동으로 삭제할 필요가 없습니다.
```
## 새로운 도구 클래스

### 1. MallocGuard
'malloc/free'에 의해 할당된 메모리를 관리하는 데 사용됩니다.

```cpp
클래스 MallocGuard {
    char* m_ptr;
공개:
    명시적 MallocGuard(char* ptr) : m_ptr(ptr) {}
    ~MallocGuard() { if (m_ptr) free(m_ptr); }
    무효 릴리스() { m_ptr = nullptr; }
    char* get() const { return m_ptr; }
    // 이동 의미를 지원하고 복사를 금지합니다.
};
```
### 2. 어레이가드\<T\>
`new[]/delete[]`에 의해 할당된 메모리를 관리하는 데 사용됩니다.

```cpp
템플릿<유형 이름 T>
클래스 ArrayGuard {
    T* m_ptr;
공개:
    명시적 ArrayGuard(T* ptr) : m_ptr(ptr) {}
    ~ArrayGuard() { 삭제[] m_ptr; }
    T* get() const { return m_ptr; }
    T& 연산자[](size_t idx) { return m_ptr[idx]; }
    // 이동 의미를 지원하고 복사를 금지합니다.
};
```
## 모범 사례

### 1. 표준 용기 사용을 우선시하라
```cpp
// 추천
std::벡터<char> 버퍼(크기);

// 피하다
char* 버퍼 = 새 문자[크기];
// ... 일찍 반환되어 메모리 누수가 발생할 수 있습니다.
삭제[] 버퍼;
```
### 2. 스마트 포인터를 사용하여 리소스 관리
```cpp
// 추천
std::unique_ptr<char[]> buffer(new char[size]);// 또는 사용자 정의 삭제자를 사용합니다.
std::unique_ptr<char, decltype(&free)> buffer(malloc(size), free);
```
### 3. OpenCV Mat 메모리 관리
```cpp
// 외부 데이터를 사용하여 Mat를 생성할 때 Mat는 메모리를 소유하지 않습니다.
cv::Mat mat(행, 열, 유형, externalData);

// Mat에 데이터가 필요하면 clone()을 사용하세요.
cv::매트 소유 = mat.clone();

// 또는 create + memcpy를 사용하세요.
이력서::매트 소유;
owned.create(행, 열, 유형);
memcpy(owned.data, externalData, 크기);
```
### 4. 오류 처리 및 리소스 해제
```cpp
// 피하십시오: 잘못된 경로에서 리소스를 해제하는 것을 잊어버릴 수도 있습니다.
char* 버퍼 = (char*)malloc(크기);
if (some_error) {
    -1을 반환합니다. // 메모리 누수!
}
무료(버퍼);

// 권장사항: RAII 사용
MallocGuard buffer((char*)malloc(size));
if (some_error) {
    -1을 반환합니다. // 자동 해제
}
//일반 프로세스도 자동으로 해제됩니다.
```
## 파일 수정 목록

| 파일 | 콘텐츠 수정 |
|------|----------|
| `네이티브/include/custom_file.h` | 도구 클래스 선언 추가 및 인터페이스 업데이트 |
| `네이티브/opencv_helper/custom_file.cpp` | 메모리 누수 수정, RAII 도구 클래스 추가 |
| `네이티브/opencv_helper/common.cpp` | UTF8ToGB 메모리 관리 최적화 |

## 확인 제안

1. **Visual Studio 메모리 진단 도구 사용**
   - 디버그 → 창 → 진단 도구 표시
   - 메모리 사용량 곡선이 안정적인지 관찰

2. **애플리케이션 검증 도구 사용**
   - 힙 검사 활성화
   - 프로그램을 실행하고 메모리 누수 보고가 있는지 확인합니다.

3. **코드리뷰 핵심 포인트**
   - 모든 `malloc`/`free` 쌍을 확인하세요.
   - 모든 `new`/`delete` 쌍을 확인하세요.
   - 비정상 경로의 리소스 해제 확인

## 후속 최적화 제안

1. 문자열 경로 작업 대신 `std::filesystem` 사용을 고려하세요.
2. 더 나은 오류 로깅 추가
3. 빈번한 소규모 메모리 할당을 최적화하려면 메모리 풀 사용을 고려하세요.
4. 대용량 이미지 처리를 위한 메모리 사용량 추정 및 제한 추가
