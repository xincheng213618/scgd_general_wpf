# 현재 UI DLL 문서 커버리지

이 페이지는 `UI/` 디렉터리의 모듈별 대장입니다. 인수인계 담당자는 현재 존재하는 UI 프로젝트, 해당 문서 페이지, DLL 릴리스 또는 교체 시 확인해야 할 증거를 여기서 먼저 확인한 뒤 개별 컴포넌트 페이지나 릴리스 절차로 이동합니다.

업데이트 날짜: 2026-06-10.

## 현재 결론

- 현재 `UI/` 아래에는 실제 프로젝트 디렉터리가 10개 있으며, 각 디렉터리에 `.csproj`와 `README.md`가 있습니다.
- 10개 프로젝트 모두 `docs/04-api-reference/ui-components/` 아래에 대응 문서 페이지가 있습니다.
- 10개 프로젝트 모두 `GeneratePackageOnBuild`를 사용하므로 릴리스 검증 시 호스트 출력 폴더뿐 아니라 NuGet 패키지 내용도 확인해야 합니다.
- `ColorVision.Common`, `ColorVision.Themes`, `ColorVision.UI`, `ColorVision.Core`, `ColorVision.Database`, `ColorVision.SocketProtocol`, `ColorVision.Scheduler`는 `net8.0-windows7.0`과 `net10.0-windows7.0`을 대상으로 합니다.
- `ColorVision.ImageEditor`, `ColorVision.UI.Desktop`, `ColorVision.Solution`은 현재 `net10.0-windows7.0`만 대상으로 합니다.
- `ColorVision.Core`는 native/runtime 위험이 가장 높은 UI 패키지입니다. `ColorVision.ImageEditor`는 이미지 상호작용과 결과 overlay 위험이 가장 높고, `ColorVision.UI.Desktop`은 데스크톱 도구와 현장 운영 진입점 위험이 가장 높습니다.

## 커버리지 표

| UI 프로젝트 | 프로젝트 파일 | 소스 README | 문서 페이지 | 릴리스 형태 | 인수인계 중점 |
| --- | --- | --- | --- | --- | --- |
| `UI/ColorVision.Common/` | `ColorVision.Common.csproj` | 있음 | [ColorVision.Common](./ColorVision.Common.md) | DLL + NuGet | MVVM 기반, 플러그인 인터페이스, 공유 계약, 상태 표시줄 메타데이터 |
| `UI/ColorVision.Themes/` | `ColorVision.Themes.csproj` | 있음 | [ColorVision.Themes](./ColorVision.Themes.md) | DLL + NuGet | 테마 리소스 사전, 창 스타일, 라이트/다크 전환 |
| `UI/ColorVision.UI/` | `ColorVision.UI.csproj` | 있음 | [ColorVision.UI](./ColorVision.UI.md) | DLL + NuGet | 설정, 메뉴, 플러그인 로딩, PropertyGrid, 단축키, 다국어 |
| `UI/ColorVision.Core/` | `ColorVision.Core.csproj` | 있음 | [ColorVision.Core](./ColorVision.Core.md) | DLL + NuGet + native runtime | `HImage`, OpenCV helper, 이미지/비디오 상호 운용, `runtimes/win-x64/native` |
| `UI/ColorVision.Database/` | `ColorVision.Database.csproj` | 있음 | [ColorVision.Database](./ColorVision.Database.md) | DLL + NuGet | SqlSugar DAO, 데이터베이스 브라우저, MySQL/SQLite 접속 |
| `UI/ColorVision.SocketProtocol/` | `ColorVision.SocketProtocol.csproj` | 있음 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) | DLL + NuGet | 로컬 TCP 서비스, JSON/Text 분배, 메시지 기록 |
| `UI/ColorVision.Scheduler/` | `ColorVision.Scheduler.csproj` | 있음 | [ColorVision.Scheduler](./ColorVision.Scheduler.md) | DLL + NuGet | Quartz 스케줄링, 작업 복구, 작업 기록, 관리 창 |
| `UI/ColorVision.ImageEditor/` | `ColorVision.ImageEditor.csproj` | 있음 | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) | DLL + NuGet + 이미지 리소스 | `ImageView`, `DrawCanvas`, 도구 모음, 결과 overlay, 3D/CIE 보기 |
| `UI/ColorVision.UI.Desktop/` | `ColorVision.UI.Desktop.csproj` | 있음 | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) | WinExe + NuGet | 설정 창, 마법사, 마켓플레이스, 다운로드 도구, DLL 버전 보기 |
| `UI/ColorVision.Solution/` | `ColorVision.Solution.csproj` | 있음 | [ColorVision.Solution](./ColorVision.Solution.md) | DLL + NuGet | 작업 공간, 편집기, 터미널, 다중 이미지 보기, 로컬 RBAC, 프로젝트 관리 |

## 릴리스 경계

