#ColorVision.Engine

이 페이지에서는 현재 웨어하우스에서 실제로 사용할 수 있는 `ColorVision.Engine` 모듈만 설명하며 "완전한 API 테이블 + 통합 계층 청사진 + 의사 예제"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 모듈이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 'ColorVision.Engine'은 단순한 알고리즘 라이브러리가 아니라 ColorVision 메인 프로그램의 핵심 엔진 어셈블리 레이어입니다. 현재 최소한 다음을 담당합니다.

- 장치 및 서비스 객체의 호스트 측 추상화.
- 템플릿 시스템의 로딩, 편집 및 지속성.
- MQTT 요청, 하트비트 및 메시지 로깅.
- FlowEngineLib는 기본 프로그램의 UI와 템플릿을 연결합니다.
- 알고리즘 표시 레이어와 템플릿 편집기 간의 연결.

따라서 모든 비즈니스를 로컬에서 직접 계산하는 단일 모듈보다는 "런타임 엔진 호스팅 계층"에 더 가깝습니다.

## 현재 가장 중요한 파일

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
- `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`
- `Engine/ColorVision.Engine/Services/DeviceService.cs`
- `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
- `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
- `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`

메인 엔진이 템플릿, 장치, 메시지 체인 및 프로세스를 구성하는 방법을 알고 싶다면 이 코드가 이미 백본을 다루었습니다.

## 현재 제어 평면을 블록으로 나누는 방법

### 템플릿 로딩 및 템플릿 등록

`TemplateControl`은 현재 템플릿 시스템의 일반적인 입구입니다. 모든 어셈블리에서 `IITemplateLoad` 구현을 검색하고 MySQL을 사용할 수 있게 된 후 `Load()`를 실행한 다음 템플릿 인스턴스를 `ITemplateNames`에 등록합니다.

이는 손으로 작성한 정적 목록 대신 템플릿 시스템이 현재 다음을 사용한다는 것을 의미합니다.

- 초기화 트리거
- 조립 스캔
- 템플릿 인스턴스 레지스트리

연속으로 세 단계입니다.

### JSON 템플릿 편집

`ITemplateJson<T>`는 현재 JSON 템플릿의 실제 위치를 보여줍니다.

- MySQL에서 템플릿 데이터를 읽습니다.
- 템플릿 객체는 `Activator.CreateInstance`를 통해 매개변수 객체로 패키징됩니다.
- 저장 및 삭제도 데이터베이스에 직접 다시 쓰기 가능

해당 편집기 `EditTemplateJson`은 다음을 제공합니다.

- 텍스트 모드
- 속성 편집 모드
- 주석 보기 전환
- 외부 JSON 검증 웹사이트에 빠르게 접속

이는 현재 엔진 레이어가 템플릿을 저장할 뿐만 아니라 템플릿 편집 UI의 일부를 직접 호스팅하고 있음을 보여줍니다.

### 프로세스 브리징 레이어

`FlowEngineManager`와 `DisplayFlow`는 `ColorVision.Engine`과 `FlowEngineLib` 사이의 브리지입니다. 그들은 현재 다음을 담당하고 있습니다.

- Flow의 MQTT 기본 구성 초기화
- 프로세스 템플릿 목록 및 현재 선택 유지
- Base64 데이터를 사용하여 `FlowEngineControl`에 템플릿 로드
- 'MqttRCService' 서비스 토큰과 결합하여 사용 가능한 서비스 노드를 새로 고칩니다.
- 프로세스 편집, 템플릿 편집, 배치 기록 보기 등의 UI 작업을 제공합니다.

따라서 메인 프로그램의 프로세스 기능은 'FlowEngineLib'만으로는 완성되지 않고, 실제로 윈도우 및 템플릿 시스템에 들어가려면 이 브리징 코드 계층을 거쳐야 합니다.

### 장치 및 서비스 추상화

`DeviceService`는 현재 호스트 측 장치 개체의 기본 추상화이며 다음을 담당합니다.

