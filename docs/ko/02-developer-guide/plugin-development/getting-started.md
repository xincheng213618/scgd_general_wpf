# 플러그인 개발 시작하기

이 페이지에서는 더 이상 이전 범용 호스트, 비동기 수명 주기 및 `plugin.json` 예제를 사용하지 않는 현재 웨어하우스에서 실행 가능한 가장 짧은 플러그인 개발 경로를 제공합니다.

## 먼저 준비할 것

- 윈도우 개발 환경
- .NET 8.0 SDK
- WPF 개발 도구 체인
- 현재 웨어하우스 소스코드와 메인 프로그램을 실행하고 출력할 수 있습니다.

## 최소 개발 경로

### 1. 새로운 플러그인 프로젝트 생성

`Plugins/<PluginId>/` 아래에 직접 플러그인 프로젝트를 빌드하고 대상 프레임워크를 `net8.0-windows`로 유지하는 것이 좋습니다. 플러그인에 인터페이스가 있으면 WPF를 활성화하세요.

```xml
<프로젝트 Sdk="Microsoft.NET.Sdk">
  <속성 그룹>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>참</UseWPF>
    <OutputType>라이브러리</OutputType>
  </PropertyGroup>

  <항목 그룹>
    <ProjectReference include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />

항목 유형을 명시적으로 지정해야 하는 경우 'entry_point'를 계속해서 채울 수 있습니다.

## 4. 제품을 기본 프로그램 플러그인 디렉터리에 복사합니다.

메인 프로그램이 실행 중일 때 자체 출력 디렉토리에서 `Plugins/`를 검색하므로 디버깅할 때 플러그인 제품을 여기에 복사해야 합니다.

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</대상>
```
로컬 출력 디렉터리가 다른 경우 실제 기본 프로그램 출력 경로에 따라 조정해야 합니다.

## 5. 실행 및 디버그

1. 메인 프로그램을 빌드합니다.
2. 플러그인 프로젝트를 빌드하고 DLL 및 `manifest.json`이 플러그인 디렉터리에 복사되었는지 확인합니다.
3. `ColorVision/ColorVision.csproj`를 시작합니다.
4. 해당 메뉴, 도구 페이지 또는 플러그인 관리 인터페이스에 플러그인이 로드되었는지 확인합니다.

## 권장 참조 구현

- `플러그인/EventVWR/EventVWRPlugins.cs`
- `플러그인/EventVWR/Dump/MenuDump.cs`
- `플러그인/SystemMonitor/SystemMonitorControl.xaml.cs`
-`플러그인/README.md`

이 예에서는 기본 플러그인 항목과 메뉴 확장이라는 두 가지 일반적인 패턴을 다루었습니다.

## FAQ

### 플러그인을 찾을 수 없습니다

- `manifest.json`이 존재하는지 확인하세요.
- `dllpath`가 가리키는 DLL이 실제로 존재하는지 확인
- 플러그인 디렉터리가 기본 프로그램 출력 디렉터리 아래 `Plugins/<PluginId>/`에 복사되었는지 확인합니다.

### 플러그인을 찾았으나 기능이 나타나지 않습니다

- 기본 Plug-in 클래스만 구현되어 있고 필수 Provider 인터페이스는 구현되어 있지 않은지 확인
- 항목 유형에 공개 인수 없는 구조가 있는지 확인하십시오.
- 타입이 추상적이지 않고, 제네릭이 아닌 개방형인지 확인하세요.

### 종속성 충돌

- 플랫폼과 함께 제공되는 `ColorVision.*.dll`을 다시 패키지하지 마십시오.
- 플러그인에 '.deps.json'이 포함된 경우 종속 버전이 대상 플랫폼보다 높지 않은지 확인하세요.

## 다음 단계

- 플랫폼이 플러그인을 검색하고 로드하는 방법을 이해하려면 [플러그인 수명 주기](./lifecycle.md)를 참조하세요.
- 전체적인 구조를 먼저 이해하고 싶다면 [플러그인 개발 개요](./overview.md)를 참고하세요.
