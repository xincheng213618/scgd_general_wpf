# 시스템모니터 플러그인

이 페이지에서는 현재 웨어하우스에 실제로 존재하는 SystemMonitor 플러그인 구현에 대해서만 설명하며 더 이상 "버전 정보 + 튜닝 매뉴얼 + 이상적인 아키텍처 다이어그램"의 이전 초안을 유지하지 않습니다.

## 먼저 이 플러그인이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 SystemMonitor는 경량 시스템 모니터링 플러그인입니다. 핵심은 독립적인 애플리케이션 셸이 아니라 싱글톤 모니터링 서비스를 중심으로 한 통합 지점 집합입니다.

- `SystemMonitors`: 데이터 및 명령을 모니터링하기 위한 중앙 싱글톤입니다.
- `SystemMonitorProvider`: 플러그인을 설정 페이지 및 도구 메뉴에 연결합니다.
- `SystemMonitorIStatusBarProvider`: 선택적 모니터링 항목을 기본 프로그램 상태 표시줄에 통합합니다.
- `SystemMonitorControl`: 실제로 모니터링 데이터를 표시하는 WPF 컨트롤입니다.

따라서 무겁고 독립적인 윈도우 프로그램이라기보다는 "시스템 모니터링 서비스 + UI 접근 레이어"에 더 가깝습니다.

## 현재 가장 중요한 파일

- `플러그인/SystemMonitor/manifest.json`
- `플러그인/SystemMonitor/SystemMonitors.cs`
- `플러그인/SystemMonitor/SystemMonitorControl.xaml(.cs)`
- `플러그인/SystemMonitor/SystemMonitorIStatusBarProvider.cs`

그중 `SystemMonitors.cs`는 실제 런타임 로직의 대부분을 담당합니다. 다른 두 가지 유형은 주로 호스트 UI에 연결하는 역할을 합니다.

## 현재 기능 영역에는 실제로 무엇이 포함되나요?

`SystemMonitors` 구현으로 판단하면 현재 이 플러그인이 다루는 모니터링 영역은 이전 문서의 "시간 + RAM"보다 확실히 더 넓습니다.

### 성능 카운터

플러그인은 Windows 성능 카운터를 비동기식으로 초기화하고 정기적으로 업데이트합니다.

- 시스템 CPU 사용량
- 현재 프로세스 CPU 사용량
- 시스템 RAM 사용량
- 현재 프로세스 개인 작업 세트

성능 카운터 초기화가 실패하면 현재 구현에서는 전체 플러그인을 중단하는 대신 이러한 값을 새로 고치지 않도록 대체합니다.

### 디스크 및 네트워크

플러그인은 현재 활발하게 로드되고 유지 관리됩니다.

- 준비된 모든 디스크의 용량, 사용 공간, 여유 공간, 점유율
- 비루프백/터널 네트워크 인터페이스 정보
- IPv4 주소, MAC 주소, 링크 속도 및 네트워크 인터페이스 상태

데이터의 이 부분은 상태 표시줄 스위치에 의존하지 않습니다. 상태 표시줄은 일부를 기본 창 하단에 투영할지 여부를 결정합니다.

### 프로세스 및 런타임 환경

현재 수집 중인 항목:

- 메모리 사용량이 높은 상위 10개 프로세스
- 현재 프로세스의 스레드 수와 핸들 수
- 시스템 시작 시간, 애플리케이션 실행 시간, 시스템 실행 시간
- CPU 이름, 호스트 이름, .NET 런타임, 시스템 아키텍처, 사용자 이름
- 홈 화면 해상도

### GPU 및 캐시

플러그인은 또한 `ConfigCuda.Instance`를 읽고 사용 가능한 경우 CUDA 장치 이름과 비디오 메모리 정보를 표시합니다. 또한 캐시 크기 통계 및 정리 명령도 제공합니다.

## 현재 호스트에 연결된 3개의 체인

### 설정 페이지

`SystemMonitorProvider`는 `IConfigSettingProvider`를 구현하고 `SystemMonitors.GetInstance()`를 설정 페이지 데이터 소스로 사용하며 `SystemMonitorControl`을 디스플레이 컨트롤로 사용합니다.

