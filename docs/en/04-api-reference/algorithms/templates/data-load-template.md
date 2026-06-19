# DataLoad Template

This page explains `Engine/ColorVision.Engine/Templates/DataLoad/` and its Flow nodes. `DataLoad` is not an image algorithm and currently has no dedicated result handler. It packages the information needed to load data by device, serial number, result type, and ZIndex.

## Scope

| Item | Current implementation |
| --- | --- |
| Template class | `TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad` |
| Parameter class | `DataLoadParam : ParamModBase` |
| Template code | `DataLoad` |
| Dictionary ID | `TemplateDicId = 22` |
| Flow nodes | `AlgDataLoadNode`, `AlgDataLoadNode2` |
| Flow operator | `operatorCode = "DataLoad"` |
| Configurator | `AlgDataLoadNodeConfigurator` |
| Result handler | No dedicated `ViewHandleDataLoad` currently |

## Source Entries

| File | Handoff purpose |
| --- | --- |
| `TemplateDataLoad.cs` | Registers the DataLoad template and dictionary ID. |
| `DataLoadParam.cs` | Defines device, result type, serial number, and ZIndex fields. |
| `FlowEngineLib/Node/Algorithm/AlgDataLoadNode.cs` | Builds `DataLoadData { TemplateParam = BuildTemp() }`. |
| `FlowEngineLib/Node/Algorithm/AlgDataLoadNode2.cs` | Builds `DataLoadData2(new DataLoadInput(...))`. |
| `FlowEngineLib/Node/Algorithm/DataLoadData*.cs` | Defines the request payload sent to the service. |
| `Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | Binds device and DataLoad template panels in the flow editor. |

## Parameter Model

| Parameter | Type | Meaning |
| --- | --- | --- |
| `DeviceCode` | `string?` | Source device code. |
| `ResultType` | `CVCommCore.CVResultType` | Result type to load. |
| `SerialNumber` | `string?` | Batch or serial number. |
| `ZIndex` | `int` | Flow/service-side data layer index. |

`AlgDataLoadNode` is template-driven: it selects a DataLoad template and sends `TemplateParam`. `AlgDataLoadNode2` is explicit-parameter-driven: it sends `DeviceCode`, `SerialNumber`, `ResultType` as a string, and `ZIndex` through `DataLoadInput`.

## Boundary

Do not describe DataLoad as a file importer. The current implementation does not select or parse files. It passes data-location parameters to the algorithm service or Flow chain, and the downstream service resolves the data.

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Flow cannot find template | `TemplateDataLoad.Params` and dictionary `TemplateDicId = 22`. |
| Wrong batch loaded | Where `SerialNumber` comes from: template, node property, or upstream Flow. |
| Wrong device | `DeviceCode/DataDeviceCode` versus the target algorithm service. |
| Wrong result type | `CVResultType.ToString()` value expected by the service. |
| Wrong data layer | `ZIndex/DataZIndex` and Flow node ordering. |

## Handoff Checklist

- State whether the flow uses `AlgDataLoadNode` or `AlgDataLoadNode2`.
- Record device code, result type, serial-number source, and ZIndex semantics.
- Update `DataLoadData`, `DataLoadData2`, and Flow docs when the service protocol changes.
- Add explicit file parameters if true file import is required later.
- Validate with a real batch and confirm the downstream node consumes the same data.

## Further Reading

- [Engine Template And Flow Chain](../../engine-components/template-flow-chain.md)
- [Flow Engine](./flow-engine.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)
