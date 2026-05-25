# SFR 모듈 리팩토링 완료 문서

## 리팩토링 개요

SFR 모듈은 Core 모듈의 통합 아키텍처 설계에 맞춰 분산형 3계층 구조에서 중앙 집중형 구조로 재구성되었습니다.

## 디렉토리 구조 변경

### 재건축 전
```
packages/sfr/ # 분산형 알고리즘 라이브러리
├── 포함/sfr/
│ ├── 일반.h
│ ├── 경사.h
│ └── 실린더.h
└── 소스/
    ├── 일반.cpp
    ├── 경사.cpp
    └── 실린더.cpp

포함/
└── opencv_media_export.h # C 인터페이스 선언

코어/opencv_helper/
└── opencv_media_export.cpp # C 인터페이스 구현(sfr 호출)
```
### 재구성 후
```
코어/opencv_helper/
├── 알고리즘/
│ └── sfr/ # SFR 알고리즘 구현
│ ├── sfr_base.h/.cpp #기본 도구 기능(이전에는 일반)
│ ├── sfr_slanted.h/.cpp # 빗변법 SFR (이전 경사)
│ └── sfr_cylinder.h/.cpp# 원통형 SFR(원통형)
│
├── include/cvcore/
│ └── sfr.h # 통합 인터페이스 헤더 파일
│
└── 수출/
    └── sfr_export.cpp # C 인터페이스 내보내기 구현

packages/sfr/ # 더 이상 사용되지 않는 것으로 표시(삭제 가능)
```
## 네임스페이스 변경

| 이전 네임스페이스 | 새 네임스페이스 | 설명 |
|------------|------------|------|
| `::sfr` | `cvcore::sfr` | cvcore 네임스페이스로 통합 |

### 이전 버전과의 호환성
```cpp
// sfr_base.h에 별칭을 제공합니다.
네임스페이스 sfr = cvcore::sfr;
```
## API 변경

### C++ 인터페이스

| 이전 API | 새로운 API | 설명 |
|---------|---------|------|
| `sfr::CalSFR()` | `cvcore::sfr::calculateSlantedEdgeSFR()` | 새 이름이 더 명확해졌습니다 |
| `sfr::원` | `cvcore::sfr::원` | 이름을 대문자로 입력 |
| `sfr::circle_fit()` | `cvcore::sfr::fitCircle()` | 동사의 시작 |
| `sfr::esf()` | `cvcore::sfr::cylinderESF()` | 구별방법 |
| `sfr::lsf()` | `cvcore::sfr::cylinderLSF()` | 구별방법 |
| `sfr::mtf()` | `cvcore::sfr::cylinderMTF()` | 구별방법 |

### C++ 인터페이스 추가
```cpp
// 구조화된 반환 결과
구조체 SFRResult {
    이중 vs기울기;
    std::벡터<더블> 주파수;
    std::벡터<더블> sfr;
    이중 mtf10_norm;
    더블 mtf50_norm;
    더블 mtf10_cypix;
    더블 mtf50_cypix;
    
    bool isValid() const;
};

구조체 실린더SFRResult {
    서클 서클;
    std::벡터<cv::Point2d> esf;
    std::벡터<cv::Point2d> lsf;
    std::벡터<cv::Point2d> mtf;
    이중 mtf10;
    더블 mtf50;
    
    bool isValid() const;
};

// 새로운 메인 함수
SFRResult 계산SlantedEdgeSFR(const cv::Mat& img,
                                   이중 델 = 1.0,
                                   int npol = 5,
                                   int nbin = 4,
                                   이중 vs기울기 = -1);

실린더SFR결과 계산CylinderSFR(const cv::Mat& mat,
                                        정수 쓰레쉬 = 80,
                                        플로트 ROI = 15.0f,
                                        부동 소수점 크기 = 0.032f,
                                        int n_fit = 25);
```
### C 인터페이스(변경되지 않음)
```cpp
COLORVISIONCORE_API int M_CalSFR(...);
COLORVISIONCORE_API int M_CalSFRMultiChannel(...);
```
## 파일 대응| 원본 파일 | 새 파일 | 설명 |
|---------|---------|------|
| `패키지/sfr/include/sfr/general.h` | `코어/opencv_helper/algorithm/sfr/sfr_base.h` | 이름 바꾸기 |
| `패키지/sfr/include/sfr/slanted.h` | `코어/opencv_helper/algorithm/sfr/sfr_slanted.h` | 이름 바꾸기 |
| `패키지/sfr/include/sfr/cylinder.h` | `코어/opencv_helper/algorithm/sfr/sfr_cylinder.h` | 이름 바꾸기 |
| `패키지/sfr/src/general.cpp` | `코어/opencv_helper/algorithm/sfr/sfr_base.cpp` | 이름 바꾸기 |
| `패키지/sfr/src/slanted.cpp` | `코어/opencv_helper/algorithm/sfr/sfr_slanted.cpp` | 이름 바꾸기 |
| `패키지/sfr/src/cylinder.cpp` | `코어/opencv_helper/algorithm/sfr/sfr_cylinder.cpp` | 이름 바꾸기 |

