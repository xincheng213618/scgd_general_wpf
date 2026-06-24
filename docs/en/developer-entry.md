# Developer And Delivery Entry

This page keeps the English developer entry short. The maintained implementation and delivery details are in the Simplified Chinese documentation.

## Main References

| Goal | Chinese Source |
| --- | --- |
| Modify code, build packages, or deliver a version | [Developer Manual](/02-developer-guide/README) |
| Build and release scripts | [Build Scripts](/02-developer-guide/scripts/README) |
| Test and verification commands | [Testing](/02-developer-guide/testing) |
| Plugin development | [Plugin Development](/02-developer-guide/plugin-development/README) |
| Customer project packages | [Project Guide](/00-projects/README) |
| Source/module reference | [Reference](/04-api-reference/README) |

## Build Commands

```powershell
dotnet restore
dotnet build
dotnet build -c Release -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/
```

For release work, use the repository release scripts and the Chinese delivery documentation before changing packaging behavior.

## Repository

- [GitHub repository](https://github.com/xincheng213618/scgd_general_wpf)
- [Changelog](https://github.com/xincheng213618/scgd_general_wpf/blob/master/CHANGELOG.md)
