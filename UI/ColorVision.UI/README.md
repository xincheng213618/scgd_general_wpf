ColorVision.UI

封装的底层控件库
提供对于菜单，配置，设置，视窗，语言，主题，日志，热键，命令，工具栏，状态栏，对话框，下载，CUDA，加密等的封装，用户可以按照需求实现对映的UI，也可以直接使用封装好的UI。

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

设置窗口的实现移动到框架中来实现

    //设置窗口可拖动
    this.MouseLeftButtonDown += (s, e) =>
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    };

   属性编辑窗口 PropertyGrid
   提供对于对象属性的编辑功能，支持属性分类，属性排序，属性过滤，属性编辑器自定义等功能。
    