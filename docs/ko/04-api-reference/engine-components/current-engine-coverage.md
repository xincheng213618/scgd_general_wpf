# 현재 Engine 문서 커버리지

이 페이지는 `Engine/` business logic 에 인수인계 입구가 있는지 확인합니다. file-by-file API list 가 아니라 실제 Engine project 와 `ColorVision.Engine` key directory 를 handoff page 에 매핑합니다.

## 커버리지

| Engine project | project file | README | docs page | handoff entry | result |
| --- | --- | --- | --- | --- | --- |
| `Engine/ColorVision.Engine/` | `ColorVision.Engine.csproj` | present | [ColorVision.Engine](./ColorVision.Engine.md) | [business matrix](./business-flow-matrix.md), [scenario playbook](./business-scenario-playbook.md), [runtime object map](./runtime-object-map.md) | main runtime covered |
| `Engine/FlowEngineLib/` | `FlowEngineLib.csproj` | present | [FlowEngineLib](./FlowEngineLib.md) | [templates and Flow chain](./template-flow-chain.md) | Flow execution covered |
| `Engine/cvColorVision/` | `cvColorVision.csproj` | present | [cvColorVision](./cvColorVision.md) | [result handoff chain](./result-handoff-chain.md) | native/vision boundary documented |
| `Engine/ColorVision.FileIO/` | `ColorVision.FileIO.csproj` | present | [ColorVision.FileIO](./ColorVision.FileIO.md) | [data export/import](../../01-user-guide/data-management/export-import.md), result chain | file I/O covered |
| `Engine/ST.Library.UI/` | `ST.Library.UI.csproj` | present | [ST.Library.UI](./ST.Library.UI.md) | [templates and Flow chain](./template-flow-chain.md) | node-editor UI base covered |
| `Engine/ColorVision.ShellExtension/` | `ColorVision.ShellExtension.csproj` | missing | [ColorVision.ShellExtension](./ColorVision.ShellExtension.md) | shell thumbnail extension page, [ColorVision.FileIO](./ColorVision.FileIO.md) | external Explorer integration covered |

## `ColorVision.Engine` business directory coverage

| source directory | meaning | current handoff page | first question |
| --- | --- | --- | --- |
| `Services/` | service management, device base, terminal, cache, RC service | [device service chain](./device-service-chain.md), [business matrix](./business-flow-matrix.md) | Can the resource create the correct `DeviceService`? |
| `Services/Devices/` | Camera, Motor, SMU, FileServer, FlowDevice and devices | [device service chain](./device-service-chain.md) | Do manual action and Flow node reference the same device? |
| `Templates/` | template parameters, Flow template, algorithm template, POI/ROI, ARVR | [templates and Flow chain](./template-flow-chain.md), [result handoff chain](./result-handoff-chain.md) | Are template version, node binding, and result mapping aligned? |
| `FlowEngineLib/Node/Algorithm/`, `FlowEngineLib/Algorithm/` | Flow algorithm, conversion, calibration nodes | [Flow 변환 및 보정 노드](./flow-conversion-calibration-nodes.md), [templates and Flow chain](./template-flow-chain.md) | Do `operatorCode`, parameter object, and configurator match? |
| `MQTT/` | MQTT config, connection, control objects | [device service chain](./device-service-chain.md), [scenario playbook](./business-scenario-playbook.md) | Do topic, connection state, and device Code match? |
| `Batch/`, `Dao/`, `Mysql/` | batch, result record, MySQL/SQLite access | [result handoff chain](./result-handoff-chain.md) | Was data written and does batch/SN match? |
| `Messages/` | MQTT and business messages | [business matrix](./business-flow-matrix.md) | Which message model is used? |
| `Archive/`, `Reports/` | archive lookup and report generation | [result handoff chain](./result-handoff-chain.md) | Do source, fields, path, and report version match? |
| `ToolPlugins/` | built-in tools such as ImageJ and CVRaw-to-CSV | [scenario playbook](./business-scenario-playbook.md), [ColorVision.Engine](./ColorVision.Engine.md) | Debug helper or production deliverable? |
| `Abstractions/`, `PropertyEditor/`, `Utilities/` | shared interface, property editing, utility | [runtime object map](./runtime-object-map.md) | Is it called by a business chain? |
| `Assets/`, `Properties/`, `CalFile/`, `Media/` | resources, property, calibration/media files | scenario pages | Must it be copied with package? |
| `bin/`, `obj/` | build output and intermediates | not documentation objects | Do not use as business evidence |

## 인수인계 읽기 순서

1. 소유 경계가 불명확하면 [Engine Business Flow Matrix](./business-flow-matrix.md) 부터 봅니다.
2. 알려진 시나리오라면 [Engine Business Scenario Playbook](./business-scenario-playbook.md) 을 봅니다.
3. class/runtime object 를 알고 있으면 [Engine Runtime Object Map](./runtime-object-map.md) 을 봅니다.
4. chain 을 알고 있으면 [Device Service Chain](./device-service-chain.md), [Templates And Flow Chain](./template-flow-chain.md), [Result Display And Project Handoff](./result-handoff-chain.md) 를 봅니다.
5. customer output 은 [Project Capability & Handoff Matrix](../projects/project-capability-matrix.md) 와 project page 를 봅니다.

Engine project, device type, template directory, result chain 을 추가하면 이 페이지와 관련 chain page 를 업데이트합니다.
