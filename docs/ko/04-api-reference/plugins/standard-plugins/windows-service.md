# WindowsServicePlugin 플러그인

이 페이지에서는 현재 웨어하우스에 실제로 존재하는 WindowsServicePlugin 구현에 대해서만 설명하며 "운영 및 유지 관리 플랫폼 일반 매뉴얼 + 크고 완전한 API 디렉터리" 스타일의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 플러그인이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 WindowsServicePlugin은 단순한 "서비스 로그 바로가기" 모음이 아니라 로컬 Windows 서비스 운영 및 유지 관리를 중심으로 진행되는 플러그인 패키지입니다. 현재 가장 명확한 능력 라인은 다음과 같습니다.

- 도움말 메뉴의 서비스 관리자 항목.
- 서비스 설치 및 업데이트 창입니다.
- 서비스 로그 및 로컬 로그 디렉터리에 빠르게 액세스할 수 있습니다.
- CVWinSMS 프로필 및 업데이트 패키지에 연결됩니다.
- 마법사 단계에서 구성 읽기 및 CFG 덮어쓰기.

따라서 이전 문서의 일반적인 "서비스 도구 상자"보다 더 구체적입니다. 실제 중심은 두 개의 제어 체인 `ServiceManagerViewModel`과 `ServiceInstallViewModel`입니다.

## 현재 가장 중요한 파일

