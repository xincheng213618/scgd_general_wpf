# 템플릿 메뉴 진입점

`Templates/Menus/` 는 알고리즘 실행 모듈이 아니라 템플릿 기능을 메인 메뉴에 배치하는 골격입니다. 어떤 그룹에 보이고 클릭 시 어떤 `TemplateEditorWindow` 를 열지 결정합니다.

## 빠른 정보

| 항목 | 클래스 |
| --- | --- |
| 최상위 메뉴 | `MenuTemplate` |
| 알고리즘 템플릿 그룹 | `MenuITemplateAlgorithm` |
| 일반 템플릿 메뉴 기반 | `MenuItemTemplateBase` |
| 알고리즘 템플릿 메뉴 기반 | `MenuITemplateAlgorithmBase` |
| 기본 동작 | `new TemplateEditorWindow(Template).Show()` |

## 계층

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase 파생 메뉴
```

`MenuItemTemplateBase` 는 기본적으로 `MenuTemplate` 아래에 붙고, `Execute()` 에서 `ShowTemplateWindow()` 를 호출합니다. 구체 메뉴는 `Template` 속성에서 열 템플릿을 반환합니다.

## 인수인계 주의점

- `Menus/` 는 진입점만 구성하며 알고리즘을 실행하지 않습니다.
- `OwnerGuid` 를 바꾸면 메뉴 위치가 바뀌거나 템플릿 진입점이 보이지 않을 수 있습니다.
- 기본 창은 비모달 `Show()` 입니다. 특수 흐름이 필요할 때만 `ShowTemplateWindow()` 를 재정의합니다.
- 저장, 가져오기, 내보내기는 구체 `ITemplate` 구현의 책임입니다.
- 검색 시 `MenuDefalutDicAlg` 처럼 현재 클래스 이름을 사용합니다.

## 관련 페이지

- [템플릿 관리](./template-management.md)
- [Templates API 참조](./api-reference.md)
- [플러그인 개발 가이드](../../../02-developer-guide/plugin-development/README.md)
- [확장 포인트](../../extensions/README.md)
