#코어모듈통합인터페이스설계규격

## 디자인 목표

1. **일관성**: 모든 이미지 처리 기능은 통합된 호출 규칙을 따릅니다.
2. **확장성**: 새로운 처리 알고리즘 및 백엔드 추가가 용이함
3. **유형 안전성**: 런타임 오류를 방지하려면 강력한 유형을 사용하세요.
4. **성능**: 오버헤드가 없는 추상화, 여러 백엔드 지원
5. **유지관리성**: 명확한 오류 처리 및 로깅

## 아키텍처 개요

```
┌──────────────────────────────────────────────────────┐
│ 애플리케이션 계층 │
├───────────────────────────────────────────────────────┤
│ cvcore::이미지 cvcore::ImageSequence cvcore::결과 │
├───────────────────────────────────────────────────────┤
│ 처리 인터페이스 │
│ adjustWhiteBalance() focusStacking() applyGaussianBlur() │
├───────────────────────────────────────────────────────┤
│ 백엔드 추상화 계층 │
│CPU(OpenCV) CUDA OpenCL OpenMP │
├───────────────────────────────────────────────────────┤
│ 플랫폼 추상화 계층 │
│ cv::Mat cuda::GpuMat cl::버퍼 │
└──────────────────────────────────────────────────────┘
```
## 코어 유형

### 1. 이미지 클래스

`cv::Mat`을 캡슐화하고 통합 인터페이스를 제공합니다.

```cpp
네임스페이스 cvcore {

클래스 이미지 {
공개:
    //건축
    이미지();
    명시적 이미지(const cv::Mat& mat);
    이미지(정수 너비, 정수 높이, 정수 채널, 정수 깊이);
    
    //팩토리 메소드
    static Result<Image> fromFile(const std::string& 경로);
    정적 이미지 제로(const ImageDescriptor& desc);
    
    //접속자
    int 너비() const;
    int 높이() const;
    int 채널() const;
    bool 비어 있음() const;
    
    // 변환
    cv::매트&매트();
    Result<이미지> toGray() const;
    Result<Image> ConvertDepth(int newDepth) const;
    
    // 작업
    이미지 복제() const;
    이미지 roi(int x, int y, int w, int h) const;
};

} // 네임스페이스 cvcore
```
### 2. 결과\<T\> 유형

오류 처리의 경우:

```cpp
템플릿<유형 이름 T>
Result = std::pair<T, std::ional<Error>>; 사용

// 사용 예
자동 결과 = cvcore::adjustWhiteBalance(이미지, 옵션);
if (결과.초) {
    // 오류 처리
    std::cerr << "오류: " << result.second->message << std::endl;
    반품;
}
이미지 출력 = result.first;
```
### 3. 처리 옵션

모든 핸들러 함수에 대한 옵션 기본 클래스:

```cpp
구조체 처리 옵션 {
    프로세싱백엔드 백엔드 = 프로세싱백엔드::자동;
    ProcessContext* 컨텍스트 = nullptr;
    부울 비동기 = 거짓;
    ProgressCallback 진행콜백;
};

// 파생 옵션
struct WhiteBalanceOptions : 처리 옵션 {
    이중 redBalance = 1.0;
    이중 greenBalance = 1.0;
    이중 blueBalance = 1.0;
};
```
## 처리 기능 사양

### 명명 규칙

| 작업 유형 | 접두사 | 예 |
|------------|------|------|
| 색상 조정 | `조정` | `adjustWhiteBalance` |
| 필터 | '적용' | `applyGaussianBlur` |
| 자동처리 | '자동' | `자동레벨` |
| 감지 | `찾기`/`감지` | `findLightBeads` |
| 변환 | `to`/`convert` | `toGray`, `convertDepth` |

### 매개변수 순서

```cpp
결과<이미지> 함수이름(
    const Image& src, // 1. 입력 이미지(const 참조)
    const 특정옵션& 옵션 // 2. 옵션(const 참조)
);// 추가 매개변수가 있는 변형
결과<이미지> 함수이름(
    const 이미지& src,
    double param1, // 3. 주요 매개변수
    int param2, // 4. 보조 매개변수
    const ProcessOptions& 옵션 // 5. 옵션
);
```
### 오류 처리

```cpp
// 오류 코드 정의
열거형 클래스 ErrorCode {
    성공 = 0,
    잘못된 매개변수 = -1,
    잘못된 이미지 = -2,
    지원되지 않는 형식 = -3,
    OutOfMemory = -4,
    // ...
};

// 오류 메시지
구조체 오류 {
    오류 코드 코드;
    표준::문자열 메시지;
    표준::문자열 함수;
    표준::문자열 파일;
    int 라인;
};

// 편의 매크로
#CVCORE_CHECK_IMAGE(img) 정의 \
    { \
        if (img.empty()) { \
            return { {}, std::make_ional<오류>( \
                ErrorCode::InvalidImage, "빈 이미지", \
                __기능__, __FILE__, __LINE__) }; \
        } \
    } 동안(0)
```
## 백엔드 선택

### 자동 선택 논리

```cpp
ProcessingBackend selectBackend(ProcessingBackend 요청됨, const Image& img) {
    if (요청됨 != ProcessBackend::Auto) {
        반품 요청됨;
    }
    
    // CUDA 우선순위 지정(사용 가능하고 이미지가 충분히 큰 경우)
    #ifdef HAVE_CUDA
    if (cuda::isAvailable() && img.sizeBytes() > 10 * 1024 * 1024) {
        ProcessBackend::CUDA를 반환합니다.
    }
    #endif
    
    // 두 번째 OpenCL
    #ifdef HAVE_OPENCL
    if (ocl::isAvailable()) {
        프로세싱백엔드::OpenCL을 반환합니다.
    }
    #endif
    
    //기본 CPU
    프로세싱백엔드::CPU를 반환합니다.
}
```
### 백엔드별 구현