즉, 설정 페이지와 별도의 팝업 창에서는 실제로 두 세트의 모니터링 인스턴스가 아닌 동일한 단일 인스턴스 데이터를 본다는 의미입니다.

### 도구 메뉴

동일한 `SystemMonitorProvider`는 현재 `Tool` 메뉴 아래에 "Performance Monitoring" 항목을 삽입하고 `SystemMonitorControl`을 호스팅하는 일반 WPF 창을 여는 `IMenuItemProvider`도 구현합니다.

### 상태 표시줄

`SystemMonitorIStatusBarProvider`는 구성 스위치에 따라 상태 표시줄 항목이 존재하는지 여부를 동적으로 결정하는 `IStatusBarProviderUpdatable`을 구현합니다. 현재 상태 표시줄에 투영되는 항목은 다음과 같습니다.

- 시간
- 애플리케이션 실행 시간
- CPU 텍스트
- RAM 텍스트
- 디스크 아이콘 및 남은 공간

따라서 이전 문서와 같이 두 개의 고정 항목이 있는 정적 상태 표시줄 공급자가 아닙니다.

## 현재 구성 모델

`SystemMonitorSetting`에는 현재 최소한 다음 스위치와 매개변수가 포함되어 있습니다.

- `업데이트 속도`
- `DefaultTimeFormat`
- 'IsShowTime'
- `IsShowRAM`
- `IsShowCPU`
- `IsShowUptime`
- `IsShowDisk`

이전 문서에서는 더 이상 완전히 다루지 않는 시간과 RAM에 대해서만 기록합니다.

## 현재 명령 표면

현재 'SystemMonitors'에 의해 노출되는 사용자 명령은 주로 다음과 같습니다.

- `ClearCacheCommand`
-`RefreshDrives명령`
- `RefreshNetworkCommand`
-`새로 고침 프로세스 명령`

이러한 명령에 해당하는 실제 작업은 애플리케이션 데이터 및 로그 디렉터리를 정리하고, 디스크 목록을 다시 로드하고, 네트워크 인터페이스 목록을 다시 로드하고, 점유율이 높은 프로세스 목록을 다시 로드하는 것입니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 독립형 창 프로그램 중심 플러그인이 아닙니다.

메뉴를 실행하면 창이 열리지만 해당 창에는 `SystemMonitorControl`만 마운트되어 있습니다. 실제로 지속적으로 실행되는 핵심 개체는 'SystemMonitors' 싱글톤입니다.

### 단순한 상태 표시줄 시간 플러그인이 아닙니다.

현재 상태 표시줄은 세 가지 통합 체인 중 하나일 뿐입니다. 실제로 많은 양의 데이터는 디스크, 네트워크, GPU, 프로세스 목록 및 캐시 통계를 포함한 완전한 모니터링 제어 기능을 제공합니다.

### `IStatusBarProviderUpdatable`이 중요합니다.

상태 표시줄 표시 항목 새로 고침은 현재 `SystemMonitorIStatusBarProvider`를 사용하여 구성 변경을 수신한 후 `StatusBarItemsChanged`를 트리거합니다. 실수로 일반 정적 공급자로 작성하면 현재 동적 새로 고침 체인이 편향적으로 작성됩니다.

### 유형 이름 지정 및 네임스페이스를 당연하게 여기지 마십시오.

`SystemMonitors` 및 `SystemMonitorSetting`은 현재 플러그인 자체 `SystemMonitor` 네임스페이스가 아닌 `ColorVision.UI.Configs` 네임스페이스에 있습니다. 이것은 현재 코드의 일부입니다. 승인 없이 "플러그인의 내부 독립 시스템"으로 다시 언급하지 마십시오.

## 추천읽기순서

1. `플러그인/SystemMonitor/SystemMonitors.cs`
2. `플러그인/SystemMonitor/SystemMonitorControl.xaml.cs`
3. `플러그인/SystemMonitor/SystemMonitorIStatusBarProvider.cs`
4. `플러그인/SystemMonitor/manifest.json`

이를 통해 먼저 실제 제어 표면을 캡처한 다음 메뉴, 상태 표시줄 및 로딩 정보로 돌아갈 수 있습니다.

## 계속 읽기

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [기존 플러그인 기능](../README.md)
