#ColorVision.스케줄러

이 페이지에서는 현재 구현된 `UI/ColorVision.Scheduler/`의 스케줄링 기능만 설명하고 이전 문서의 "일반 Quartz 튜토리얼 + 가상 작업 플랫폼 기능 목록"을 더 이상 유지하지 않습니다.

## 모듈 포지셔닝

`ColorVision.Scheduler`는 현재 데스크톱 측의 작업 예약 및 모니터링 모듈입니다. 핵심은 "추상적인 작업 유형 컬렉션"이 아니라 다음 세 가지 실제 체인입니다.

- `QuartzSchedulerManager`는 Quartz 스케줄러 및 작업 복구를 관리합니다.
- `scheduler_tasks.json`은 작업 구성을 저장합니다.
- `SchedulerHistory.db`는 실행 이력 및 통계 복구 데이터를 저장합니다.

따라서 이는 순수한 UI 컨트롤도 아니고 단순한 Quartz 래핑 레이어도 아닙니다.

## 현재 가장 중요한 파일

프로젝트 디렉터리를 살펴보면 먼저 알아야 할 가장 중요한 사항은 다음과 같습니다.

- `QuartzSchedulerManager.cs`: 스케줄러의 주요 입구
- `TaskViewerWindow.xaml(.cs)` : 작업 보기, 필터링 및 마우스 오른쪽 버튼 클릭 작업 창
- `CreateTask.xaml(.cs)`: 작업 창 생성 및 편집
- `TaskExecutionListener.cs`: 실행 모니터링 및 통계 업데이트
- `Data/SchedulerDbManager.cs`: 기록 SQLite 지속성
- `MenuTaskViewer.cs`: 메뉴 항목 및 초기화 프로그램
- `SchedulerInfo.cs`: 작업 표시 및 지속성 모델

## 키 입력 유형

### `QuartzSchedulerManager`

`QuartzSchedulerManager`는 현재 스케줄링 모듈의 중심 개체입니다. 다음을 담당합니다.

- Quartz 스케줄러 시작
- 'IJob' 유형에 대해 로드된 어셈블리를 스캔합니다.
- `TaskInfos` 유지
- JSON 파일에서 작업 구성 로드
- 시작 후 역사적 임무 복원
- 작업을 일시 중지, 재개, 삭제, 업데이트 및 생성하는 방법을 제공합니다.

현재 작업 구성 파일은 기본적으로 다음 위치에 있습니다.

- `%AppData%/ColorVision/scheduler_tasks.json`

이는 현재 작업 정의가 데이터베이스에 완전히 존재하지 않고 주로 JSON 구성을 기반으로 하며 SQLite 기록으로 보완됨을 보여줍니다.

### `태스크뷰어 창`

`TaskViewerWindow`는 현재 작업 관리의 기본 창입니다. 다음을 담당합니다.

- `TaskInfos` 바인딩
- 이름, 그룹, 상태로 필터링
- 스케줄러에서 등록된 작업의 다음 및 마지막 실행 시간을 읽어옵니다.
- 마우스 오른쪽 버튼 클릭 메뉴를 통해 편집, 속성 보기, 일시 정지, 계속, 즉시 실행, 삭제, 기록 보기

이 페이지의 이전 문서에 있는 "크고 포괄적인 모니터링 패널 설계 도면"은 여기의 실제 창만큼 가치가 없습니다.

### `CreateTask`

'CreateTask' 창은 작업 생성 및 편집을 담당합니다. 'SchedulerInfo'와 함께 작동하여 작업이 궁극적으로 직렬화, 복원 및 업데이트되는 방법을 결정합니다.

### `SchedulerDbManager`

실행 기록은 동일한 JSON 파일이 아닌 별도의 SQLite 데이터베이스에 저장됩니다. `SchedulerDbManager`는 현재 다음을 담당합니다.

- `%AppData%/ColorVision/SchedulerHistory.db` 초기화
- 실행기록 쓰기
- 단일 작업 또는 전체 실행 내역 쿼리
- 재부팅 후 복구를 위한 통계 계산
- 오래된 기록을 정리하세요