```cpp
Result<Image> adjustWhiteBalance(const Image& src, const WhiteBalanceOptions& options) {
    CVCORE_CHECK_IMAGE(src);
    
    자동 백엔드 = selectBackend(options.backend, src);
    
    스위치(백엔드) {
        경우 처리백엔드::CUDA:
            return adjustWhiteBalanceCUDA(src, options);
        경우 처리백엔드::OpenCL:
            return adjustWhiteBalanceOpenCL(src, 옵션);
        경우 처리백엔드::CPU:
        기본값:
            return adjustWhiteBalanceCPU(src, 옵션);
    }
}
```
## 이전 버전과의 호환성

### 기존 인터페이스 유지

```cpp
// 새로운 인터페이스
CV_CORE_API Result<Image> focusStacking(const ImageSequence& 이미지,
                                        const FocusStackingOptions& 옵션);

//기존 인터페이스(유지됨, 새 인터페이스가 내부적으로 호출됨)
COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
```
### 적응 레이어

```cpp
//오래된 유형에서 새로운 유형으로 변환
이미지 변환FromHImage(const HImage* hImg);
오류 ConvertToHImage(const Image& img, HImage* out);
```
## 성능 고려 사항

### 1. 의미 이동

```cpp
// 복사 방지
이미지 처리(이미지&& 입력) {
    // 새 메모리 할당을 피하기 위해 입력을 직접 수정합니다.
    // ...
    return std::move(입력);
}
```
### 2. 메모리 풀

```cpp
클래스 처리 컨텍스트 {
공개:
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;
    
    cv::Mat acquireBuffer(const ImageDescriptor& desc);
    void releaseBuffer(cv::Mat& 버퍼);
};
```
### 3. 비동기 처리

```cpp
// 비동기 인터페이스
std::future<Result<Image>> adjustWhiteBalanceAsync(const Image& src,
                                                    const WhiteBalanceOptions& 옵션);// 총중량
std::벡터<std::future<Result<Image>>> processBatchAsync(
    const 이미지시퀀스& 이미지,
    std::function<Result<Image>(const Image&)> 프로세서);
```
## 示例代码

### 基本使용

```cpp
#include <cvcore/cvcore.h>

네임스페이스 cvcore 사용;

정수 메인() {
    // 加载图이미지
    자동 결과 = Image::fromFile("input.jpg");
    if (결과.초) {
        std::cerr << "로드 실패: " << result.second->message << std::endl;
        -1을 반환합니다.
    }
    이미지 img = result.first;
    
    // 백평衡
    WhiteBalanceOptions wbOptions;
    wbOptions.redBalance = 1.2;
    wbOptions.blueBalance = 0.9;
    wbOptions.backend = 프로세싱백엔드::CUDA;  // 확정 확정
    
    자동 wbResult = adjustWhiteBalance(img, wbOptions);
    if (wbResult.second) {
        std::cerr << "화이트 밸런스 실패" << std::endl;
        -1을 반환합니다.
    }
    
    //保存
    오류 err = io::writeImage("output.jpg", wbResult.first);
    if (err.isFailure()) {
        std::cerr << "저장 실패: " << err.message << std::endl;
    }
    
    0을 반환합니다.
}
```
### 景深融합

```cpp
// 加载图image序列
std::벡터<std::string> 파일 = {"img1.jpg", "img2.jpg", "img3.jpg"};
auto seqResult = ImageSequence::fromFiles(파일);
if (seqResult.second) { /* 오류 처리 */ }

// 합치다
FocusStackingOptions 융합옵션;
fusionOptions.step = 2;
fusionOptions.backend = 프로세싱백엔드::CUDA;
fusionOptions.useMultiStream = true;

auto fusionResult = focusStacking(seqResult.first, fusionOptions);
if (fusionResult.second) { /* 오류 처리 */ }

// 保存结果
io::writeImage("fused.jpg", fusionResult.first);
```
## 文件组织

```
포함/
└── cvcore/
    ├── cvcore_base.h # 基础类型화宏
    ├── cvcore_image.h # 이미지 와 ImageSequence
    ├── cvcore_processing.h # 处理函数声明
    ├── cvcore_io.h # I/O 操작
    └── cvcore_cuda.h # CUDA 특별정정功能

코어/
├── opencv_helper/
│ ├── cvcore_image.cpp # 이미지 实现
│ ├── cvcore_processing.cpp # 处理函数实现
│ └── cvcore_io.cpp # I/O 实现
└── opencv_cuda/
    ├── cvcore_cuda.cpp # CUDA 后端实现
    └── cvcore_cuda_kernels.cu # CUDA 커널
```
## 迁移指南

### 从旧 API 설명

| 旧 API | 새로운 API |
|---------|---------|
| `CVRead(경로)` | `io::readImage(경로)` |
| `AdjustWhiteBalance(src, dst, r, g, b)` | `adjustWhiteBalance(src, 옵션)` |
| `퓨전(imgs, step)` | `focusStacking(시퀀스, 옵션)` |
| `CM_Fusion(json, out)` | `focusStacking(시퀀스, 옵션)` + 转换 |

### 逐步迁移策略

1. **1단계**: 새로운 API
2. **2단계**: 旧 API 内调사용 새로운 API
3. **3단계**: 逐步替换旧 API 调사용
4. **4단계**: 移除旧 API(保留适配层)
