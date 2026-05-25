#ColorVision.Socket프로토콜

이 페이지에서는 UI/ColorVision.SocketProtocol의 현재 통신 구현에 대해서만 설명하고 이전 문서의 "일반 JSON 프로토콜 계층 예"와 일치하지 않는 메시지 모델 설명을 더 이상 계속하지 않습니다.

## 모듈 포지셔닝

ColorVision.SocketProtocol은 현재 주로 다음을 담당하는 데스크탑 측 로컬 TCP 통신 모듈입니다.

- 소켓 서버 시작 및 중지
- JSON 또는 일반 텍스트 요청 배포
- SQLite에 지속적인 메시지 기록
- 관리창 및 상태바 입구 제공
- 설정 시스템에 접속하세요

추상적인 "장치 프로토콜 사양 문서"가 아니라 UI, 구성 및 데이터베이스 검색 포털과 결합된 실제 모듈 집합입니다.

## 현재 가장 중요한 파일

프로젝트 디렉토리에서 가장 먼저 읽어야 할 내용은 다음과 같습니다.

- `SocketManager.cs`: 서버, 클라이언트, 배포자 및 메시지 관리를 위한 주요 입구
- `SocketInitializer.cs`: 시작 시 초기화 및 모니터링 시작 및 중지
- `SocketConfig.cs`: 통신 구성
- `ISocketJsonHandler.cs`: JSON 요청 처리 확장 지점
- `SocketMessage.cs`: 메시지 지속성 엔터티
- `SocketMessageManager.cs`: SQLite 지속성 및 쿼리
- `SocketManagerWindow.xaml(.cs)`: 창 관리 및 보기
- `SocketStatusBarProvider.cs`: 상태 표시줄 항목
- `SocketConfigProvider.cs`: 시스템 액세스 포인트 설정

## 키 입력 유형

### 소켓매니저

`SocketManager`는 현재 통신 모듈의 중심 개체입니다. 다음을 담당합니다.

- `SocketConfig` 보유
- `SocketJsonDispatcher` 및 `SocketTextDispatcher` 생성
- `SocketMessageManager` 관리
- 서버 시작 및 중지
- 연결 상태 추적
- 구성 편집 명령 노출

전체 모듈을 이해하기 위해 하나의 파일만 읽으려는 경우 첫 번째 선택은 `SocketManager.cs`입니다.

### 소켓 초기화 프로그램

`SocketInitializer`는 현재 모듈에 존재하며 실제 시작 항목 중 하나입니다. 그것은:

- 시작 시 `SocketConfig.Instance.IsServerEnabled`를 읽습니다.
- 활성화되면 `SocketManager.GetInstance().StartServer()`를 호출합니다.
- `ServerEnabledChanged`를 구독하여 작동 중에 서비스를 동적으로 시작하고 중지합니다.

즉, 통신 서비스가 온라인 상태인지 여부는 현재 사용자가 창을 수동으로 여는 데 의존하는 것이 아니라 주로 구성에 따라 결정됩니다.

### 소켓 구성

`SocketConfig`의 현재 구성 내용은 주로 다음과 같습니다.

- 서버 활성화 여부
- 청취 IP
- 항구
- 버퍼 크기
- 프로토콜 모드: `Json` 또는 `Text`

이전 문서에 기재된 timeout, automatic reconnection 등의 필드는 현재 클래스에 실제로 존재하는 구성 항목이 아닙니다.

### SocketJsonDispatcher / SocketTextDispatcher

현재 프로토콜 배포는 두 세트로 나뉩니다.

- `SocketJsonDispatcher`: `ISocketJsonHandler` 검색
- `SocketTextDispatcher`: `ISocketTextDispatcher` 검색

JSON 프로세서가 현재 `EventName`을 기준으로 일치하는 경우 요청 및 응답의 실제 모델은 다음과 같습니다.

- `SocketRequest`: `Version`, `MsgID`, `EventName`, `SerialNumber`, `Params`
- `SocketResponse`: `Version`, `MsgID`, `EventName`, `SerialNumber`, `Code`, `Msg`, `Data`

따라서 이전 문서에서와 같이 일반화된 '유형/데이터/타임스탬프' 메시지 형식이 아닙니다.

### SocketMessage / SocketMessageManager

현재 메시지 지속성은 개념적 계층 기능이 아니지만 SQLite에서 직접 구현됩니다. `SocketMessage`는 주로 다음을 저장합니다.

- 클라이언트 주소
- 방향(수신/송신)
- 내용
- 시간
-이벤트 이름/MsgID/응답 코드

`SocketMessageManager`는 다음을 담당합니다.

- `SocketMessages.db` 초기화
- 최근 메시지 로드
- 메시지 삽입, 삭제 및 쿼리
- 데이터베이스 파일 위치 열기
- 데이터베이스 브라우징 입구 제공

데이터베이스의 기본 경로는 다음과 같습니다.

- `%AppData%/ColorVision/Config/SocketMessages.db`

### SocketManagerWindow 및 SocketStatusBarProvider

현재 사용자 측의 주요 입구는 프로토콜 샘플 코드 묶음이 아니라 두 개의 UI 액세스 포인트입니다.

- `SocketManagerWindow`: 기록 메시지, 메시지 세부정보 보기, 복사, 재전송, 삭제
- `SocketStatusBarProvider`: 상태 표시줄에 연결 상태를 반영하고, 클릭하여 관리 창을 엽니다.

