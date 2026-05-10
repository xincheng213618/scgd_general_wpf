# XC WebView Android App

一个最小原生 Android WebView 应用，启动后打开：

`http://xc213618.ddns.me:9998/`

## 构建方式

1. 用 Android Studio 打开 `AndroidWebViewApp` 目录。
2. 等待 Gradle 同步完成。
3. 点击 Run 安装到手机，或执行 `Build > Build Bundle(s) / APK(s) > Build APK(s)`。

因为目标地址是 `http://`，项目已经配置：

- `android.permission.INTERNET`
- `android:usesCleartextTraffic="true"`
- `network_security_config.xml` 允许 `xc213618.ddns.me` 明文 HTTP 访问