재시작 후에도 "실행 횟수, 성공 및 실패 횟수, 평균 소요 시간" 등 현재 데이터가 계속 유지될 수 있는 이유이기도 합니다.

### `TaskExecutionListener`

런타임 통계 업데이트 및 실행 피드백은 창 자체에서 폴링하여 얻는 것이 아니라 리스너를 통해 작업 상태 및 실행 기록을 다시 작성하여 얻습니다.

## 현재 런타임 메인 체인

스케줄링 모듈은 현재 다음 체인에 더 가깝습니다.

1. `TaskViewerInitializer` 또는 메뉴 항목이 `QuartzSchedulerManager.GetInstance()`를 트리거합니다.
2. `QuartzSchedulerManager`는 Quartz 스케줄러를 시작합니다.
3. 현재 로드된 어셈블리에서 'IJob' 유형을 검색하고 작업 유형 사전을 빌드합니다.
4. `%AppData%/ColorVision/scheduler_tasks.json`을 읽어보세요.
5. 시작 후 기존 작업의 복구를 지연합니다.
6. `TaskExecutionListener`는 작업이 실행될 때 상태와 통계를 업데이트합니다.
7. `SchedulerDbManager`는 `SchedulerHistory.db`에 실행 기록을 씁니다.
8. 'TaskViewerWindow'는 이러한 상태, 기록 및 통계를 사용자에게 표시합니다.

이 링크는 이전 문서의 "작업 편집기/모니터링 패널/로그 뷰어의 3계층 아키텍처"보다 기존 구현에 더 가깝습니다.

## 현재 구현의 경계는 무엇입니까?

### 작업 유형은 로드된 어셈블리에서 나옵니다.

현재 'QuartzSchedulerManager'는 'AssemblyService.Instance.GetAssemblies()'를 순회하여 'IJob'을 구현하는 유형을 수집하고 표시 이름으로 'DisplayNameAttribute'에 우선순위를 부여합니다.

따라서 새 작업 유형을 추가하는 것은 본질적으로 특정 작업 유형 테이블에 등록하는 것이 아니라 어셈블리에서 검색할 수 있는 'IJob' 구현을 추가하는 것입니다.

### 구성 복구와 실행 내역은 두 가지 저장소로 구성됩니다.

현재 작업 정의 및 복구는 주로 JSON에 의존합니다. 실행 기록 및 통계 복구는 주로 SQLite에 의존합니다. 두 가지를 단일 데이터베이스 파견 센터로 혼합하지 마십시오.

### 작업창은 회로도가 아닌 실제 관리 입구입니다.

현재 가장 중요한 사용자 포털은 `TaskViewerWindow`와 `CreateTask`입니다. 코드가 특정 구현에 직접적으로 대응할 수 없다면 기존 기능으로 많은 오래된 문서에서 조작된 "일괄 내보내기, 통계 보고 및 복잡한 패널 분할"을 계속해서 나열할 필요는 없습니다.

## 현재 이 프로젝트를 어떻게 읽는 것이 더 적합합니까?

### 스케줄러가 어떻게 시작되고 복원되는지 보고 싶습니다.

먼저 살펴보세요:

- `QuartzSchedulerManager.cs`
- `MenuTaskViewer.cs`

### 작업 인터페이스와 작업 입구를 보고 싶어요

먼저 살펴보세요:

- `TaskViewerWindow.xaml(.cs)`
- `CreateTask.xaml(.cs)`

### 실행 내역 및 통계를 보고 싶습니다.

먼저 살펴보세요:

-`데이터/SchedulerDbManager.cs`
-`TaskExecutionListener.cs`
- `ExecutionHistoryWindow.xaml(.cs)`

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 일반 Quartz 샘플코드 수집
- 검증되지 않은 시스템 업무/업무 업무/유지보수 업무 분류표
- 상상의 통합 업무 플랫폼 기능 매트릭스
- 오래된 버전 번호 및 대상 프레임워크 목록

나중에 특정 작업 유형을 추가해야 하는 경우 여기에 튜토리얼 스타일의 콘텐츠를 계속 작성하는 대신 실제 작업 구현이나 창 페이지로 직접 이동해야 합니다.

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Database](./ColorVision.Database.md)