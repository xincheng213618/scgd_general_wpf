#cvColorVision

이 페이지에서는 현재 웨어하우스에서 실제로 사용할 수 있는 `cvColorVision` 모듈만 설명하며 "함수 승격 + 다수의 가상 예제 + 순수하게 관리되는 알고리즘 라이브러리"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 모듈이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 `cvColorVision`은 비즈니스 알고리즘을 구현하기 위해 주로 C#에 의존하는 모듈이 아니라 기본 상호 운용성 브리지의 두꺼운 계층입니다. 현재 핵심 역할은 다음과 같습니다.

- `DllImport`를 통해 `cvCamera.dll` 및 `cvOled.dll`의 기능을 C#에 노출합니다.
- 카메라, 색상, 그래픽 카드, 소스 테이블, OLED 알고리즘 등 하위 수준 인터페이스를 통합된 네임스페이스에 집중합니다.
- `ColorVision.Engine`, 플러그인 및 장치 서비스에 대한 얇은 래퍼 호출 표면을 제공합니다.

따라서 이전 문서와 같이 순수하게 관리되는 시각적 프레임워크보다 "네이티브 기능 바인딩 계층"에 더 가깝습니다.

## 현재 가장 중요한 파일

- `엔진/cvColorVision/cvCameraCSLib.cs`
- `엔진/cvColorVision/ConvertXYZ.cs`
- `엔진/cvColorVision/CvOledDLL.cs`
-`엔진/cvColorVision/PG.cs`
-`엔진/cvColorVision/PassSx.cs`
- `엔진/cvColorVision/Algorithms.cs`

모듈이 기본 DLL에 연결되는 방법과 현재 노출되는 기능이 무엇인지 확인하려는 경우 이러한 코드는 이미 본체를 다루었습니다.

## 현재 제어 평면을 블록으로 나누는 방법

### 카메라 및 일반 비전 인터페이스

`cvCameraCSLib.cs`는 현재 가장 큰 바인딩 표면입니다. 코드를 보면 카메라 스위치뿐만 아니라 다음을 포함한 대규모 기본 입구 컬렉션도 포함됩니다.

- 카메라 켜기, 끄기, 실시간 미리보기, 프레임 캡처
- JSON 읽기 및 쓰기 구성
- 자동 노출, ROI, 콜백 등록
- XYZ/xy/uv/CCT/파동 샘플링
- TIFF 내보내기 및 데이터 분할/병합
- 자동 초점, 렌즈 위치, Canon 관련 제어
- 다양한 시각적 감지 및 이미지 처리 기능

따라서 이는 수십 개의 카메라 API만 포함하는 작은 패키지가 아니라 현재 가장 밀도가 높은 P/Invoke 수렴 지점입니다.

### 색상 및 크로마 샘플링

`ConvertXYZ.cs`는 `cvCamera.dll`의 XYZ 관련 항목을 현재 다음 사항에 초점을 맞춘 보다 집중된 바인딩 표면으로 분할합니다.

- XYZ 버퍼 초기화 및 해제
- 원형/사각형 영역 샘플링
- xyz, uv, CCT, 주파장 등 내보내기
- 배치 포인트 샘플링

이는 현재 색상 샘플링 체인이 독립형 C# 계산기가 아니라 기본 버퍼링 및 샘플링 기능을 중심으로 실행된다는 것을 보여줍니다.

### OLED 전용 알고리즘

`CvOledDLL.cs`는 현재 `cvOled.dll`을 구체적으로 바인딩하여 다음을 제공합니다.

- 매개변수 로딩
- 사진 로딩
- 픽셀 검색
- 픽셀 재구성
- 모아레 필터

따라서 OLED 관련 기능은 현재 카메라 인터페이스 내에서 혼합되지 않고 별도의 DLL로 구현됩니다.

### 그래픽 카드 및 주변 장치 인터페이스

'PG.cs'는 현재 그래픽 카드 장치 제어를 위한 씬 래퍼로 다음을 제공합니다.

- PG 초기화
- TCP/직렬 연결
- 시작/중지/리셋
- 상하 전환 및 특정 프레임 전환

`PassSx.cs`는 소스 테이블/전원 측에서 다음을 포함하는 기본 호출 패키징을 제공합니다.

- 장치를 켜고 끕니다.
- 소스 모드 설정
- 전면 및 후면 포트를 갖춘 2선/4선 설정
- 전압 및 전류 읽기
- 스테핑 및 스캔 수행

이는 'cvColorVision'이 현재 이미지 처리를 처리할 뿐만 아니라 여러 유형의 주변 장치의 기본 바인딩도 수행한다는 것을 보여줍니다.

### 매우 얇은 알고리즘 항목

`Algorithms.cs`와 같은 파일은 모듈의 또 다른 특징을 보여줍니다. 일부 캡슐화는 매우 얇아서 가장 직접적인 형태로 단일 기본 함수만 노출합니다.

따라서 이 계층의 책임은 모든 API 스타일을 균일하게 디자인하는 것이 아니라 기본 기능을 최대한 완벽하게 매핑하는 것입니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 순수 C# 알고리즘 센터가 아닙니다.

대부분의 주요 기능은 현재 기본 DLL에서 제공되며 C# 코드는 주로 선언, 소량의 보조 패키징 및 데이터 유형 브리징을 담당합니다. 계속해서 "주요 알고리즘은 호스팅 레이어에서 구현된다"고 쓰면 실제 코드 구조와 어긋나게 됩니다.

### `cvCameraCSLib`은 단지 카메라에 관한 것이 아닙니다.

파일 이름은 사람들이 오해하기 쉽지만 현재는 실제로 컬러 샘플링, 이미지 처리, 자동 초점 및 감지 기능을 많이 노출하고 있으며 전체 바인딩 항목 중 하나입니다.

### 여기서 인터페이스 세분성은 균일하지 않습니다.

`cvCameraCSLib.cs`와 같은 일부 파일은 매우 두껍고 `Algorithms.cs`, `PG.cs`, `CvOledDLL.cs`와 같은 일부 파일은 매우 얇습니다. 문서는 더 이상 깔끔하고 균일한 계층형 API 시스템에 직접 작성해서는 안 됩니다.

### "상위 레이어에서 호출되는" 기본 레이어에 가깝습니다.

현재 `ColorVision.Engine`, 장치 서비스 및 일부 플러그인은 여기에 노출된 기본 인터페이스를 호출합니다. `cvColorVision` 자체는 호스트 수준 창, 템플릿 또는 작업 흐름 조정을 담당하지 않습니다.

## 추천읽기순서

1.`엔진/cvColorVision/cvCameraCSLib.cs`
2. `엔진/cvColorVision/ConvertXYZ.cs`
3. `엔진/cvColorVision/CvOledDLL.cs`
4. `엔진/cvColorVision/PG.cs`
5. `엔진/cvColorVision/PassSx.cs`

이런 방식으로 가장 두꺼운 전체 바인딩 표면을 먼저 살펴본 다음 OLED, 그래픽 카드, 소스 미터와 같은 전용 인터페이스로 확장할 수 있습니다.

## 계속 읽기

- [docs/04-api-reference/engine-comComponents/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)
- [docs/04-api-reference/engine-comComponents/ColorVision.FileIO.md](./ColorVision.FileIO.md)