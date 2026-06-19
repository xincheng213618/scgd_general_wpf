# 스펙트럼 플러그인

이 페이지에서는 현재 웨어하우스에 실제로 존재하는 Spectrum 플러그인 구현에 대해서만 설명하며 "버전 테이블 + 기능 승격 + 이상적인 API 매뉴얼"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 플러그인이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 Spectrum은 단편화된 장치 드라이버 예제가 아니라 독립적인 스펙트럼 테스트 창을 중심으로 하는 플러그인 워크벤치입니다. 현재 최소한 4개의 명시적인 실행 체인이 포함되어 있습니다.

- 도구 메뉴의 창 항목.
- `Spectrum` 대상 창의 자체 메뉴 및 상태 표시줄입니다.
- 'SpectrometerManager'를 중심으로 연결, 교정 및 측정 제어.
- 'ViewResultManager' 주변의 결과 표시, SQLite 지속성 및 측정 초상화 로깅.

따라서 이는 이전 문서의 "분광계 테스트 도구"에 대한 일반적인 설명보다 더 구체적이며 실제로는 완전하지만 여전히 단일 창 중심의 측정 워크벤치입니다.

## 현재 가장 중요한 파일

-`플러그인/스펙트럼/manifest.json`
- `플러그인/스펙트럼/MainWindow.xaml(.cs)`
- `플러그인/스펙트럼/MainWindow.Connection.cs`
- `플러그인/스펙트럼/MainWindow.Measurement.cs`
-`플러그인/스펙트럼/SpectrometerManager.cs`
- `플러그인/스펙트럼/데이터/ViewResultManager.cs`
- `플러그인/스펙트럼/SpectrumStatusBarProvider.cs`
- `플러그인/스펙트럼/교정/교정GroupWindow.xaml.cs`
- `플러그인/스펙트럼/License/LicenseDatabase.cs`

플러그인이 호스트에 어떻게 들어가는지, 장치에 어떻게 연결하는지, 결과를 저장하는 방법만 알고 싶다면 이 코드들이 이미 본체에 다뤄져 있습니다.

## 현재 호스트에 연결된 여러 체인

### 창가 입구

`MenuSpectrumWindow`는 현재 `MenuItemBase`를 상속하고 `Tool` 메뉴 아래에 있으며 실행 중에 `MainWindow`를 직접 엽니다.

이는 Spectrum의 핵심 호스트 입구가 두꺼운 플러그인 항목 클래스가 아니라 메뉴 항목과 이후에 열리는 작업 창임을 보여줍니다.

### 창 수준 메뉴 및 상태 표시줄

`MainWindow`는 초기화될 때 호출됩니다:

- `MenuManager.GetInstance().LoadMenuForWindow("Spectrum", 메뉴)`
- `StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum")`

즉, 이 플러그인은 단지 메인 프로그램 메뉴의 항목이 아닙니다. 창이 열리면 `TargetName = "Spectrum"`을 대상으로 하는 로컬 메뉴 및 상태 표시줄 확장 세트도 있습니다.

### 상태 표시줄 공급자

`SpectrumStatusBarProvider`는 현재 이 정보를 `Spectrum` 창의 상태 표시줄로 가져옵니다.

- 연결 상태
- 하드웨어 모델
- SN 일련번호
- 현재 교정 그룹
- 전류 측정 모드
- 셔터 연결 상태
- CFW 필터 휠 연결 상태

SN 문자는 현재 클릭 복사도 지원하므로 읽기전용 꾸미기 항목은 아닙니다.

### 매니페스트 정보

현재 `manifest.json`에 따르면 플러그인이 호스트에 노출하는 로딩 정보는 다음과 같습니다.

- `Id = "스펙트럼"`
- `이름 = "스펙트럼"`
- `버전 = "1.0"`
- `dllpath = "Spectrum.dll"`
- `필요 = "1.3.15.8"`

이는 이전 문서의 사용자 정의 긴 버전 및 종속성 목록보다 현재 실제 로드 모델에 더 가깝습니다.

## 현재 런타임의 핵심은 누구인가요?