## 컴파일 구성 업데이트

### vcxproj 파일 수정
`Core/opencv_helper/opencv_helper.vcxproj`를 업데이트해야 합니다.

1. **포함 디렉터리 추가**
```xml
<추가포함디렉터리>
  $(프로젝트 디렉터리)/include/cvcore;
  $(ProjectDir)/알고리즘/sfr;
  %(추가Include디렉터리)
</AdditionalIncludeDirectories>
```
2. **소스 파일 추가**
```xml
<항목 그룹>
  <Cl컴파일 포함="알고리즘\sfr\sfr_base.cpp" />
  <Cl컴파일 포함="알고리즘\sfr\sfr_slanted.cpp" />
  <Cl컴파일 포함="알고리즘\sfr\sfr_cylinder.cpp" />
  <Cl컴파일 포함="exports\sfr_export.cpp" />
</ItemGroup>

<항목 그룹>
  <ClInclude include="include\cvcore\sfr.h" />
  <ClInclude include="알고리즘\sfr\sfr_base.h" />
  <ClInclude 포함="알고리즘\sfr\sfr_slanted.h" />
  <ClInclude include="알고리즘\sfr\sfr_cylinder.h" />
</ItemGroup>
```
3. **이전 참조 삭제**
```xml
<!-- 다음 내용을 제거하세요 -->
<!-- <Cl컴파일 포함="..\..\packages\sfr\src\*.cpp" /> -->
```
### CMakeLists.txt(CMake를 사용하는 경우)
```cmake
# SFR 모듈
세트(SFR_SOURCES
    알고리즘/sfr/sfr_base.cpp
    알고리즘/sfr/sfr_slanted.cpp
    알고리즘/sfr/sfr_cylinder.cpp
    수출/sfr_export.cpp
)

설정(SFR_HEADERS
    include/cvcore/sfr.h
    알고리즘/sfr/sfr_base.h
    알고리즘/sfr/sfr_slanted.h
    알고리즘/sfr/sfr_cylinder.h
)

target_sources(opencv_helper PRIVATE ${SFR_SOURCES} ${SFR_HEADERS})

target_include_directories(opencv_helper PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include/cvcore
    ${CMAKE_CURRENT_SOURCE_DIR}/algorithm/sfr
)
```
## 마이그레이션 단계

1. **기존 코드 백업**
``배쉬
git checkout -b sfr-리팩터링-백업
자식 추가 .
git commit -m "SFR 리팩터링 전 백업"
```
2. **새 코드 컴파일 확인**
``배쉬
# 오래된 컴파일 캐시를 정리합니다
닷넷 클린

# 재컴파일
닷넷 빌드
```
3. **테스트 실행**
``배쉬
#SFR 관련 테스트 실행
# M_CalSFR 및 M_CalSFRMultiChannel이 제대로 작동하는지 확인하세요.
```
4. **오래된 파일 삭제**
``배쉬
# 새 코드가 제대로 작동하는지 확인한 후 기존 디렉터리를 삭제합니다.
rm -rf 패키지/sfr/
```
## 코드 예

### 새로운 C++ 인터페이스 사용
```cpp
#include <cvcore/sfr.h>

네임스페이스 cvcore 사용;

// 빗변 방법 SFR
cv::Mat img = cv::imread("edge.png", cv::IMREAD_GRAYSCALE);
자동 결과 = sfr::calculateSlantedEdgeSFR(img, 1.0, 5, 4);

if (result.isValid()) {
    std::cout << "MTF50: " << result.mtf50_cypix << " cy/pix" << std::endl;
}

// 원통형 방법 SFR
cv::Mat 실린더_img = cv::imread("circle.png", cv::IMREAD_GRAYSCALE);
자동 cyl_result = sfr::calculateCylinderSFR(cylinder_img, 80, 15.0f);

if (cyl_result.isValid()) {
    std::cout << "MTF10: " << cyl_result.mtf10 << std::endl;
}
````### 이전 버전과의 호환성(이전 코드)
```cpp
// 이전 네임스페이스를 계속 사용할 수 있습니다.
#include <cvcore/sfr.h>

// 기존 호출 방법을 사용합니다.
sfr::SFRResult 결과 = sfr::CalSFR(img, 1.0, 5, 4, -1);
```
## 장점

1. **명확한 구조**: 모든 SFR 관련 코드가 하나의 디렉토리에 집중되어 있습니다.
2. **이름 일관성**: `cvcore::` 네임스페이스를 균일하게 사용합니다.
3. **인터페이스 통합**: 다른 알고리즘 모듈(예: 융합)과 구조가 일관됩니다.
4. **유지보수 용이**: 한 곳에서만 SFR 수정
5. **하위 호환성**: 이전 API를 유지하고 원활하게 마이그레이션

## 메모

1. **Eigen 종속성**: 프로젝트가 여전히 Eigen 라이브러리에 액세스할 수 있는지 확인하세요.
2. **OpenCV 버전**: OpenCV 4.x 필요
3. **컴파일러**: C++17 지원 필요