- 트리 노드 동작
- 아이콘 및 상황에 맞는 메뉴
- 구성 가져오기 및 내보내기
- 재설정, 다시 시작 및 속성 명령
- MQTT 서비스 객체 또는 표시기 제어에 대한 후크

'DeviceServiceFactoryRegistry'는 Camera, PG, Spectrum, SMU, Sensor 등의 서비스 유형을 일괄적으로 팩토리로 등록합니다.

이는 현재 장치 인스턴스화가 더 이상 분산된 스위치 케이스가 아니라 중앙 집중식 공장 등록임을 보여줍니다.

### MQTT 런타임

'MQTTServiceBase'는 현재 메시지 체인의 가장 중요한 호스트 기본 클래스입니다. 다음을 담당합니다.

- MQTT 메시지 구독/게시
- `MsgRecord` 유지
- 심장 박동을 기반으로 'IsAlive'를 결정합니다.
- 시간 초과 처리 및 패킷 상태 반환

'MqttRCService'는 추가로 등록 센터 클라이언트의 역할을 맡고 다음을 담당합니다.

- RC 테마 빌드
-재등록
- 서비스 토큰 캐시
- RC 연결 상태

"서비스가 온라인인지, 프로세스를 새로 고칠 수 있는지, 장치 토큰이 어디서 오는지"와 같은 엔진 계층의 많은 문제는 결국 이 계층으로 돌아옵니다.

## 이 계층에서 알고리즘은 현재 어떤 역할을 하고 있나요?

`AlgorithmPOI` 및 `AlgorithmMTF`의 구현으로 판단하면 현재 `ColorVision.Engine`의 알고리즘 클래스는 다음과 같습니다.

- 템플릿 편집기를 엽니다.
- 조직 템플릿 선택 상태
- MQTT 매개변수 조합
- 명령을 실행하기 위해 장치 서비스를 호출합니다.

즉, 이 계층의 알고리즘 개체는 일반적으로 로컬에서 이미지 계산을 직접 완료하는 순수 알고리즘 커널이 아닌 "표시 및 명령 어댑터"입니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### "모든 알고리즘이 로컬에서 실행됩니다" 모듈이 아닙니다.

현재 많은 알고리즘 클래스가 수행하는 작업은 실제로 템플릿, 파일 이름 및 장치 정보를 MQTT 요청으로 조합한 다음 처리를 위해 장치나 서버에 전달하는 것입니다. 이 레이어를 순전히 로컬 알고리즘 구현으로 계속 작성하면 실제 제어 체인과 일치하지 않게 됩니다.

### 템플릿 시스템은 초기화 및 데이터베이스와 분리될 수 없습니다.

`TemplateControl`은 MySQL이 초기화된 후 어셈블리 스캐닝에 의존합니다. `ITemplateJson<T>`도 데이터베이스와 직접 상호 작용합니다. 이것을 "완전히 로컬인 정적 템플릿 세트"로 작성하면 핵심 전제가 누락됩니다.

### 모든 프로세스 기능이 FlowEngineLib에 있는 것은 아닙니다.

기본 프로그램에서 Flow 템플릿을 실제로 편집, 선택 및 실행하려면 브리징 코드의 `Templates/Flow/` 레이어도 필요합니다. FlowEngineLib만 설명하면 호스트 측의 실제 제어 화면이 누락됩니다.

### 장치 서비스 인스턴스화는 현재 레지스트리 중심입니다.

`DeviceServiceFactoryRegistry`는 이미 현재 실제 인스턴스화 항목입니다. 이전 문서의 분산 구조 설명을 계속 사용하면 확장 지점이 편향됩니다.

## 추천 읽기 순서

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Services/DeviceService.cs`
4. `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
5. `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
6. `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

이런 방식으로 먼저 템플릿과 서비스 호스트 계층을 확인한 다음 메시지 체인과 프로세스 브리징 계층을 연결할 수 있습니다.

## 계속 읽기

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/03-architecture/components/templates/analysis.md](../../03-architecture/components/templates/analysis.md)
- [docs/04-api-reference/algorithms/overview.md](../algorithms/overview.md)
