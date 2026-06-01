# ColorVision.FileIO

This page only describes the `ColorVision.FileIO` module actually available in the current repository, no longer maintaining the old "generic file framework + JSON/YAML/batch processing platform" draft.

## What This Module Is Now

Based on current source code status, `ColorVision.FileIO` is not a generalized file processing framework, but a dedicated I/O module organized around ColorVision's custom image file format. The clearest landing points are:

- Identifying and reading `CVCIE` / `CVRAW` / `CVSRC` type files.
- Parsing file headers, versions, exposure, dimensions, channels, and raw data regions.
- Opening local ColorVision image files by channel or file type.
- Using `CVCIEFile` as the most core data carrier.

Therefore, it is much narrower than the old documentation described as "supporting generic JSON/XML/YAML/image batch processing," and much closer to the current actual code.

## Most Critical Files

- `Engine/ColorVision.FileIO/CVFileUtil.cs`
- `Engine/ColorVision.FileIO/CVCIEFile.cs`

At least from the readable implementations in the current module, these two already cover the most core format identification, header parsing, and data carrier definition.

## What Formats Are Actually Processed

The `CVType` enum currently defined in `CVCIEFile.cs` includes:

- `Raw`
- `Src`
- `CIE`
- `Calibration`
- `Tif`
- `Dat`

But from the current densest implementation in `CVFileUtil`, the focus is really on the `CVCIE` / `CVRAW` group of ColorVision custom image files, not generic office or configuration file formats.

## How the Current Read Chain Works

The core flow of `CVFileUtil` is roughly:

1. Use file headers to determine if it is a `CVCIE` family file.
2. Read header and version.
3. Parse metadata such as filename, gain, channel count, exposure, dimensions by different versions.
4. Read raw byte blocks by data region offset.
5. Converge this information into `CVCIEFile`.

It currently supports both:

- Reading from file path
- Reading from byte array

And large amounts of detail revolve around "preventing out-of-bounds, exceptions, OOM, invalid headers" — these are typical format parsing code, not generic file service layer code.

## What Role `CVCIEFile` Plays

`CVCIEFile` is now the most core data structure in the module, carrying:

- File version
- File type
- Rows, columns, and bit depth
- Channel count
- Gain and exposure arrays
- Source filename
- Raw data bytes
- File path

It also implements `IDisposable` itself, actively clearing large data arrays upon disposal. This again indicates that the current module primarily deals with large-volume image data, not lightweight text configuration.

## What Practical Entry Points Are Available

From the current implementation of `CVFileUtil`, the key entry points include:

- `IsCIEFile(...)`
- `IsCVCIEFile(...)`
- `ReadCIEFileHeader(...)`
- `ReadCIEFileData(...)`
- `Read(...)`
- `OpenLocalFileChannel(...)`
- `OpenLocalCVFile(...)`

These entry points basically revolve around two goals:

- Determining whether a file is a ColorVision proprietary format
- Safely parsing proprietary formats into in-memory objects

## Most Common Mistakes to Avoid

### It Is Not a Generalized File System Middleware

The current source code does not implement the complete system of generic `FileIOManager`, JSON processor, YAML processor, batch executor, etc. from old documentation. Continuing to use that writing style would write non-existent abstraction layers as fact.

### The Core Object Is a Binary Image Carrier, Not a Text Configuration Model

`CVCIEFile` currently primarily carries image metadata and large raw byte arrays. This is an entirely different focus from the old draft's emphasis on configuration, compression, and serialization services.

### Version Branching Is the Implementation Focus

`ReadCIEFileHeader(...)`'s branching parsing for different versions is one of the current real complexity sources. If documentation only writes "reads a custom format file," it would erase this layer of detail.

### Safety Is Primarily Reflected in Defensive Reading

The current code extensively checks file length, offsets, array allocation, and exceptions, rather than implementing a so-called "hot-reload async file framework." Understanding this is key to seeing why the code focus is on header/data parsing.

## Recommended Reading Order

1. `Engine/ColorVision.FileIO/CVCIEFile.cs`
2. `Engine/ColorVision.FileIO/CVFileUtil.cs`

Looking at the data carrier first, then the specific parsing logic, is far more effective than starting with old documentation.

## Continue Reading

- [docs/04-api-reference/engine-components/cvColorVision.md](./cvColorVision.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)