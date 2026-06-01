#ColorVision.Core

이 페이지에서는 현재 UI/ColorVision.Core에서 구현되는 기본 상호 운용성 레이어에 대해서만 설명합니다. 더 이상 "고수준 이미지 API 매뉴얼"과 이전 문서에 존재하지 않는 관리 방법 예제를 계속하지 않습니다.

## 모듈 포지셔닝

ColorVision.Core는 현재 주로 다음을 담당하는 기본 이미지 및 비디오 기능 브리징 레이어에 더 가깝습니다.

- 관리/비관리 경계를 넘나드는 `HImage`와 같은 데이터 구조 정의
- P/Invoke를 통해 `opencv_helper.dll`, `opencv_cuda.dll` 호출
- WPF 측에서 비트맵 변환 및 업데이트 지원 제공
- 가색상, 이미지 강화, 초점 평가, 영상 관련 등 네이티브 진입이 노출됨

캡슐화된 고급 이미지 처리 프레임워크가 아닙니다. 현재 많은 기능이 여전히 'extern' 메서드 수준의 기본 내보내기 래퍼입니다.

## 현재 가장 중요한 파일

프로젝트 디렉토리에서 가장 먼저 읽어야 할 내용은 다음과 같습니다.

- `HImage.cs`: 이미지 데이터 구조
- `HImageExtension.cs`: `HImage`와 WPF 이미지 객체 사이를 연결합니다.
- `OpenCVMediaHelper.cs`: 기본 내보내기 래퍼의 주요 컬렉션
- `OpenCVCuda.cs`: CUDA 관련 네이티브 입구
- `ColormapTypes.cs`: 의사 색상 열거
- `NativeLogBridge.cs`: 네이티브 로그 브리지
- `nvcuda.cs`: CUDA 관련 P/Invoke 정의

## 키 입력 유형

### H이미지

`HImage`는 현재 이전 문서와 같이 여러 인스턴스 메서드가 있는 관리 클래스가 아니라 기본 이미지 버퍼를 보유하는 구조입니다. 핵심 분야는 다음과 같습니다.

- `행`
-`열`
-`채널`
- `깊이`
- `스트라이드`
- `p데이터`

동시에 `Marshal.AllocHGlobal`에 의해 할당된 이미지 메모리를 해제하는 역할을 하는 `Dispose()`를 구현합니다.

즉, 현재 모듈의 가장 중요한 책임 중 하나는 기본 경계와 관리 경계를 넘어 이미지 버퍼를 안전하게 전달하는 것입니다.

### H이미지 확장

'HImageExtension'은 완전한 처리 알고리즘 라이브러리보다는 브리징 지원을 제공합니다. 현재 주로 다음을 담당합니다.

- 채널 수와 비트 심도에 따른 'PixelFormat' 도출
- `HImage`의 내용을 `WriteableBitmap`에 복사합니다.
- 비동기 비트맵 업데이트 경로 제공
- 기본 이미지 데이터를 WPF 표시 가능 객체로 변환하는 데 도움을 줍니다.

따라서 그 가치는 주로 알고리즘 체인이 아닌 디스플레이 체인에 있습니다.

### OpenCVMediaHelper

이름은 `OpenCVMediaHelper`이지만 현재 비디오 관련 인터페이스뿐만 아니라 다음을 포함하는 `opencv_helper.dll`의 내보내기 패키지가 많이 포함되어 있습니다.

- 의사 색상 및 자동 범위 의사 색상
- 최소/최대값 추출
- 자동 밝기, 자동 색상, 자동 색조
- 채널 추출
- 밝기 대비, 감마, 반전, 임계값, 선명도, 필터링, 가장자리 감지
- SFR 및 집중평가
- 여러 개의 식별 또는 감지 입구
- 영상 관련 구조 및 기능

따라서 현재 더 정확한 이해는 "비디오 도움말 클래스"뿐만 아니라 주요 기본 이미지 기능 내보내기 표면이라는 것입니다.

### OpenCVCuda

'OpenCVCuda'는 현재 이전 문서에서 주장된 것처럼 범용 CUDA 장치 관리 계층이 아닙니다. 이제 노출되는 것은 다음과 같은 융합 관련 항목에 초점을 맞춘 소수의 `opencv_cuda.dll` 내보내기입니다.

- `CM_퓨전`
- `CM_Fusion_Async`
- `CM_Fusion_Batch`

따라서 CUDA 기능을 설명할 때 현재 실제 내보내기에 따라 작성해야 하며 GPU 기능의 완전한 일반 입구로 확장해서는 안 됩니다.

### ColormapTypes 및 NativeLogBridge

- `ColormapTypes`는 의사 색상 매핑 열거를 통합하는 역할을 담당합니다.
- `NativeLogBridge`는 기본 측면 로그를 관리되는 로그 시스템에 연결하는 역할을 담당합니다.

두 파일 모두 작지만 각각 유사 색상 체인과 디버그 체인의 중요한 경계 지점입니다.

## 현재 런타임 메인 체인

이 모듈 세트는 현재 다음 체인과 유사합니다.

1. 상위 모듈은 P/Invoke를 통해 `OpenCVMediaHelper` 또는 `OpenCVCuda`를 호출합니다.
2. 네이티브 DLL은 'HImage'를 반환하거나 'HImage' 출력 매개변수를 작성합니다.
3. WPF 디스플레이 체인은 `HImageExtension`을 통해 이미지 데이터를 `WriteableBitmap`으로 업데이트합니다.
4. 'ColorVision.ImageEditor'와 같은 상위 계층 모듈은 이러한 비트맵 주위에 계속 상호 작용하고, 그리고, 표시합니다.

## 현재 구현의 경계는 무엇입니까?

### 고급 OO API로 작성하지 마세요.

현재 코드에는 이전 문서에 작성된 다음과 같은 일반적인 고급 인터페이스가 없습니다.

- `HImage.Load(...)`
- `HImage.ToBitmapSource()`
- `OpenCVCuda.GetCudaDeviceCount()`
- `OpenCVCuda.IsCudaAvailable()`

이러한 작성 방법은 독자가 존재하지 않는 관리 패키지를 찾도록 오해할 수 있습니다.

### HImage의 리소스 의미는 매우 중요합니다.

`HImage`는 일반적인 관리 개체가 아니며 관리되지 않는 포인터와 명시적인 해제 논리를 포함합니다. 이 모듈을 논의할 때 "클래스 디자인"보다 메모리 및 소유권 경계가 더 중요합니다.

### 상위 수준의 비즈니스 의미는 여기에 없습니다.

Core는 기본 기능 연결만 담당하며 도구 모음, 상호 작용 또는 ImageEditor와 같은 문서 상태 조정은 담당하지 않습니다. 읽을 때 이는 단지 낮은 수준의 능력 기반일 뿐이라는 점을 분명히 해야 합니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 이미지 데이터 구조와 디스플레이 브리징을 보고 싶습니다.

먼저 살펴보세요:

-`HImage.cs`
- `HImageExtension.cs`

### 기본 내보내기 인터페이스를 보고 싶습니다.

먼저 살펴보세요:

- `OpenCVMediaHelper.cs`
-`OpenCVCuda.cs`

### 유사 색상 및 로그 테두리를 보고 싶습니다.

먼저 살펴보세요:

- `ColormapTypes.cs`
- `NativeLogBridge.cs`

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 존재하지 않는 관리되는 상위 수준 메서드의 예
- 'OpenCVCuda'를 완전한 장치 관리 계층으로 작성합니다.
- 대규모 업데이트 로그 및 버전 목록
- Core를 완전한 상위 수준 이미지 처리 프레임워크로 호출

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)