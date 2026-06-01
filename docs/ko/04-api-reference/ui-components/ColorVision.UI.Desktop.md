#ColorVision.UI.Desktop

이 페이지에서는 현재 구현된 데스크탑 창과 UI/ColorVision.UI.Desktop의 지원 기능에 대해서만 설명하고 더 이상 이전 문서에서 "전체 시스템의 기본 프로그램 입구"라는 작성 방법을 계속하지 않습니다.

## 모듈 포지셔닝

`ColorVision.UI.Desktop`은 현재 데스크탑 보조 셸 기능 세트에 더 가깝고 주로 다음을 제공합니다.

- 설정 창
- 구성 마법사
- 메뉴 항목 관리 창
- 구성 관리 창
- 타사 애플리케이션 액세스
- DLL 정보 조회 등의 보조 창

창고 전체의 주요 적용 출입구는 아닙니다. 현재 실제 메인 프로그램 프로젝트는 `ColorVision/`에 있는데 여기서 `App.xaml.cs`와 `MainWindow.xaml.cs`는 매우 가볍습니다.

## 현재 가장 중요한 디렉토리

프로젝트 디렉토리에서 가장 먼저 읽어야 할 내용은 다음과 같습니다.

- `설정/`: 통합 설정 창
- `Wizards/`: 마법사 창, 단계 검색, 창 구성
- `MenuItemManager/`: 메뉴 항목 관리 및 지속성
- `ThirdPartyApps/`: 시스템 도구 및 타사 애플리케이션 입구
- `Marketplace/`: DLL 버전 보기 및 기타 보조 창
- `ConfigManagerWindow.xaml(.cs)`: 구성 관리 창
- `Feedback/`, `Download/`, `TimedButtons/`, `WebViewService.cs`: 기타 데스크톱 보조 기능

## 키 입력 유형

### 앱 및 MainWindow

현재 `App.xaml.cs`는 매우 가벼운 `Application` 부분이고 `MainWindow.xaml.cs`는 기본 구성 논리만 유지합니다.

이는 다음을 의미합니다.

- 이 프로젝트에는 `App`과 `MainWindow`가 있습니다.
- 그러나 이전 문서에 설명된 대로 완전한 시작 프로세스와 시스템 초기화 논리를 전달하는 중앙 파일은 아닙니다.

이 프로젝트를 읽을 때 빈 쉘 입구에 초점을 맞추기보다는 다양한 기능 창과 관리자를 먼저 살펴보는 것이 정말 가치가 있습니다.

### 설정 창

'Settings/SettingWindow.xaml.cs'는 현재 설정 시스템의 기본 데스크톱 항목입니다. 다음을 담당합니다.

- `ConfigSettingManager.GetInstance().GetAllSettings()` 읽기
- 그룹별로 탭 생성
- 탭 페이지, 전체 유형의 속성 페이지 또는 `ConfigSettingType`을 기반으로 하는 단일 속성 컨트롤을 생성할지 결정합니다.
- 창 초기화 중에 모든 뷰를 한 번에 인스턴스화하지 않도록 'ViewType'의 지연 로딩

따라서 이 페이지의 이전 문서에 있는 "통합 설정 창"의 방향은 맞지만 구현 세부 사항은 `ConfigSettingManager` + 지연 로딩에 속해야 합니다.

### WizardManager / WizardWindow / WizardWindowConfig

현재 마법사 체인은 다음과 같은 유형의 그룹입니다.

- `WizardManager`: 반사 스캔 `IWizardStep`
- `WizardWindow`: 다단계 창 및 완료 논리
- `WizardWindowConfig`: 창 구성 및 완료 상태

'WizardManager'는 어셈블리를 순회하고 'IWizardStep'을 인스턴스화한 다음 'Order'를 기준으로 정렬합니다. 'WizardWindow'는 진행률 표시줄을 구동하고 이전 단계와 다음 단계 사이를 전환하며 확인을 완료합니다.

이 부분은 현재 프로젝트에서 가장 명확한 "데스크탑 보조 프로세스 체인"입니다.

### MenuItemManagerConfig 및 MenuItemManagerWindow

