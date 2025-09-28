# API 参考


1. 项目结构分析：

该仓库是一个Windows WPF应用程序，主要提供色彩管理和光电技术解决方案。项目结构较为庞大，包含多个子项目和模块，按功能和技术分层组织。

主目录下有多个文件和文件夹，主要目录及其说明如下：

1.1 /Engine 目录
- 这是项目的核心引擎部分，负责色彩处理、流程引擎、算法实现、设备控制等。
- 包含多个子模块，如cvColorVision（色彩视觉核心）、ColorVision.Net、ColorVision.Engine（流程引擎及服务）、CVImageChannelLib（视频图像处理库）、MySql（数据库交互）、Rbac（权限管理）、Services（服务管理）、Templates（模板管理）、Messages（消息传递）、Utilities（工具类）等。
- 该目录体现了项目的业务逻辑和核心功能实现。

1.2 /UI 目录
- 包含用户界面相关代码，如主题管理、控件、MVVM架构支持、扩展、热键、菜单管理、搜索、排序等。
- 体现了界面层的设计和实现，支持多语言、多主题。

1.3 /Plugins 目录
- 插件相关代码，用户可实现IPlugin接口开发插件，程序启动时自动加载。
- 包含多个插件子项目，如EventVWR、ScreenRecorder、SystemMonitor、WindowsServicePlugin、ColorVisonChat等。
- 体现了系统的扩展性设计。

1.4 /Projects 目录
- 具体项目实现，如ProjectKB、ProjectHeyuan、ProjectBlackMura、ProjectShiyuan、CV_Spectrometer、ProjectARVR等。
- 每个项目包含其配置、窗口、服务等，实现业务场景。

1.5 /ColorVision 目录
- 主程序入口和核心窗口管理，如MainWindow、App.xaml、插件管理、更新管理、项目管理等。
- 负责程序启动、主界面和整体协调。

1.6 /ColorVisionSetup 目录
- 安装及更新相关代码，自动更新、视频播放器等。

1.7 /Advanced、/Core、/Scripts、/Test 等目录
- Advanced包含高级资源文件。
- Core包含OpenCV相关的底层支持。
- Scripts包含构建、配置、修改等脚本。
- Test包含测试代码。

1.8 /docs 目录
- 文档资源，如API文档、用户手册、许可证等。

整体架构：
- 采用分层架构，底层为核心引擎和算法，中间为服务和设备管理，上层为UI界面和插件扩展。
- 支持多项目、多插件、多语言、多主题。
- 设计模式包括MVVM（UI层）、插件接口（IPlugin）、服务接口、模板接口等。

关键入口：
- ColorVision目录下的MainWindow.xaml.cs为主窗口入口。
- Engine目录下的ColorVision.Engine承担流程引擎和服务管理。
- Plugins目录实现插件扩展，动态加载。

总结：
项目结构清晰，按职责划分模块，便于维护和扩展。底层算法和设备管理与上层UI解耦，支持多项目和插件。适合复杂的色彩视觉和光电应用场景。
