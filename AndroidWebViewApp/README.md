# XC WebView Android App

一个最小原生 Android WebView 应用，启动后打开：

`http://xc213618.ddns.me:9998/`

## 构建方式

1. 用 Android Studio 打开 `AndroidWebViewApp` 目录。
2. 等待 Gradle 同步完成。
3. 点击 Run 安装到手机，或执行 `Build > Build Bundle(s) / APK(s) > Build APK(s)`。

因为目标地址是 `http://`，项目已经配置：

- `android.permission.INTERNET`
- `android.permission.CAMERA`，用于扫码连接和 WebView 页面的视频采集请求
- `android.hardware.camera` 标记为非必需，拒绝权限后仍可手动输入地址
- `android:usesCleartextTraffic="true"`
- `network_security_config.xml` 允许 `xc213618.ddns.me` 明文 HTTP 访问

音乐播放使用系统文件选择器授权单个音频文件，不申请存储权限或整盘媒体库权限；播放器底层使用 AndroidX Media3/ExoPlayer，优先兼容 mp3、m4a、flac 等常见音频格式。