- `플러그인/WindowsServicePlugin/manifest.json`
-`플러그인/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
- `플러그인/WindowsServicePlugin/ServiceManager/ServiceManagerWindow.xaml.cs`
-`플러그인/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
- `플러그인/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml.cs`
-`플러그인/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
-`플러그인/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
-`플러그인/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
- `플러그인/WindowsServicePlugin/SetMysqlConfig.cs`
-`플러그인/WindowsServicePlugin/SetServiceConfig.cs`
- `플러그인/WindowsServicePlugin/Menus/ServiceLog.cs`

플러그인이 호스트에 어떻게 들어가는지, 서비스 관리자를 여는 방법, 구성 동기화 및 업데이트를 수행하는 방법만 알고 싶다면 이 파일들이 이미 본체를 다루었습니다.

## 현재 호스트에 연결된 여러 체인

### 도움말 메뉴의 서비스 관리자 항목

`MenuServiceManager`는 현재 `도움말` 메뉴 아래에 있으며 실행 중에 `ServiceManagerWindow`를 직접 엽니다.

또한 동일한 파일의 `ServiceManagerAppProvider`는 `ITirdPartyAppProvider`도 구현하여 "Service Manager"를 호스트의 타사 애플리케이션 입구에 대한 내부 도구로 노출합니다.

이는 단지 하나의 메뉴 명령이 아니라 최소한 두 개의 UI 액세스 체인이 있음을 의미합니다.

### 서비스 로그 메뉴 트리

'ServiceLog'는 현재 '도움말' 메뉴 아래의 루트 메뉴 항목이기도 합니다. 그 주위에 플러그인은 계속해서 여러 로그 빠른 항목 세트를 주입합니다.

- HTTP 로그 페이지
- `CVWinSMSConfig.BaseLocation`을 기반으로 구문 분석된 로컬 로그 디렉터리

예를 들어 `ExportRCServiceLog` 및 `Exportx64ServiceLog`와 같은 유형은 로컬 URL을 직접 엽니다. 접미사가 있는 디렉터리 버전은 서비스 디렉터리의 `log` 폴더를 연결합니다.

### 마법사 및 초기화 진입

`InstallTool`은 현재 다음 두 가지를 모두 구현합니다.

-`MenuItemBase`
- `IWizardStep`
-`IMinWindow초기화됨`

CVWinSMS를 열거나 찾기 위한 메뉴 항목으로 사용할 수 있으며, 메인 창을 초기화한 후 업데이트를 확인할 수도 있고, 설치 마법사의 집계 체인에 들어갈 수도 있습니다.

그러므로 이 플러그인은 "서비스 관리창만"처럼 단순하지 않습니다. CVWinSMS와 관련된 지침 및 업데이트 논리도 호스트 액세스 인터페이스의 일부입니다.

### 매니페스트 정보

현재 `manifest.json`에 따르면 플러그인이 노출하는 로딩 정보는 다음과 같습니다.

- `Id = "WindowsServicePlugin"`
- `이름 = "비주얼 컬러 서비스 플러그인"`
- `버전 = "1.0"`
- `dllpath = "WindowsServicePlugin.dll"`
- `필요 = "1.3.12.34"`

이는 이전 문서에 추가로 설명된 종속성 매트릭스보다 현재 실제 로드 모델에 더 가깝습니다.

## 현재 서비스 관리자의 업무 방식

'ServiceManagerWindow' 자체는 매우 얇습니다. 윈도우가 초기화되면 `DataContext`가 `ServiceManagerViewModel.Instance`로 직접 설정되고, 로그 텍스트가 변경되면 로그 영역이 자동으로 스크롤됩니다.

실제 런타임 센터는 `ServiceManagerViewModel`에 있습니다. 현재 구현된 대로 최소한 다음을 담당합니다.

- 기본 서비스 목록을 유지합니다.
- MySQL 및 MQTT 관리자를 유지 관리합니다.
- 현재 버전, 사용 가능한 버전, 사용 중 상태, 진행 상황 및 로그 텍스트를 유지합니다.
- 관리자 모드 상태 및 "관리자로 다시 시작" 명령이 노출되었습니다.
- 원클릭 시작, 중지, 새로 고침, 디렉터리 열기, 구성 파일 열기 등의 명령을 노출합니다.

### 현재 기본적으로 관리되는 서비스

현재 `ServiceManagerConfig.GetDefaultServiceEntries()`에 명시적으로 나열되어 있습니다.

- `등록센터서비스`
- `CVMainService_x64`
-`CVMainService_dev`
-`CVArchService`

따라서 이 문서는 "임의의 서비스 조정 프레임워크"로 계속 일반화되기보다는 이러한 실제 서비스 항목을 중심으로 작성되어야 합니다.

### 경로 및 버전 감지

`ServiceManagerConfig`는 현재 다음을 시도합니다.

1. 레지스트리의 `RegistrationCenterService`에서 설치 경로를 읽습니다.
2. 실패한 경우 CVWinSMS의 `App.config`에서 `BaseLocation` 및 `MysqlPort` 읽기를 다시 시도합니다.

`RefreshAll()`은 각 서비스의 상태를 우연히 새로 고치고 `RegistrationCenterService`의 버전 텍스트를 기반으로 현재 버전 표시를 업데이트합니다.

## 현재 설치 및 업데이트 체인을 확장하는 방법은 무엇입니까?

`ServiceInstallWindow` 자체도 매우 얇으며 핵심 로직은 `ServiceInstallViewModel`에 있습니다. 현재 이 체인이 실제로 관리하는 것은 다음과 같습니다.

- 서비스 설치 패키지 선택
- MySQL ZIP 선택
- MQTT 설치 프로그램 선택
- 다운로드 디렉토리 선택
- 온라인으로 업데이트를 확인하세요.
- 백업 및 복원
- 한 번의 클릭으로 모든 구성요소 설치현재 구현에 따르면 이 창은 단일 "최신 버전 다운로드"와 관련이 없지만 진행률, 로그, 자동 시작, 데이터베이스 업데이트 및 백업 스위치를 포함한 전체 설치 및 조정 상태와 관련이 있습니다.

## CVWinSMS와의 현재 관계

`CVWinSMSConfig`는 `CVWinSMSPath`, 업데이트 주소 및 자동 업데이트 스위치를 유지하고 외부 `App.config`에서 구문 분석된 `BaseLocation`을 제공하는 역할을 담당합니다.

`InstallTool`은 다음을 담당합니다.

- 기존 CVWinSMS 실행 파일을 감지합니다.
- 필요할 때 업데이트를 다운로드하세요.
- 이전 디렉터리의 압축을 풀고 교체합니다.
- 관리자 권한으로 CVWinSMS를 다시 시작합니다.

이는 WindowsServicePlugin이 현재 별도의 폐쇄형 서비스 운영 및 유지 관리 UI 집합이 아니라 외부 CVWinSMS 도구에 대한 브리징 및 마이그레이션 논리를 명시적으로 전달한다는 것을 보여줍니다.

## 현재 마법사 단계는 어떻게 구현되나요?

### 서비스 구성 읽기

`SetMysqlConfig`는 CVWinSMS 디렉터리에서 `config/App.config`를 읽고 MySQL 구성을 현재 호스트에서 사용하는 데이터베이스 구성 개체에 다시 씁니다.

### 서비스 CFG 덮어쓰기

`SetServiceConfigStep`은 동일한 `App.config`를 읽은 다음 현재 호스트에 있는 것을 사용합니다.

- MySQL 설정
- MQTT 설정
- RC 설정

서비스 디렉터리를 업데이트하려면 다음 안내를 따르세요.

-`cfg/MySql.config`
-`cfg/MQTT.config`
-`cfg/WinService.config`

쓰기 저장이 완료된 후 'RegistrationCenterService'를 다시 시작하려고 시도합니다. 이는 실제로 서버 구성을 수정하는 운영 및 유지 관리 체인이므로 계속해서 "일반 마법사 버튼"으로 작성해서는 안 됩니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 단순한 로그 메뉴 플러그인이 아닙니다

현재 전체 로그 항목 세트가 있지만 플러그인의 주요 본체는 여전히 서비스 관리 및 설치 업데이트 제어 체인입니다. 로그 메뉴만 작성한다면 메인 구현면이 너무 작아질 것입니다.

### `Application` 셸은 호스트 확장의 초점이 아닙니다.

저장소에 'App.xaml.cs'가 있지만 현재는 독립 실행형 실행 또는 디버그 셸처럼 작동합니다. 기본 프로그램 플러그인 문서의 경우 이 '애플리케이션' 유형을 일일 플러그인 항목으로 착각하기보다는 매니페스트, 메뉴, 공급자, 보기 모델 및 마법사 단계에 더 주의를 기울여야 합니다.

### 구성 동기화는 실제로 서버 CFG를 변경합니다.

`SetServiceConfigStep`은 읽기 전용 검사기가 아닙니다. 현재 호스트 구성을 여러 서비스 디렉터리의 구성 파일에 다시 쓰고 등록 센터 서비스를 다시 시작하려고 합니다.

### 서비스 관리자는 현재 싱글톤 센터입니다.

'ServiceManagerViewModel.Instance'는 현재 창과 명령이 공유하는 상태 센터입니다. "창이 열릴 때마다 컨텍스트를 재구성하는" 모델로 계속 작성하는 것은 현재 구현과 일치하지 않습니다.

## 추천읽기순서

1.`플러그인/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
2. `플러그인/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
3. `플러그인/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
4. `플러그인/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
5. `플러그인/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
6. `플러그인/WindowsServicePlugin/SetMysqlConfig.cs`
7. `플러그인/WindowsServicePlugin/SetServiceConfig.cs`
8. `플러그인/WindowsServicePlugin/메뉴/ServiceLog.cs`
9. `플러그인/WindowsServicePlugin/manifest.json`

이런 방식으로 호스트 입구를 먼저 볼 수 있고 그 다음 상태 센터, 구성 브리지 및 설치 체인을 볼 수 있습니다.

## 계속 읽기

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/eventvwr.md](./eventvwr.md)
