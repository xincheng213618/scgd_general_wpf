# Engine Service Development Handoff

This page documents the real service model under `Engine/ColorVision.Engine/Services/`. In this repository, an Engine service is a runtime-visible device or business service with configuration, UI, and often MQTT commands. It is not a generic dependency-injection service.

Read [Engine Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md) first, then use this page to locate the development points.

## Runtime Chain

| Stage | Key Object | Purpose |
| --- | --- | --- |
| Service type | `ServiceTypes` | Type ids for Camera, PG, Spectrum, SMU, Sensor, FileServer, Algorithm, FilterWheel, Calibration, Motor, Flow, ThirdPartyAlgorithms, and others |
| Configuration | `SysResourceModel.Value` | Device configuration JSON, restored by `DeviceService<T>` into a concrete `Config*` type |
| Creation | `DeviceServiceFactoryRegistry` | Creates the concrete `Device*` from `SysResourceModel.Type` |
| Runtime list | `ServiceManager.GetInstance().DeviceServices` | Loaded device services in the host |
| UI entry | `GetDeviceInfo()` and `GetDisplayControl()` | Property/info panels, display controls, and device tree actions |
| Command chain | `GetMQTTService()` and `MQTTDeviceService<T>` | Device command send, response matching, timeout, and message record |

Typical loading flow:

1. `SysResourceModel` is stored in the database or configuration.
2. `ServiceManager.Load()` enumerates resources.
3. `DeviceServiceFactoryRegistry.CreateService(sysResourceModel)` creates a concrete service.
4. `DeviceService<T>` restores `Config`, `Code`, and `Name` from `SysResourceModel.Value`.
5. The concrete `Device*` creates its matching `MQTT*` service and info/display controls.
6. Device tree, flow nodes, projects, and templates select services from `ServiceManager.DeviceServices`.

## Default Registrations

| Type | Folder | Device Class | MQTT Class | Responsibility |
| --- | --- | --- | --- | --- |
| Camera | `Services/Devices/Camera/` | `DeviceCamera` | `MQTTCamera` | Camera, live frames, capture, exposure, calibration commands |
| PG | `Services/Devices/PG/` | `DevicePG` | `MQTTPG` | Pattern generator switching and project image sequence control |
| Spectrum | `Services/Devices/Spectrum/` | `DeviceSpectrum` | `MQTTSpectrum` | Spectrometer connection, dark storage, measurement, spectrum data |
| SMU | `Services/Devices/SMU/` | `DeviceSMU` | `MQTTSMU` | Source meter, scans, result reads, spectrum linkage |
| Sensor | `Services/Devices/Sensor/` | `DeviceSensor` | `MQTTSensor` | Serial/network sensor commands and command templates |
| FileServer | `Services/Devices/FileServer/` | `DeviceFileServer` | `MQTTFileServer` | File paths, download, cache, file service commands |
| Algorithm | `Services/Devices/Algorithm/` | `DeviceAlgorithm` | `MQTTAlgorithm` | Algorithm service calls, result queries, algorithm view |
| FilterWheel | `Services/Devices/CfwPort/` | `DeviceCfwPort` | `MQTTCfwPort` | Filter wheel port and position control |
| Calibration | `Services/Devices/Calibration/` | `DeviceCalibration` | `MQTTCalibration` | Calibration commands, files, and results |
| Motor | `Services/Devices/Motor/` | `DeviceMotor` | `MQTTMotor` | Home, move, position read, diaphragm control |
| Flow | `Services/Devices/FlowDevice/` | `DeviceFlowDevice` | `MQTTFlowDevice` | Flow device service |
| ThirdPartyAlgorithms | `Services/Devices/ThirdPartyAlgorithms/` | `DeviceThirdPartyAlgorithms` | `MQTTThirdPartyAlgorithms` | Third-party algorithm integration |

## Add a Device Service

1. Decide whether an existing `ServiceTypes` value can be reused.
2. Add `Config* : DeviceServiceConfig`; keep old JSON deserialization compatible.
3. Add `Device* : DeviceService<Config*>`; create `DService = new MQTT*(Config)` in the constructor.
4. Override `GetDeviceInfo()`, and add `GetDisplayControl()` / `GetMQTTService()` when needed.
5. Add `MQTT* : MQTTDeviceService<Config*>`; wrap device commands only, not customer judgement.
6. Register the factory in `DeviceServiceFactoryRegistry.RegisterDefaults()`.
7. If terminal creation is needed, verify `DeviceServiceCreateContext` writes `Code`, `Name`, `SendTopic`, and `SubscribeTopic`.
8. Update the user guide, device service chain, module map, and acceptance notes.

## Change Boundaries

| Change | Edit Here | Avoid Editing |
| --- | --- | --- |
| New device type | `ServiceTypes`, `DeviceServiceFactoryRegistry`, `Services/Devices/<Name>/` | Project `Process` code |
| New command | Matching `MQTT*` method and command parameters | Global `MQTTControl` |
| New config field | `Config*` and device property UI | `SysResourceModel` schema unless migration is required |
| New device UI | `Info*`, `Display*`, view config | Algorithm result handlers |
| Customer judgement | Project `Process`, Recipe, Fix, exporters | Device service layer |

## Acceptance Checklist

| Item | Validation |
| --- | --- |
| Registration | Host starts and the device tree restores the service |
| Configuration | Export, import, reset, save, and restart preserve fields |
| MQTT | `SendTopic` / `SubscribeTopic` are correct and responses match `MsgID` |
| UI | Property, info, and display controls open without exceptions |
| Flow/project | Dependent flow nodes or project packages can select the service |
| Logging | Timeout, failed response, and exception paths produce records or logs |

## Related Documents

- [Engine Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md)
- [MQTT Message Processing](./mqtt.md)
- [Engine Runtime Object Map](../../04-api-reference/engine-components/runtime-object-map.md)
- [Testing and Validation Handoff](../testing.md)