또한 `SocketManagerWindow.xaml.cs`는 현재 관리 창을 열기 위해 도움말 메뉴 아래에 걸려 있는 메뉴 항목 클래스 `MenuProjectManager`도 정의합니다.

현재의 관리창은 더 이상 "메시지 목록 + 세부정보"의 가장 작은 형태가 아닙니다. 창 상단에는 서비스 활성화 상태, 서비스가 열려 있는지 여부, 수신 주소, 프로토콜 모드 및 클라이언트 수가 표시됩니다. 열기에 실패하면 마지막 오류 메시지가 직접 표시됩니다. 메시지 영역은 텍스트 필터링, 방향 필터링, 자동 스크롤 및 목록 가상화를 지원합니다. 정보는 오른쪽에 "메시지 세부정보/연결된 클라이언트/서비스 진단" 탭을 통해 구성되며, 세부정보 영역은 JSON 형식 보기를 지원합니다. 메시지를 다시 보낼 때 원래 클라이언트 주소에 따라 연결을 일치시키는 것이 우선적으로 적용됩니다. 찾을 수 없는 경우 현재 선택한 클라이언트를 대상으로 사용할 수 있습니다.

자주 사용되는 단축키:

- `Ctrl+F`: 포커스 필터 상자
- `Esc`: 필터 지우기
- `F5`: 최근 메시지 다시 로드
- `Ctrl+C`: 선택한 메시지 내용 복사
- `삭제`: 선택한 메시지를 삭제합니다.

## 현재 런타임 메인 체인

기존 링크는 대략 다음과 같습니다.

1. `SocketInitializer`가 시작되고 `SocketConfig.Instance.IsServerEnabled`를 수신합니다.
2. 서비스가 활성화되면 'SocketManager'가 TCP 서버를 시작합니다.
3. 요청을 받은 후 현재 구성된 프로토콜 모드에 따라 JSON 또는 Text를 통해 배포합니다.
4. JSON 요청은 `EventName`에 의해 `ISocketJsonHandler` 구현과 일치됩니다.
5. 보내고 받은 메시지는 `SocketMessageManager`에 의해 관리되는 SQLite 데이터베이스에 기록됩니다.
6. `SocketStatusBarProvider` 및 `SocketManagerWindow`는 관리자로부터 상태 및 메시지 목록을 읽습니다.

## 현재 구현의 경계는 무엇입니까?

### 순수 JSON 프로토콜 라이브러리가 아닙니다.JSON은 기본 모드 중 하나이지만 현재 구현에서는 'SocketPhraseType.Text'도 지원합니다. 전체 모듈을 "통합 JSON 프로토콜 계층"으로 작성하면 텍스트 모드와 상태 표시줄, 창 및 지속성의 실제 책임을 놓치게 됩니다.

### 프로세서 인터페이스뿐만 아니라

이전 문서는 `ISocketJsonHandler`에 중점을 두었지만 현재 모듈의 값도 다음에서 나옵니다.

- 초기화
- 관리창
- 상태 표시줄 항목
- SQLite 메시지 기록

핸들러 확장점만 작성하면 모듈을 플랫하게 작성하기 쉽습니다.

### 구성 필드는 실제 클래스에 따라 설명되어야 합니다.

현재 'SocketConfig'에는 이전 문서에서 주장한 'ReceiveTimeout', 'SendTimeout' 및 'AutoReconnect' 필드가 없습니다. 통신 구성을 설명할 때 실제 속성이 우선해야 합니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 서버와 유통메인체인을 보고싶다

먼저 살펴보세요:

-`SocketManager.cs`
-`SocketInitializer.cs`
- `ISocketJsonHandler.cs`

### 설정 및 상태 표시줄 액세스를 확인하고 싶습니다.

먼저 살펴보세요:

-`SocketConfig.cs`
-`SocketConfigProvider.cs`
-`SocketStatusBarProvider.cs`

### 메시지 기록 및 관리창을 보고싶다

먼저 살펴보세요:

-`SocketMessage.cs`
-`SocketMessageManager.cs`
-`SocketManagerWindow.xaml.cs`

## 경로 최적화

이 모듈에 대한 후속 최적화 권장 사항은 네 가지 수준으로 제안됩니다.

| 스테이지 | 목표 | 집중 |
| --- | --- | --- |
| P0 안정성 | 서비스 수명주기 및 TCP 경계 강화 | 재시작 방지, 취소 토큰, 통합 중지 경로, 고정/하프 패킷 처리 |
| P1 가시성 | 현장 문제 해결 효율성 향상 | 메시지 내보내기, 연결 수명 주기, 오류 통계, 처리 시간 |
| P2 프로토콜화 | 외부 장치 도킹 비용 절감 | 오류 코드, 핸들러 메타데이터, JSON 스키마, 버전 호환성 |
| P3 성능 및 용량 | 장기 운영 및 더 큰 역사적 볼륨 지원 | 페이징 로딩, 데이터베이스 인덱싱, 일괄 쓰기, 보존 정책 |

자세한 경로는 [소켓 통신 모듈 최적화 경로](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)를 참조하세요.

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 조작된 통합 메시지 필드 모델
- 실제 클래스와 일치하지 않는 구성 항목 목록
- 핸들러 예제만 있고 관리 창은 없으며 지속성 경계에 대한 소개가 없습니다.
- 현재 모듈을 실제 UI 통신 모듈이 아닌 순수 프로토콜 사양으로 작성

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [소켓 통신 모듈 최적화 경로](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)