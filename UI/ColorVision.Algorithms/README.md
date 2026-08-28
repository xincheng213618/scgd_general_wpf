# ColorVision.Algorithms

`ColorVision.Algorithms` contains the provider-neutral control-plane contracts used by the ColorVision image algorithm platform. It targets .NET 8 and .NET 10 without depending on WPF, OpenCvSharp, native image handles, Flow/STN, MQTT, device algorithms, or UI dialogs.

The package provides stable algorithm identities and versions, parameter schemas and validation, serializable invocations, ROI coordinate contracts, structured result artifacts, catalog projections, provider selection, scheduling, cancellation, diagnostics, and input/output ownership rules.

Provider implementations and host adapters remain in their owning assemblies. In particular, `ColorVision.ImageEditor` supplies the standard local OpenCV/native providers and ImageView/Batch adapters, while `ColorVision.Engine` owns Flow integration.

## Package dependency

`ColorVision.ImageEditor` has a normal project/package dependency on this package. Release automation must generate and publish `ColorVision.Algorithms` before `ColorVision.ImageEditor`; consumers should use compatible versions of both packages.