SpectrometerManager는 현재 플러그인의 가장 중요한 싱글톤 상태 센터입니다. 이는 `ConfigService`를 통해 획득되며 다음을 보유합니다.

-`스펙트럼 구성`
- `셔터컨트롤러`
- `FilterWheelController`
-`SmuController`
- 현재 장치 핸들
- 현재 연결 상태, 하드웨어 모델, 일련번호
- 현재 교정 그룹 구성 및 활동 그룹화
- 현재 측정 모드 텍스트

따라서 'MainWindow'는 UI 및 호출 체인 구성에 더 가깝고 파일 간 공유의 실제 측정 상태는 주로 'SpectrometerManager'에 수렴됩니다.

## 현재 연결 및 보정 작동 방식

`MainWindow.Connection.cs`는 현재 연결 체인의 실제 순서를 보여줍니다.

1. 연결하기 전에 'LicenseDatabase.Instance.SyncToLocal()'을 호출하여 라이선스를 동기화하세요.
2. `Spectrometer.CM_CreateEmission(...)`을 통해 핸들을 생성합니다.
3. 구성에 따라 USB 또는 COM 포트 초기화를 사용할지 결정하십시오.
4. 성공적으로 연결되면 장치 일련번호를 읽습니다.
5. 일련번호별로 교정 그룹 구성을 로드합니다.
6. 현재 파장 교정 파일과 진폭 교정 파일을 로드합니다.
7. SP100 매개변수를 적용합니다.

연결이 실패하면 현재 구현에서는 장치 목록 읽기도 시도합니다. 단일 장치가 감지되었지만 연결이 실패하면 문제는 일반적인 오류 상자를 표시하는 대신 라이센스 관리로 전달됩니다.

### 캘리브레이션 그룹핑은 단순한 파일 선택 상자가 아닙니다

'CalibrationGroupWindow'는 현재 분광계 SN별로 그룹화 구성을 관리합니다. 편집하는 동안 변경 사항은 일시적으로 메모리에 저장되며 저장을 클릭할 때까지 다시 기록되지 않습니다. 창을 직접 닫으면 저장되지 않은 변경 사항이 삭제됩니다.

이전 문서의 "측정을 계속하려면 교정 파일을 선택하십시오"라는 설명과 비교하면 이는 장비별로 그룹화된 구성 모델 세트가 더 명확합니다.

## 현재 측정 체인을 확장하는 방법은 무엇입니까?

`MainWindow.Measurement.cs`는 현재 단일 측정을 여러 명확한 단계로 나눕니다.

- 자동 제로 교정 사전 확인
- 자동 포인트
- 적응형 제로 교정
- 데이터 수집
- 렌더링 결과
- 지속적인 결과

특정 동작 측면에서 현재 구현은 더 이상 "장치 SDK를 한 번 호출한 다음 그리는" 것이 아닙니다.

- 자동 영점 조정은 'ShutterController'에 따라 다릅니다.
- 동기 주파수 모드는 `CM_Emission_GetDataSyncfreq(...)`를 사용합니다.
- 표준 모드는 시간 초과가 발생하면 재시도를 수행합니다.
- EQE 모드에서는 'SmuController'가 연결되고 전압 및 전류 결과가 창 구성 및 결과 개체에 다시 기록됩니다.

동시에 측정 프로세스에서는 각 단계의 소요 시간, 입력 스냅샷 및 성공 상태를 추가로 기록합니다.

## 현재 결과와 지속성이 구현되는 방식

'ViewResultManager'는 현재 단순한 메모리 내 목록 관리자가 아니라 플러그인의 데이터 드롭오프 지점입니다. 구현된 대로 다음과 같습니다.

- `AppData\Spectromer\Config\Spectrum.db`에 SQLite 데이터베이스를 유지합니다.
- `SpectrumModel` 결과 기록을 저장합니다.
- 'SpectrumMeasurementProfile' 측정 프로필을 유지합니다.
- 측정 단계 세부 정보를 JSON으로 저장합니다.
- EQE 필드를 업데이트하고 필요한 경우 총 경과 시간을 측정합니다.따라서 Spectrum은 현재 "측정하고 버릴 수 있는" 임시 도구가 아니며 이미 기본적인 데이터 추적 및 검토 기능을 포함하고 있습니다.

