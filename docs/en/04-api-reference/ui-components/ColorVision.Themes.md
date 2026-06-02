# ColorVision.Themes

This page only describes the theming capabilities currently implemented in UI/ColorVision.Themes, no longer continuing the old documentation's writing style of "theme development framework + custom theme platform + complete FAQ tutorial."

## Module Positioning

ColorVision.Themes is currently closer to a WPF theme resource and window appearance support library, with four main categories of core responsibilities:

- Defining the Theme enum and theme switching entry points
- Injecting resource dictionaries into Application
- Updating the UI following Windows theme changes
- Handling window title bar color and icon linkage

It is not an already abstracted "arbitrary custom theme platform." Theme.Custom, ResourceDictionaryCustom, and the complete custom theme registration flow mentioned in old documentation have no corresponding implementations in the current code.

## Most Critical Files

From the current project structure, the most worthwhile to read first are:

- ThemeManager.cs: Theme switching main entry point
- ThemeManagerExtensions.cs: Application and Window extension methods
- Theme.cs: Theme enum definitions
- XAML under Themes/: Base styles and per-theme resource dictionaries
- Controls/, Converter/, Utilities/: Controls, converters, and utility code accompanying the theme library

## Key Entry Point Types

### ThemeManager

ThemeManager is the central object of the current theme module. It is responsible for:

- Maintaining CurrentTheme and CurrentUITheme
- Handling five themes: UseSystem, Light, Dark, Pink, Cyan
- Loading the corresponding ResourceDictionary lists based on theme
- Listening for Windows theme changes
- Triggering theme change events when switching themes
- Adjusting window title bar colors

Current resource dictionaries are organized into several fixed groups:

- ResourceDictionaryBase: Shared base styles
- ResourceDictionaryDark: Dark theme resources
- ResourceDictionaryWhite: Light theme resources
- ResourceDictionaryPink: Pink theme resources
- ResourceDictionaryCyan: Cyan theme resources

This indicates that the current theme mechanism is an implementation approach of "fixed theme enum + fixed resource dictionary collections," not an open model where arbitrary new theme types can be registered at runtime.

### Theme

The current theme enum has only five values:

- UseSystem
- Light
- Dark
- Pink
- Cyan

Among these, UseSystem is not a separate set of resources, but is mapped to the light or dark theme corresponding to the current AppsTheme during ApplyTheme.

### ThemeManagerExtensions

ThemeManagerExtensions provides two actually very commonly used entry points:

- Application.ApplyTheme: Apply theme
- Application.ForceApplyTheme: Force reload theme resources

Additionally, Window.ApplyCaption will, after the window is Loaded:

- Set title bar color
- Switch window icon based on current theme
- Subscribe to theme changes and unbind on window close

So this module not only manages resource dictionaries but also handles part of the window shell appearance behavior.

## Current Runtime Main Chain

The existing theme chain is closer to the following:

1. Upper-layer UI selects a theme.
2. Application.ApplyTheme calls ThemeManager.Current.ApplyTheme.
3. If the current selection is UseSystem, it is first resolved to AppsTheme.
4. ThemeManager adds this module's resource dictionaries to Application.Resources.MergedDictionaries based on theme.
5. CurrentTheme and CurrentUITheme are updated, and change events are triggered.
6. Windows that have called ApplyCaption follow to update title bar colors and icons.

## How System Theme Following Works

ThemeManager starts a delayed initialization flow during construction. The current implementation hooks system events at a later time rather than handling synchronously at the earliest application startup phase.

It primarily listens to:

- SystemEvents.UserPreferenceChanged
- SystemParameters.StaticPropertyChanged

Then determines by reading the Personalize item in the registry:

- AppsUseLightTheme
- SystemUsesLightTheme

Therefore, "follow system" currently depends on Windows registry values and system events, not a complete theme synchronization service automatically provided by the framework layer.

## Title Bar Colors and Window Icons

ThemeManager is also responsible for calling DWM APIs to update window appearance:

- Dark theme enables immersive dark title bar
- Pink and Cyan themes directly set title bar and border colors
- Light and follow-system modes reset to system default title bar colors

Window.ApplyCaption also switches window icon resources based on the current theme. This behavior is a very practical layer of value in the current module that old documentation did not actually clarify.

## Boundaries of the Current Implementation

### Theme Persistence Is Not Done by ThemeManager Itself

Current theme configuration uses the ColorVision.Themes namespace, but the configuration class ThemeConfig is actually located in UI/ColorVision.UI/Themes.

This means:

- Theme resources and switching core are in UI/ColorVision.Themes
- Integration logic like menus, shortcuts, and configuration item editing is in UI/ColorVision.UI

Do not attribute the entire "theme configuration system" solely to the Themes project itself.

### Menu and Shortcut Entry Points Are in the UI Integration Layer

Current theme menu and shortcut entry points are mainly in:

- UI/ColorVision.UI/Themes/ThemesHotKey.cs

It is responsible for:

- Generating theme menu items
- Writing to ThemeConfig.Instance.Theme on switch
- Calling Application.ApplyTheme
- Providing Ctrl + Shift + T shortcut to cycle themes

So the Themes module itself provides the capability foundation, and what actually interfaces with the desktop menu system is the UI layer.

### Custom Theme Extension Points in Old Documentation Do Not Exist

The current code does not have these interfaces claimed to be available in old documentation:

- Theme.Custom
- ThemeManager.ResourceDictionaryCustom
- ThemeConfig.FollowSystem

Such content can no longer continue to be written as existing capabilities in the API reference.

## How to Better Read This Module Currently

### To View How Themes Switch

Read first:

- ThemeManager.cs
- ThemeManagerExtensions.cs
- Theme.cs

### To View How Themes Integrate into Application Menus and Configuration

Read first:

- UI/ColorVision.UI/Themes/ThemeConfig.cs
- UI/ColorVision.UI/Themes/ThemesHotKey.cs

### To View What Theme Resources Look Like

Read first:

- Themes/Base.xaml
- Themes/Dark.xaml
- Themes/White.xaml
- Themes/Pink.xaml
- Themes/Cyan.xaml

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Non-existent custom theme registration APIs
- Fabricated ThemeConfig configuration fields
- Tutorial-style complete theme development process
- Extensive version numbers, framework compatibility matrices, performance number promises

If theme-related content needs to be supplemented later, priority should be given to real resource dictionaries, window behavior, or UI integration points, rather than recovering into a generalized tutorial.

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
