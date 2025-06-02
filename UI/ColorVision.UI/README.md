ColorVision.UI

封装的底层控件库
提供对于菜单，配置，设置，视窗，语言，主题，日志，热键，命令，工具栏，状态栏，对话框，下载，CUDA，加密等的封装，用户可以按照需求实现对映的UI，也可以直接使用封装好的UI。

1.3.2.1 更新
支持 配置Socket服务启动

    //读取配置
    ConfigHandler.GetInstance();
    //设置权限
    Authorization.Instance = ConfigService.Instance.GetRequiredService<Authorization>();
    //设置日志级别
    LogConfig.Instance.SetLog();
    //设置主题
    this.ApplyTheme(ThemeConfig.Instance.Theme);
    //设置语言
    Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
