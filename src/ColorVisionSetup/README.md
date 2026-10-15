# ColorVision 在线下载

独立 WPF 下载工具，目标为 .NET Framework 4.8 / x64。打开即查询最新版，用户点击后下载安装包，校验成功后可打开现有完整安装器。

运行需要 Windows x64 和 .NET Framework 4.8 或 4.8.1；不需要 .NET 10。交付文件只有 `ColorVisionSetup.exe`，没有额外 DLL、配置或媒体文件。单文件不包含 Framework 运行时，也不包含 ColorVision 完整安装包。

运行、签名信任、缓存和验证边界见[桌面交付制品与责任路由](../../docs/02-developer-guide/deployment/overview.md#独立在线下载工具)。本地构建入口为 `Scripts/build_setup.ps1`，仅构建下载工具，不签名、上传或启动安装。
