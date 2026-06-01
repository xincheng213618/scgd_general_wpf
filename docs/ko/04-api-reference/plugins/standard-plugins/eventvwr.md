#EventVWR 플러그인

이 페이지에서는 현재 웨어하우스에 있는 실제 EventVWR 플러그인 구현에 대해서만 설명하며 "전체 하위 시스템 매뉴얼 + 이상적인 API 테이블"의 이전 초안은 더 이상 유지되지 않습니다.

## 먼저 이 플러그인의 기능을 살펴보겠습니다.

현재 소스 코드로 판단하면 EventVWR은 주로 두 가지 작업을 수행합니다.

- 읽기 전용 Windows 애플리케이션 이벤트 오류 보기 창을 제공합니다.
- Windows 오류 보고의 LocalDumps 레지스트리 키를 쓰거나 지우기 위한 덤프 구성 메뉴 세트를 제공합니다.

따라서 복잡한 진단 플랫폼이 아니라 "이벤트 창 + 덤프 구성 메뉴"의 두 가지 매우 직접적인 기능 체인입니다.

## 현재 가장 중요한 파일

- `플러그인/EventVWR/EventVWRPlugins.cs`
- `플러그인/EventVWR/ExportEventWindow.cs`
- `플러그인/EventVWR/EventWindow.xaml(.cs)`
- `플러그인/EventVWR/Dump/DumpConfig.cs`
- `플러그인/EventVWR/Dump/MenuDump.cs`
- `플러그인/EventVWR/manifest.json`

플러그인이 호스트에 어떻게 진입하는지, 이벤트 창을 여는 방법, 덤프 설정을 수정하는 방법만 알고 싶다면 이 몇 가지 코드면 충분합니다.

## 현재 호스트에 연결된 두 개의 메뉴 체인

### 이벤트 창 항목

`ExportEventWindow`는 `MenuItemBase`를 상속하며 현재 `Help` 메뉴 아래에 정지되어 있습니다.

-`OwnerGuid = "도움말"`
- `GuidId = "EventWindow"`
- `주문 = 1000`

실행되면 `EventWindow` 대화상자가 열립니다.

이 항목에는 중요한 제약 조건도 있습니다. 'Execute()'에는 현재 'RequiresPermission(PermissionMode.Administrator)'가 있는데, 이는 이것이 순수한 로컬 보조 메뉴가 아니라 호스트 권한 모드의 적용을 받는다는 것을 나타냅니다.

### 덤프 설정 항목

`MenuDump`는 `Help` 메뉴 아래의 상위 메뉴 항목이기도 하며 `MenuThemeProvider`는 이에 대한 하위 메뉴를 계속 제공합니다.

- 각 `DumpType` 열거 항목
- DMP 지우기
- DMP 저장

따라서 EventVWR에는 현재 하나의 창 항목만 있는 것이 아니라 도움말 메뉴 아래에 두 개의 독립적인 기능이 있습니다.

## 현재 이벤트 창 작동 방식

EventWindow.xaml.cs`의 논리는 매우 간단합니다.

1. 창이 초기화되면 Windows `Application` 이벤트 로그를 엽니다.
2. `EventLogEntry`를 모두 읽어보세요.
3. `EntryType == Error`인 이벤트만 유지됩니다.
4. `TimeGenerated`를 기준으로 역순으로 정렬합니다.
5. 결과를 왼쪽 목록에 바인딩합니다.
6. 레코드를 선택하면 세부정보 영역에 '메시지'가 표시됩니다.

이는 현재 창에 복잡한 필터, 검색기 또는 비동기 페이징 논리가 없으며 본질적으로 "오류 이벤트 빠른 브라우저"임을 의미합니다.

## 현재 덤프 구성은 어떻게 구현되나요?

`DumpConfig`는 실제 시스템 설정을 작성하는 역할을 합니다. 현재 핵심 사항은 다음과 같습니다.

- 대상 레지스트리 경로는 `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`입니다.
- 기본 LocalDumps 구성을 먼저 읽은 후 현재 프로세스에 해당하는 `LocalDumps\{Name}.exe`를 덮어씁니다.
- 현재 관리되는 주요 분야는 다음과 같습니다.
  - `덤프폴더`
  - `덤프카운트`
  - `덤프 유형`
  - `CustomDumpFlags`

구성 작성 및 구성 지우기 모두 관리자 권한이 필요합니다. 현재 관리자가 아닐 경우 계속 진행하지 않고 팝업창이 뜹니다.

레지스트리 구성 외에도 `SaveDump()`는 `DumpHelper.WriteMiniDump(...)`를 호출하여 현재 프로세스 덤프를 대상 디렉터리에 씁니다.

## 현재 매니페스트 정보

`manifest.json`에 따르면 현재 이 플러그인이 노출하는 기본 정보는 다음과 같습니다.

- `ID = "EventVWR"`
- `이름 = "이벤트 플러그인"`
- `버전 = "1.0"`
- `dllpath = "EventVWR.dll"`
- `필요 = "1.3.15.10"`

이는 이전 문서의 "대상 프레임워크, 종속성 매트릭스, 완전한 API 테이블"보다 현재 플러그인 로딩 모델이 실제로 관심을 갖는 정보에 더 가깝습니다.

## 현재 실수하기 쉬운 몇 가지 사항

### 완전한 사고진단센터는 아닙니다

현재 구현에서는 Windows 애플리케이션 로그의 오류 항목만 읽고 메시지 텍스트를 표시합니다. 여러 로그 소스의 고급 검색, 내보내기 및 분석 기능을 갖춘 플랫폼으로 계속 작성하지 마십시오.

### 덤프 구성은 시스템 수준에서 기록됩니다.

'DumpConfig'는 현재 애플리케이션 내부 구성 파일이 아닌 HKLM에서 LocalDumps 레지스트리 키를 작동합니다. 그렇기 때문에 쓰기와 정리 모두 관리자 권한이 필요합니다.

### 플러그인 엔트리 클래스 자체가 매우 가볍습니다.

`EventVWRPlugins`는 이제 주로 헤더와 설명을 제공하는 매우 얇은 `IPluginBase` 쉘입니다. 실제 기능 입구는 여기가 아니라 메뉴 항목과 해당 창/구성 클래스에 있습니다.

### 권한 경계는 두 개의 레이어로 나누어집니다.

- 이벤트 창 메뉴 항목 자체에는 `RequiresPermission(PermissionMode.Administrator)`이 적용됩니다.
- 덤프 레지스트리 쓰기 및 정리는 실행 시 관리자 권한을 다시 확인합니다.

단 하나의 레이어만 문서화했다면 문서화로 인해 현재 동작이 지나치게 단순화될 것입니다.

## 추천읽기순서

1.`플러그인/EventVWR/ExportEventWindow.cs`
2. `플러그인/EventVWR/EventWindow.xaml.cs`
3. `플러그인/EventVWR/Dump/DumpConfig.cs`
4. `플러그인/EventVWR/Dump/MenuDump.cs`
5. `플러그인/EventVWR/manifest.json`

이런 방식으로 먼저 호스트 입구를 확인한 다음 창 동작과 시스템 수준 구성 지점을 볼 수 있습니다.

## 계속 읽기

- [플러그인/README.md](../../../../플러그인/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)