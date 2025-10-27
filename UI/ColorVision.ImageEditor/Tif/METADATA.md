# Image Metadata Extraction

## Overview
The `Opentif` and `CommonImageOpen` classes now automatically extract and store metadata when opening images. This metadata is stored in the `EditorContext.Config.Properties` dictionary and can be accessed using `Config.GetProperties<T>(key)`.

## Metadata Fields

### File Metadata (Available for all images)
- **FileSource** (string): Full path to the source file
- **FileName** (string): Name of the file with extension
- **FileSize** (long): Size of the file in bytes
- **FileCreationTime** (DateTime): When the file was created
- **FileModifiedTime** (DateTime): When the file was last modified
- **ImageWidth** (int): Width of the image in pixels
- **ImageHeight** (int): Height of the image in pixels

### EXIF Metadata (Available when present in image)
- **CameraModel** (string): Camera model that captured the image
- **CameraManufacturer** (string): Camera manufacturer
- **DateTaken** (string): Date and time when the photo was taken
- **ApplicationName** (string): Application used to create/edit the image
- **ImageTitle** (string): Title of the image
- **ImageSubject** (string): Subject or description of the image

## Supported File Formats

### Opentif
- `.tif`, `.tiff` (TIFF images with EXIF support)

### CommonImageOpen
- `.bmp` (Bitmap images)
- `.jpg`, `.jpeg` (JPEG images with EXIF support)
- `.png` (PNG images with metadata support)
- `.webp`, `.ico`, `.gif` (Basic file metadata only)

## Usage Example

```csharp
// After opening an image, access metadata like this:
string filePath = context.Config.GetProperties<string>("FileSource");
long fileSize = context.Config.GetProperties<long>("FileSize");
DateTime? dateTaken = context.Config.GetProperties<DateTime>("DateTaken");
string cameraModel = context.Config.GetProperties<string>("CameraModel");
int width = context.Config.GetProperties<int>("ImageWidth");
int height = context.Config.GetProperties<int>("ImageHeight");

// Or use the existing GetPropertyString() method to get all properties as a formatted string:
string allMetadata = context.Config.GetPropertyString();
```

## Implementation Details

### Opentif
Uses `TiffBitmapDecoder` to extract both image data and metadata from TIFF files. EXIF metadata is extracted from the first frame of the TIFF file.

### CommonImageOpen
Uses format-specific decoders (`JpegBitmapDecoder`, `PngBitmapDecoder`, `BmpBitmapDecoder`) to extract metadata when available. Falls back to standard `BitmapImage` loading if decoder fails.

## Error Handling
All metadata extraction is wrapped in try-catch blocks to ensure that:
1. Missing or corrupted metadata doesn't prevent image loading
2. Images without metadata can still be opened successfully
3. Partial metadata is preserved even if some fields fail to extract
