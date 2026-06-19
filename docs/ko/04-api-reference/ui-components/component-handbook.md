# UI DLL 컴포넌트 핸드북

이 페이지는 `UI/` 아래의 릴리스 단위별로 각 DLL의 책임을 설명합니다. 인수인계자는 먼저 각 컴포넌트가 무엇을 담당하는지, 누가 참조하는지, 진입점이 어디인지, 릴리스 때 무엇을 확인해야 하는지 파악해야 합니다.

구체적인 컨트롤, 창, 확장점을 찾을 때는 [UI 컴포넌트 카탈로그](./control-catalog.md)를 함께 보세요. 메뉴, 설정, ImageEditor 도구, Socket handler, Solution editor가 발견되지 않는 문제는 [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md)를 참고합니다. DLL 또는 NuGet 패키지를 릴리스할 때는 [UI DLL 릴리스 매트릭스](./release-matrix.md)를 사용합니다.

## 레이어

| 레이어 | DLL | 역할 |
| --- | --- | --- |
| 기본 계약 | `ColorVision.Common.dll` | MVVM, 공유 interface, status bar metadata, initializer, 권한, utility |
| 테마 리소스 | `ColorVision.Themes.dll` | ResourceDictionary, base window, caption, shared controls |
| UI 인프라 | `ColorVision.UI.dll` | config, menu, plugin loader, property editor, hotkey, language, log, status bar |
| native image bridge | `ColorVision.Core.dll` | `HImage`, OpenCV helper P/Invoke, WPF bitmap bridge |
| data access | `ColorVision.Database.dll` | SqlSugar DAO, MySQL/SQLite config, database browser provider |
| desktop communication | `ColorVision.SocketProtocol.dll` | local TCP server, JSON/Text dispatch, message history |
| scheduler | `ColorVision.Scheduler.dll` | Quartz scheduler, task config, history, management window |
| image editing | `ColorVision.ImageEditor.dll` | `ImageView`, draw primitives, overlay, toolbar, pseudo-color, CIE, 3D |
| desktop tools | `ColorVision.UI.Desktop.exe` / package | settings, wizard, marketplace, downloader, diagnostics |
| workspace | `ColorVision.Solution.dll` | `.cvsln`, explorer, editors, AvalonDock, terminal, local RBAC |

## 변경 위치

| 목적 | 우선 모듈 |
| --- | --- |
| ViewModel, Command, 공유 interface | `ColorVision.Common` |
| theme, shared window style | `ColorVision.Themes` |
| menu, settings, status bar, PropertyGrid | `ColorVision.UI` |
| OpenCV/native image call | `ColorVision.Core` |
| database browser source or DAO | `ColorVision.Database` |
| Socket JSON event handling | `ColorVision.SocketProtocol` |
| scheduled task | `ColorVision.Scheduler` |
| image tool, draw primitive, overlay | `ColorVision.ImageEditor` |
| settings page, wizard, marketplace, downloader | `ColorVision.UI.Desktop` |
| workspace editor, explorer, terminal, RBAC | `ColorVision.Solution` |

## 경계

- `Common`, `Themes`, `Core`는 상위 window, Engine business, customer project flow를 직접 알아서는 안 됩니다.
- `ImageEditor`는 표시, tools, primitives, overlay를 담당하며 customer CSV/MES/Socket output을 담당하지 않습니다.
- `Solution`은 workspace shell이며 device control 또는 project workflow 중심이 아닙니다.
- 새 public window, Provider, PropertyEditor, EditorTool, IEditor를 추가하면 이 페이지 또는 해당 DLL 페이지를 업데이트합니다.