`MenuItemManagerConfig`는 현재 메뉴 항목 설정의 지속성을 담당하는 반면 `MenuItemManagerWindow`는 시각적 관리 인터페이스를 제공합니다. 전역 메뉴 런타임 자체가 아닌 UI 셸 구성 도구에 속합니다.

### 구성 관리자 창

'ConfigManagerWindow'는 보다 중앙화된 관점에서 구성 항목을 관리하는 데 사용되는 또 다른 데스크탑 측 관리 창입니다. `SettingWindow`와 완전히 겹치지 않으며 기본 인터페이스 레이어가 아닌 데스크톱 도구 레이어에 속합니다.

### ViewDllVersions창

`Marketplace/ViewDllVersionsWindow.xaml.cs`는 현재 로드된 어셈블리를 트래버스하고 다음을 수집합니다.

- 이름
- 조립 버전
- 파일 버전
- 제품 버전
- 회사정보
- 경로

핵심 플러그인 업데이트 프로세스 자체보다 런타임 진단 및 문제 해결 창에 가깝습니다.

### SystemAppProvider 및 WebViewService

- 'ThirdPartyApps/SystemAppProvider.cs'는 시스템 도구 및 타사 애플리케이션 진입을 담당합니다.
- 'WebViewService.cs'는 이 프로젝트에 일부 데스크톱 WebView 관련 기능도 포함되어 있음을 나타냅니다.

## 현재 런타임 메인 체인

이 프로젝트에는 현재 단일 메인 체인이 없지만 여러 데스크톱 보조 체인이 공존합니다. 더 주목해야 할 점은 다음과 같습니다.

1. 설정 체인: `SettingWindow` -> `ConfigSettingManager` -> 구성 페이지/속성 페이지의 지연 로딩.
2. 마법사 체인: `WizardManager` -> `IWizardStep` 검색 -> `WizardWindow` 전환 및 완료.
3. 관리 체인: `MenuItemManagerWindow` / `ConfigManagerWindow` / `ViewDllVersionsWindow`는 다양한 측면에서 데스크톱 관리 창을 제공합니다.

## 현재 구현의 경계는 무엇입니까?

### 전체 시스템의 주 출입구는 아닙니다.

이것이 이 페이지에서 가장 흔한 실수입니다. 현재 프로젝트의 `App`과 `MainWindow`는 매우 가벼워서 `ColorVision.UI.Desktop`이 계속해서 전체 제품의 유일한 시작 센터가 될 수는 없습니다.

### 모든 기능이 MainWindow를 중심으로 이루어지는 것은 아닙니다.

이 프로젝트는 창 및 관리 도구 모음에 가깝습니다. 많은 가치는 하나의 거대한 기본 창 오케스트레이션 레이어가 아닌 독립적인 창에서 비롯됩니다.

### 이전 문서에 언급된 SystemInitializer는 이 프로젝트에 존재하지 않습니다.

현재 `UI/ColorVision.UI.Desktop` 디렉토리에는 실제 `SystemInitializer` 구현이 없습니다. 이전 문서에는 이를 기존 구성 요소로 나열하여 독자가 존재하지 않는 제어 지점을 찾도록 직접적으로 오해를 불러일으킵니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 설정 및 구성 창을 보고 싶습니다.

먼저 살펴보세요:

-`설정/SettingWindow.xaml.cs`
- `ConfigManagerWindow.xaml.cs`

### 마법사 및 최초 구성 프로세스를 보고 싶습니다.

먼저 살펴보세요:

- `마법사/WizardWindow.xaml.cs`
-`마법사/WizardWindowConfig.cs`

### 메뉴관리 및 바탕화면 보조창을 보고싶다

먼저 살펴보세요:-`MenuItemManager/MenuItemManagerConfig.cs`
-`MenuItemManager/MenuItemManagerWindow.xaml.cs`
- `Marketplace/ViewDllVersionsWindow.xaml.cs`
- `ThirdPartyApps/SystemAppProvider.cs`

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

-이 프로젝트를 전체 시스템의 주요 프로그램 입구로 작성하십시오.
- `SystemInitializer`와 같은 구성요소 설명이 존재하지 않습니다.
- 큰 버전 번호 및 의사 API 목록
- 경량 `App` / `MainWindow`를 완전한 시작 프로세스 센터로 확장

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)