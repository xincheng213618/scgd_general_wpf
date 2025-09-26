# ColorVision.Solution Extensibility Guide

This document describes the enhanced extensibility patterns in the ColorVision.Solution project after the structural optimizations.

## Overview

The ColorVision.Solution project has been optimized with several key improvements:

1. **VObject Abstract Optimization** - Common functionality extracted to base classes
2. **EditorManager Configuration Persistence** - Automatic saving of user preferences  
3. **Meta Registration Mechanism Enhancement** - Attribute-based registration system
4. **Factory Pattern Implementation** - Consistent object creation
5. **Exception Handling and UX Improvements** - Better error handling and user feedback
6. **Resource Management** - Proper IDisposable implementation

## Extending File Support

### Creating a Custom File Meta

To add support for a new file type, create a class that inherits from `FileMetaBase` and use the `FileMetaForExtensionAttribute`:

```csharp
using ColorVision.Solution.FileMeta;
using ColorVision.UI.Menus;
using System.IO;

[FileMetaForExtension(".cs|.vb|.fs", name: "Code File", isDefault: true)]
public class CodeFileMeta : FileMetaBase
{
    public CodeFileMeta() { }

    public CodeFileMeta(FileInfo fileInfo)
    {
        FileInfo = fileInfo;
        Name = FileInfo.Name;
        Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
    }

    public override IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        var menuItems = base.GetMenuItems().ToList();
        
        // Add custom menu items
        menuItems.Add(new MenuItemMetadata 
        { 
            GuidId = "CompileFile", 
            Order = 1, 
            Header = "编译文件", 
            Icon = MenuItemIcon.TryFindResource("DICompile") 
        });
        
        return menuItems;
    }
}
```

### Generic File Meta

For fallback handling of unknown file types:

```csharp
[GenericFileMeta(name: "Generic File")]
public class MyGenericFile : FileMetaBase
{
    // Implementation
}
```

## Extending Folder Support

### Creating a Custom Folder Meta

To add special handling for specific folder types:

```csharp
using ColorVision.Solution.FolderMeta;
using ColorVision.UI.Menus;
using System.IO;

[FolderMetaForPattern("bin|obj|debug|release", name: "Build Output Folder")]
public class BuildOutputFolder : FolderMetaBase
{
    public BuildOutputFolder() { }

    public BuildOutputFolder(DirectoryInfo directoryInfo)
    {
        DirectoryInfo = directoryInfo;
        Icon = FileIcon.GetDirectoryIconImageSource();
    }

    public override IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        var menuItems = base.GetMenuItems().ToList();
        
        menuItems.Add(new MenuItemMetadata 
        { 
            GuidId = "CleanBuildOutput", 
            Order = 1, 
            Header = "清理生成输出", 
            Icon = MenuItemIcon.TryFindResource("DIClean") 
        });
        
        return menuItems;
    }
}
```

### Generic Folder Meta

For fallback handling of all directories:

```csharp
[GenericFolderMeta(name: "Generic Folder")]  
public class MyGenericFolder : FolderMetaBase
{
    // Implementation
}
```

## Extending Editor Support

### Creating a Custom Editor

To add a new editor for specific file types:

```csharp
using ColorVision.Solution.Editor;

[EditorForExtension(".md|.txt", name: "Text Editor", isDefault: true)]
public class TextEditor : EditorBase
{
    public override void Open(string filePath)
    {
        // Custom editor logic
    }
}
```

### Generic Editor

For handling any file type:

```csharp
[GenericEditor(name: "Universal Editor")]
public class UniversalEditor : EditorBase
{
    // Implementation
}
```

## Using the Factory Pattern

### Creating VObjects

Use `VObjectFactory` for consistent object creation:

```csharp
// Create VFolder with automatic meta detection
var directoryInfo = new DirectoryInfo(@"C:\MyProject");
VFolder vFolder = VObjectFactory.CreateVFolder(directoryInfo);

// Create VFile with automatic meta detection
var fileInfo = new FileInfo(@"C:\MyProject\Program.cs");
VFile vFile = VObjectFactory.CreateVFile(fileInfo);
```

### Registry Initialization

Initialize registries during application startup:

```csharp
// In your application startup code
VObjectFactory.InitializeRegistries();

// Optional: Inspect available types
var folderTypes = VObjectFactory.GetAvailableFolderMetaTypes();
var fileTypes = VObjectFactory.GetAvailableFileMetaTypes();
```

## Error Handling and Logging

### Custom VObject with Enhanced Error Handling

When extending VObject, use the built-in logging and error handling:

```csharp
public class MyCustomVObject : VObject
{
    public override bool ReName(string name)
    {
        try
        {
            LogOperation($"Renaming object to: {name}");
            
            // Your rename logic here
            
            LogOperation("Rename completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Rename failed: {ex.Message}", ex);
            ShowUserError("重命名失败，请检查权限或文件是否被占用");
            return false;
        }
    }
}
```

## Resource Management

### Implementing IDisposable

When creating custom VObject classes that use resources:

```csharp
public class MyResourceVObject : VObject, IDisposable
{
    private SomeResource _resource;
    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _resource?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MyResourceVObject()
    {
        Dispose(false);
    }
}
```

## Configuration Persistence

### Editor Configuration

Editor preferences are automatically persisted:

```csharp
// Setting a default editor automatically saves to configuration
EditorManager.Instance.SetDefaultEditor(".cs", typeof(MyCustomEditor));

// Configuration is automatically loaded on startup
var defaultEditor = EditorManager.Instance.GetDefaultEditorType(".cs");
```

## Best Practices

1. **Use Attributes for Registration** - Leverage the attribute system for automatic registration
2. **Implement Initialize() Pattern** - Move heavy initialization out of constructors
3. **Handle Exceptions Gracefully** - Use the built-in logging and user feedback methods
4. **Dispose Resources Properly** - Implement IDisposable when using resources
5. **Use Factory Pattern** - Use VObjectFactory for consistent object creation
6. **Document Your Extensions** - Add XML documentation to public APIs

## Migration from Old System

### Old Pattern
```csharp
// Old way - manual registration and construction
FileMetaRegistry.RegisterFileMetasFromAssemblies();
var fileMeta = new CommonFile(fileInfo);
var vFile = new VFile(fileMeta);
```

### New Pattern
```csharp
// New way - automatic registration and factory creation
VObjectFactory.InitializeRegistries(); // Once at startup
var vFile = VObjectFactory.CreateVFile(fileInfo); // Automatic meta selection
```

This enhanced system provides better extensibility, maintainability, and user experience while maintaining backward compatibility where possible.