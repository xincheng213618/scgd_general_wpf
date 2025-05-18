# 系统要求


1. Project Structure Analysis:
The repository has a complex and layered structure with multiple projects, plugins, engines, UI components, services, and utilities. It is organized primarily by feature and functionality, with clear separation of concerns.

Main directories and their roles:
1. /ColorVisionSetup: Contains setup related files, likely for installation and configuration of the ColorVision client.
2. /Plugins: Contains multiple sub-plugins like EventVWR, ScreenRecorder, WindowsServicePlugin, SystemMonitor, ColorVisonChat. Each plugin has its own project files and source code, indicating an extensible plugin architecture.
3. /docs: Documentation files including license, API docs, user manuals, and other guides.
4. /UI: Contains UI themes, controls, languages, and utilities related to the user interface. This includes themes for dark/light modes and UI components like progress rings, message boxes, upload windows.
5. /UI/ColorVision.Scheduler: Contains scheduling related UI and logic for task execution and management.
6. /UI/ColorVision.Common: Common interfaces, utilities, and helper classes used across UI projects.
7. /UI/ColorVision.ImageEditor: Image editing features with drawing tools, 3D windows, histogram charts, and TIFF handling.
8. /UI/ColorVision.Core: Core image processing and OpenCV helper classes.
9. /UI/ColorVision.UI: Main UI project with commands, plugin loaders, socket protocol, hotkey management, menus, and extensions.
10. /UI/ColorVision.Solution: Project and solution management features including editors, menus, and file handling.
11. /Engine: Core engine components including ColorVision engine, net communication, cvColorVision processing, CVImageChannelLib for video/image streaming, and MySQL ORM.
12. /Projects: Different project implementations like ProjectKB, ProjectHeyuan, CV_Spectrometer, ProjectARVR, ProjectBlackMura, ProjectShiyuan, ProjectBase. Each has its own config, UI, services, and plugin config.
13. /Scripts: Build, configure, uninstall scripts and automation.
14. /Packages: External dependencies like OpenCV, spdlog, nlohmann json, zlib, gl libraries.
15. /Core: OpenGL and CUDA related core implementations.
16. /ColorVision: Main application project containing entry point, main window, plugins management, update system, settings, wizards, floating ball UI, and projects management.

Design Patterns:
- Plugin architecture with IPlugin interface.
- MVVM pattern in UI projects (ViewModels, Views separated).
- Modular approach with separate projects for engine, UI, core, and plugins.
- Use of interfaces for extensibility and abstraction.
- Scheduler and task execution for automation.

Key Entry Points:
- ColorVision project contains App.xaml (WPF application entry) and MainWindow.xaml (main UI).
- Engine/ColorVision.Engine contains main engine logic and MQTT communication.
- Plugins loaded dynamically from Plugins folder.
- Setup handled in ColorVisionSetup.

Summary: The repository is a large-scale WPF-based Windows application for color management and photoelectric technology, structured into multiple projects for UI, engine, plugins, and projects. It uses modular and plugin-based architecture with MVVM pattern for UI and supports internationalization, theming, and hardware device integration.