### 내보내기 및 나열 작업

현재 기본 창에는 다음 기능도 내장되어 있습니다.

- 상대 스펙트럼/절대 스펙트럼 전환
- CIE 다이어그램 연계 표시
- 보이는 열 복사
- 공통 스펙트럼 CSV 내보내기
- EQE 모드 CSV 내보내기
- 결과 삭제 및 데이터베이스 삭제

이러한 동작은 `MainWindow.Chart.cs`, `MainWindow.ListView.cs` 및 `MainWindow.Export.cs`에 분산되어 있습니다.

## 현재 어떤 추가 하위 시스템을 사용할 수 있나요?

### 레이아웃 지속성

'MainWindow'는 현재 'DockLayoutManager'를 통해 AvalonDock 레이아웃을 관리하고 창이 닫히면 자동으로 레이아웃을 저장합니다. 이는 단단한 단일 창이 아니라는 것을 의미합니다.

### 라이센스 동기화

'LicenseDatabase'는 현재 SQLite를 사용하여 가져온 라이선스 파일의 메타데이터를 추적하고 분광계에 연결하기 전에 글로벌 라이선스 디렉터리를 로컬 'license' 디렉터리와 동기화합니다.

### 독립적인 시작 쉘이 존재하지만 호스트 확장의 초점은 아닙니다.

실제로 저장소에는 독립 실행형 시작 시 테마, 로그, 소켓 및 기본 창을 초기화하는 'App.xaml.cs'가 있습니다. 그러나 현재 기본 프로그램 플러그인 로딩 모델에서 문서는 이 'Application' 클래스를 일일 호스트 확장 항목으로 실수로 작성하는 대신 매니페스트, 메뉴 항목, 공급자 및 창 본문에 더 많은 주의를 기울여야 합니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 단순히 "장치 연결 + 데이터 한 프레임 읽기"가 아닙니다.

현재 Spectrum 구현에는 라이센스 동기화, 교정 그룹화, 창 레이아웃, 상태 표시줄, SQLite 결과 로깅 및 측정 초상화가 통합되어 있습니다. 이를 경량 테스트 도구로 계속 작성하면 현재의 복잡성이 과소평가될 것입니다.

### 결과 지속성은 단순한 테이블이 아닙니다

현재 스펙트럼 결과 기록 외에도 `SpectrumMeasurementProfile` 및 단계 세부정보 JSON도 별도로 포함되어 있습니다. 이전 문서를 CSV로만 작성하여 내보내는 경우 실제 추적 체인이 누락됩니다.

### 교정은 SN별로 구성됩니다.

현재 교정 구성은 단순한 전역 단일 파일 경로가 아니라 일련 번호 및 활동 그룹에 바인딩되어 있습니다. 이 경계는 현장 장치 전환을 이해하는 데 중요합니다.

### 상태 표시줄은 창 수준 확장이며 전역 기본 프로그램 상주 항목이 아닙니다.

'SpectrumStatusBarProvider'의 대상 이름은 'Spectrum'이며, 이는 메인 프로그램의 모든 페이지에 표시되는 전역 상태 표시줄이 아니라 플러그인 창 내부의 상태 표시줄을 설명합니다.

## 추천읽기순서

1. `플러그인/스펙트럼/MainWindow.xaml.cs`
2. `플러그인/스펙트럼/SpectrometerManager.cs`
3. `플러그인/스펙트럼/MainWindow.Connection.cs`
4. `플러그인/스펙트럼/MainWindow.Measurement.cs`
5. `플러그인/스펙트럼/데이터/ViewResultManager.cs`
6. `플러그인/스펙트럼/SpectrumStatusBarProvider.cs`
7. `플러그인/스펙트럼/교정/교정GroupWindow.xaml.cs`
8. `플러그인/스펙트럼/manifest.json`

이런 방식으로 호스트 입구를 먼저 볼 수 있고, 상태 센터, 장치 체인 및 결과 드롭 지점을 볼 수 있습니다.

## 계속 읽기

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