| 경계 | 포함 모듈 | 먼저 읽을 문서 | 현장 위험 |
| --- | --- | --- | --- |
| 기본 공유 계층 | `ColorVision.Common`, `ColorVision.Themes`, `ColorVision.UI` | [UI DLL 컴포넌트 핸드북](./component-handbook.md), [UI DLL 릴리스 매트릭스](./release-matrix.md) | 메뉴, 설정, 플러그인 진입점, 테마 리소스 오류가 여러 상위 창에 영향을 줍니다 |
| 이미지 및 native 계층 | `ColorVision.Core`, `ColorVision.ImageEditor` | [UI DLL 릴리스 증거 및 현장 확인표](./dll-release-evidence.md), [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md) | native DLL, 이미지 리소스, overlay 등록 누락은 결과 보기에 직접 영향을 줍니다 |
| 데이터 및 서비스 창 계층 | `ColorVision.Database`, `ColorVision.SocketProtocol`, `ColorVision.Scheduler` | [UI DLL 릴리스 플레이북](./ui-dll-release-playbook.md), [UI 컴포넌트 카탈로그](./control-catalog.md) | 데이터 소스, Socket listener, 스케줄 기록, 백그라운드 작업 진단이 이 창들에 의존합니다 |
| 데스크톱 도구 및 작업 공간 계층 | `ColorVision.UI.Desktop`, `ColorVision.Solution` | [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md), 각 모듈 페이지 | 마켓플레이스, 다운로드 도구, Solution 작업 공간, RBAC, 로컬 프로젝트 관리가 여기에 집중됩니다 |

## 사용한 증거

이번 감사에서는 다음 증거를 사용했습니다.

- `Get-ChildItem UI -Directory`: 현재 실제 UI 프로젝트 디렉터리를 확인합니다.
- 각 `UI/ColorVision.*/`의 `.csproj`: target framework, `VersionPrefix`, `GeneratePackageOnBuild`, `PackageReadmeFile`, 리소스 복사 규칙을 확인합니다.
- 각 `UI/ColorVision.*/README.md`: 패키지에 포함되는 README 출처를 확인합니다.
- `docs/04-api-reference/ui-components/*.md`: 각 UI 프로젝트의 전용 문서 페이지를 확인합니다.
- `Directory.Build.props`: 공통 메타데이터, 작성자, 저장소 URL, `ColorVision.snk` 기반 조건부 strong-name signing을 확인합니다.

## 주요 위험

| 위험 | 영향 모듈 | 확인 방법 |
| --- | --- | --- |
| native runtime 누락 | `ColorVision.Core` | NuGet 패키지와 호스트 출력에 `runtimes/win-x64/native` 아래의 OpenCV/helper DLL이 포함되어 있는지 확인 |
| 결과 overlay가 보이지 않음 | `ColorVision.ImageEditor`, Engine 결과 표시 체인 | 먼저 [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md), 다음으로 Engine [결과 인수인계 체인](../engine-components/result-handoff-chain.md)을 확인 |
| 메뉴 또는 플러그인 진입점이 보이지 않음 | `ColorVision.UI`, `ColorVision.Common`, `ColorVision.UI.Desktop` | 메뉴 등록, 플러그인 discovery, 권한, 설정 로딩을 확인 |
| 데스크톱 도구 파일 누락 | `ColorVision.UI.Desktop` | `OutputType=WinExe`, WebView2, CSS, `aria2c.exe`, 리소스 복사 규칙을 확인 |
| 작업 공간 기능 이상 | `ColorVision.Solution` | 편집기, 터미널, 다중 이미지 보기, 로컬 RBAC, 프로젝트 디렉터리 권한을 확인 |
| net8/net10 혼용 | 모든 UI DLL | 호스트 target framework, 플러그인 target framework, Engine package fallback version을 확인 |

## 유지보수 규칙

UI DLL을 추가, 삭제 또는 이름 변경할 때는 반드시 다음을 함께 업데이트합니다.

1. 이 커버리지 페이지.
2. `docs/04-api-reference/ui-components/README.md`의 package map.
3. `ColorVision.Xxx.md` 같은 전용 모듈 페이지.
4. [UI DLL 컴포넌트 핸드북](./component-handbook.md).
5. control, window, Provider, extension point가 변경되면 [UI 컴포넌트 카탈로그](./control-catalog.md).
6. [UI DLL 릴리스 매트릭스](./release-matrix.md).
7. [UI DLL 릴리스 증거 및 현장 확인표](./dll-release-evidence.md).
8. runtime discovery, menu, settings, service window가 변경되면 [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md).
9. `docs/.vitepress/i18n/navigation-data.json`의 사이드바.

## 빠른 감사 명령

```powershell
Get-ChildItem UI -Directory | Sort-Object Name | Select-Object -ExpandProperty Name

Get-ChildItem docs/04-api-reference/ui-components -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

Get-ChildItem UI -Directory | Sort-Object Name | ForEach-Object {
  $csproj = Get-ChildItem $_.FullName -Filter *.csproj -File | Select-Object -First 1
  $readme = Test-Path (Join-Path $_.FullName 'README.md')
  "$($_.Name): csproj=$($csproj.Name) readme=$readme"
}
```

소스 프로젝트에 문서 페이지가 없거나 문서 페이지가 더 이상 존재하지 않는 프로젝트를 가리키면, 먼저 이 페이지와 사이드바를 수정한 뒤 번역본을 동기화합니다.
