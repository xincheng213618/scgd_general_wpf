#pragma warning disable CA1822,CA1863,CS8602
using ColorVision.UI;
using ColorVision.UI.Desktop;
using log4net;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    /// <summary>
    /// FloatingBallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingBallWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FloatingBallWindow));
        private readonly Queue<DesktopPetNotification> _notifications = new();
        private readonly DispatcherTimer _motionTimer = new();
        private readonly DispatcherTimer _messageTimer = new();
        private readonly DispatcherTimer _idleTipTimer = new();
        private readonly DispatcherTimer _blinkTimer = new();
        private readonly Random _random = new();
        private DateTime _motionStartedAt = DateTime.Now;
        private bool _isShowingMessage;
        private bool _isClosingFromConfig;
        private bool _webViewBridgeScriptInjected;

        public static FloatingBallWindowConfig WindowConfig => ConfigService.Instance.GetRequiredService<FloatingBallWindowConfig>();
        public static DesktopPetConfig PetConfig => DesktopPetConfig.Instance;

        private const int WmNclbuttondown = 0x00A1;
        private const int Htcaption = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public FloatingBallWindow()
        {
            InitializeComponent();
            DataContext = PetConfig;
            PlaceDefaultPositionIfNeeded();
            WindowConfig.SetWindow(this);
            ConfigureTimers();
            PetConfig.PropertyChanged += PetConfig_PropertyChanged;
        }

        public void CloseFromConfig()
        {
            _isClosingFromConfig = true;
            Close();
        }

        public void EnqueueNotification(DesktopPetNotification notification)
        {
            if (!PetConfig.ShowNotifications || string.IsNullOrWhiteSpace(notification.Message))
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => EnqueueNotification(notification));
                return;
            }

            _notifications.Enqueue(notification);
            TryShowNextNotification();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                TapPet();
                try
                {
                    DragMove();
                    WindowConfig.SetConfig(this);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildContextMenu();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _motionStartedAt = DateTime.Now;
            _motionTimer.Start();
            _blinkTimer.Start();
            ResetIdleTipTimer();
            await TryLoadLive2DAsync();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _motionTimer.Stop();
            _messageTimer.Stop();
            _idleTipTimer.Stop();
            _blinkTimer.Stop();
            PetConfig.PropertyChanged -= PetConfig_PropertyChanged;
            DesktopPetService.GetInstance().Detach(this);

            if (!_isClosingFromConfig && MainWindowConfig.Instance.OpenFloatingBall)
            {
                MainWindowConfig.Instance.OpenFloatingBall = false;
            }
        }

        private void PetConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DesktopPetConfig.EnableLive2DRenderer) ||
                e.PropertyName == nameof(DesktopPetConfig.Live2DPath) ||
                e.PropertyName == nameof(DesktopPetConfig.Live2DMaxFps) ||
                e.PropertyName == nameof(DesktopPetConfig.Live2DRenderScale) ||
                e.PropertyName == nameof(DesktopPetConfig.EnableLive2DMotionEffects))
            {
                _ = TryLoadLive2DAsync();
            }
            else if (e.PropertyName == nameof(DesktopPetConfig.EnableIdleTips) ||
                     e.PropertyName == nameof(DesktopPetConfig.IdleTipIntervalMinutes))
            {
                ResetIdleTipTimer();
            }
            else if (e.PropertyName == nameof(DesktopPetConfig.PetScale) ||
                     e.PropertyName == nameof(DesktopPetConfig.PetOpacity))
            {
                ClampVisualConfig();
            }
        }

        private void ConfigureTimers()
        {
            _motionTimer.Interval = TimeSpan.FromMilliseconds(33);
            _motionTimer.Tick += (_, _) =>
            {
                var seconds = (DateTime.Now - _motionStartedAt).TotalSeconds;
                PetFloatTransform.Y = Math.Sin(seconds * 2.2) * 5;
                PetTiltTransform.Angle = Math.Sin(seconds * 0.9) * 1.4;
            };

            _messageTimer.Tick += (_, _) =>
            {
                _messageTimer.Stop();
                HideCurrentNotification();
            };

            _idleTipTimer.Tick += (_, _) =>
            {
                if (PetConfig.EnableIdleTips && !_isShowingMessage)
                {
                    var tips = new[]
                    {
                        Properties.Resources.DesktopPetIdleTip1,
                        Properties.Resources.DesktopPetIdleTip2,
                        Properties.Resources.DesktopPetIdleTip3,
                        Properties.Resources.DesktopPetIdleTip4
                    };
                    EnqueueNotification(new DesktopPetNotification
                    {
                        Title = PetConfig.PetName,
                        Message = tips[_random.Next(tips.Length)],
                        Kind = DesktopPetNotificationKind.Info
                    });
                }
                ResetIdleTipTimer();
            };

            _blinkTimer.Interval = TimeSpan.FromSeconds(4);
            _blinkTimer.Tick += async (_, _) => await BlinkAsync();
        }

        private void PlaceDefaultPositionIfNeeded()
        {
            if (WindowConfig.Top != 0 || WindowConfig.Left != 0)
                return;

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var workingArea = screen.WorkingArea;

            double dpiX;
            double dpiY;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformFromDevice.M11;
                dpiY = source.CompositionTarget.TransformFromDevice.M22;
            }
            else
            {
                using var graphics = Graphics.FromHwnd(IntPtr.Zero);
                dpiX = 96.0 / graphics.DpiX;
                dpiY = 96.0 / graphics.DpiY;
            }

            WindowConfig.Left = workingArea.Right * dpiX - Width - 80;
            WindowConfig.Top = workingArea.Bottom * dpiY - Height - 70;
        }

        private void ClampVisualConfig()
        {
            if (PetConfig.PetScale < 0.65)
                PetConfig.PetScale = 0.65;
            else if (PetConfig.PetScale > 1.45)
                PetConfig.PetScale = 1.45;

            if (PetConfig.PetOpacity < 0.35)
                PetConfig.PetOpacity = 0.35;
            else if (PetConfig.PetOpacity > 1)
                PetConfig.PetOpacity = 1;

            if (PetConfig.Live2DMaxFps < 15)
                PetConfig.Live2DMaxFps = 15;
            else if (PetConfig.Live2DMaxFps > 60)
                PetConfig.Live2DMaxFps = 60;

            if (PetConfig.Live2DRenderScale < 0.4)
                PetConfig.Live2DRenderScale = 0.4;
            else if (PetConfig.Live2DRenderScale > 1)
                PetConfig.Live2DRenderScale = 1;
        }

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenu();

            var testMessage = new MenuItem { Header = Properties.Resources.DesktopPetSendTestReminder };
            testMessage.Click += (_, _) => DesktopPetService.GetInstance().Notify(Properties.Resources.DesktopPetReminder, Properties.Resources.DesktopPetTestMessage, DesktopPetNotificationKind.Success);
            contextMenu.Items.Add(testMessage);

            var showMainWindow = new MenuItem { Header = Properties.Resources.DesktopPetShowMainWindow };
            showMainWindow.Click += (_, _) => DesktopPetService.GetInstance().ShowMainWindow();
            contextMenu.Items.Add(showMainWindow);

            var settings = new MenuItem { Header = Properties.Resources.DesktopPetSettings };
            settings.Click += (_, _) => DesktopPetService.GetInstance().OpenSettings();
            contextMenu.Items.Add(settings);

            contextMenu.Items.Add(new Separator());

            var topmost = new MenuItem { Header = Properties.Resources.DesktopPetAlwaysOnTop, IsCheckable = true, IsChecked = PetConfig.AlwaysOnTop };
            topmost.Click += (_, _) => PetConfig.AlwaysOnTop = topmost.IsChecked;
            contextMenu.Items.Add(topmost);

            var notifications = new MenuItem { Header = Properties.Resources.DesktopPetShowNotifications, IsCheckable = true, IsChecked = PetConfig.ShowNotifications };
            notifications.Click += (_, _) => PetConfig.ShowNotifications = notifications.IsChecked;
            contextMenu.Items.Add(notifications);

            contextMenu.Items.Add(new Separator());

            var hide = new MenuItem { Header = Properties.Resources.DesktopPetHide };
            hide.Click += (_, _) => MainWindowConfig.Instance.OpenFloatingBall = false;
            contextMenu.Items.Add(hide);

            var exit = new MenuItem { Header = Properties.Resources.DesktopPetExit };
            exit.Click += (_, _) => Application.Current.Shutdown();
            contextMenu.Items.Add(exit);

            ContextMenu = contextMenu;
        }

        private void TapPet()
        {
            var scaleUp = new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(90)) { AutoReverse = true };
            PetTapScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleUp);
            PetTapScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleUp);

            if (!_isShowingMessage && _random.NextDouble() < 0.35)
            {
                EnqueueNotification(new DesktopPetNotification
                {
                    Title = PetConfig.PetName,
                    Message = Properties.Resources.DesktopPetIdleTip1,
                    Kind = DesktopPetNotificationKind.Info
                });
            }
        }

        private async Task BlinkAsync()
        {
            LeftEyeScale.ScaleY = 0.08;
            RightEyeScale.ScaleY = 0.08;
            await Task.Delay(110);
            LeftEyeScale.ScaleY = 1;
            RightEyeScale.ScaleY = 1;
        }

        private void TryShowNextNotification()
        {
            if (_isShowingMessage || _notifications.Count == 0)
                return;

            var notification = _notifications.Dequeue();
            _isShowingMessage = true;
            MessageTitle.Text = string.IsNullOrWhiteSpace(notification.Title) ? PetConfig.PetName : notification.Title;
            MessageText.Text = notification.Message;
            ApplyMessageColor(notification.Kind);

            MessageBubble.Visibility = Visibility.Visible;
            MessageBubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
            MessageBubbleTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));

            var seconds = Math.Max(2, Math.Min(20, PetConfig.MessageDisplaySeconds));
            _messageTimer.Interval = TimeSpan.FromSeconds(seconds);
            _messageTimer.Start();
        }

        private void HideCurrentNotification()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) =>
            {
                MessageBubble.Visibility = Visibility.Collapsed;
                MessageBubbleTransform.Y = 8;
                _isShowingMessage = false;
                TryShowNextNotification();
            };

            MessageBubble.BeginAnimation(OpacityProperty, fade);
            MessageBubbleTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(8, TimeSpan.FromMilliseconds(160)));
        }

        private void ApplyMessageColor(DesktopPetNotificationKind kind)
        {
            string color = kind switch
            {
                DesktopPetNotificationKind.Success => "#3B1BA784",
                DesktopPetNotificationKind.Warning => "#4BD99000",
                DesktopPetNotificationKind.Error => "#4BDA3B3B",
                _ => "#3B2F5BFF"
            };
            MessageBubbleBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
        }

        private void ResetIdleTipTimer()
        {
            _idleTipTimer.Stop();
            if (!PetConfig.EnableIdleTips)
                return;

            var minutes = Math.Max(5, Math.Min(240, PetConfig.IdleTipIntervalMinutes));
            _idleTipTimer.Interval = TimeSpan.FromMinutes(minutes);
            _idleTipTimer.Start();
        }

        private async Task TryLoadLive2DAsync()
        {
            if (!PetConfig.EnableLive2DRenderer || string.IsNullOrWhiteSpace(PetConfig.Live2DPath) || !File.Exists(PetConfig.Live2DPath))
            {
                ShowFallbackPet();
                return;
            }

            try
            {
                await WebViewService.EnsureWebViewInitializedAsync(Live2DView);
                if (Live2DView.CoreWebView2 == null)
                {
                    ShowFallbackPet();
                    return;
                }

                Live2DView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                Live2DView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                Live2DView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                Live2DView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                await EnsureWebViewBridgeScriptAsync();

                Live2DView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                Live2DView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                var extension = Path.GetExtension(PetConfig.Live2DPath);
                Live2DView.Visibility = Visibility.Visible;
                if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase))
                {
                    Live2DView.CoreWebView2.Navigate(new Uri(PetConfig.Live2DPath).AbsoluteUri);
                }
                else
                {
                    var modelFolder = Path.GetDirectoryName(PetConfig.Live2DPath);
                    if (string.IsNullOrWhiteSpace(modelFolder))
                    {
                        ShowFallbackPet();
                        return;
                    }

                    const string modelHost = "colorvision-desktop-pet.local";
                    Live2DView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        modelHost,
                        modelFolder,
                        CoreWebView2HostResourceAccessKind.Allow);

                    var modelUrl = $"https://{modelHost}/{Uri.EscapeDataString(Path.GetFileName(PetConfig.Live2DPath))}";
                    Live2DView.NavigateToString(BuildLive2DModelHtml(
                        modelUrl,
                        Math.Max(15, Math.Min(60, PetConfig.Live2DMaxFps)),
                        Math.Max(0.4, Math.Min(1, PetConfig.Live2DRenderScale)),
                        PetConfig.EnableLive2DMotionEffects));
                }
            }
            catch (Exception ex)
            {
                log.Warn("Live2D 初始化失败", ex);
                ShowFallbackPet();
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message == "desktop-pet-drag")
            {
                TapPet();
                BeginNativeDrag();
            }
            else if (message == "desktop-pet-context")
            {
                OpenContextMenu();
            }
            else if (message == "desktop-pet-tap")
            {
                TapPet();
            }
            else if (message == "live2d-ready")
            {
                Live2DView.Visibility = Visibility.Visible;
                FallbackPet.Visibility = Visibility.Collapsed;
            }
            else if (message != null && message.StartsWith("live2d-error:", StringComparison.Ordinal))
            {
                var error = message["live2d-error:".Length..];
                ShowFallbackPet();
                EnqueueNotification(new DesktopPetNotification
                {
                    Title = "Live2D",
                    Message = string.IsNullOrWhiteSpace(error)
                        ? Properties.Resources.DesktopPetLive2DError
                        : string.Format(Properties.Resources.DesktopPetLive2DErrorDetail, error),
                    Kind = DesktopPetNotificationKind.Warning
                });
            }
        }

        private void BeginNativeDrag()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            ReleaseCapture();
            SendMessage(hwnd, WmNclbuttondown, new IntPtr(Htcaption), IntPtr.Zero);
            WindowConfig.SetConfig(this);
        }

        private void OpenContextMenu()
        {
            if (ContextMenu == null)
                return;

            ContextMenu.PlacementTarget = this;
            ContextMenu.IsOpen = true;
        }

        private async Task EnsureWebViewBridgeScriptAsync()
        {
            if (_webViewBridgeScriptInjected || Live2DView.CoreWebView2 == null)
                return;

            await Live2DView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildDesktopPetBridgeScript());
            _webViewBridgeScriptInjected = true;
        }

        private void ShowFallbackPet()
        {
            Live2DView.Visibility = Visibility.Collapsed;
            FallbackPet.Visibility = Visibility.Visible;
        }

        private static string BuildLive2DModelHtml(string modelUrl, int maxFps, double renderScale, bool enableMotionEffects)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
            sb.AppendLine("html,body,#stage{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}");
            sb.AppendLine("body{user-select:none;-webkit-user-select:none;}");
            sb.AppendLine("#stage{cursor:grab;}");
            sb.AppendLine("#stage:active{cursor:grabbing;}");
            sb.AppendLine("canvas{width:100%;height:100%;}");
            sb.AppendLine("</style></head><body><div id=\"stage\"></div>");
            sb.AppendLine("<script src=\"https://cubism.live2d.com/sdk-web/cubismcore/live2dcubismcore.min.js\"></script>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/pixi.js@6.5.10/dist/browser/pixi.min.js\"></script>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/pixi-live2d-display@0.4.0/dist/cubism4.min.js\"></script>");
            sb.AppendLine("<script>");
            sb.AppendLine("(async function(){");
            sb.AppendLine("try{");
            sb.AppendLine("if(!window.PIXI||!PIXI.live2d){throw new Error('Live2D runtime not found');}");
            sb.Append("const renderScale=").Append(JsonConvert.SerializeObject(renderScale)).AppendLine(";");
            sb.Append("const maxFps=").Append(JsonConvert.SerializeObject(maxFps)).AppendLine(";");
            sb.Append("const motionEffects=").Append(JsonConvert.SerializeObject(enableMotionEffects)).AppendLine(";");
            sb.AppendLine("const app=new PIXI.Application({view:document.createElement('canvas'),autoStart:true,transparent:true,resizeTo:document.getElementById('stage'),autoDensity:true,resolution:renderScale});");
            sb.AppendLine("app.ticker.maxFPS=maxFps;");
            sb.AppendLine("document.getElementById('stage').appendChild(app.view);");
            sb.Append("const model=await PIXI.live2d.Live2DModel.from(").Append(JsonConvert.SerializeObject(modelUrl)).AppendLine(");");
            sb.AppendLine("model.anchor.set(0.5,1);");
            sb.AppendLine("let baseScale=1, pointerX=0, pointerY=0;");
            sb.AppendLine("function layout(){baseScale=Math.min(app.renderer.width/model.width,app.renderer.height/model.height)*0.95;model.scale.set(baseScale);model.x=app.renderer.width/2;model.y=app.renderer.height;}");
            sb.AppendLine("layout();");
            sb.AppendLine("app.stage.addChild(model);window.chrome.webview.postMessage('live2d-ready');");
            sb.AppendLine("window.addEventListener('resize',layout);");
            sb.AppendLine("window.addEventListener('pointermove',e=>{pointerX=(e.clientX/window.innerWidth-.5);pointerY=(e.clientY/window.innerHeight-.5);});");
            sb.AppendLine("app.ticker.add(()=>{if(!motionEffects)return;const t=performance.now()/1000;model.y=app.renderer.height+Math.sin(t*2.2)*5;model.rotation=Math.sin(t*.9)*.015+pointerX*.025;model.scale.set(baseScale*(1+Math.sin(t*1.6)*.01));if(model.internalModel&&model.internalModel.coreModel){const c=model.internalModel.coreModel;try{c.setParameterValueById('ParamAngleX',pointerX*25);c.setParameterValueById('ParamAngleY',-pointerY*18);c.setParameterValueById('ParamAngleZ',pointerX*8);}catch{}}});");
            sb.AppendLine("}catch(e){window.chrome.webview.postMessage('live2d-error:'+(e&&e.message?e.message:e));}");
            sb.AppendLine("})();");
            sb.AppendLine("</script></body></html>");
            return sb.ToString();
        }

        private static string BuildDesktopPetBridgeScript()
        {
            return @"
(function(){
    if (window.__colorVisionDesktopPetBridge) return;
    window.__colorVisionDesktopPetBridge = true;
    function post(message) {
        try {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(message);
            }
        } catch (_) {}
    }
    function bind() {
        document.addEventListener('contextmenu', function(e) {
            e.preventDefault();
            post('desktop-pet-context');
        }, true);
        document.addEventListener('pointerdown', function(e) {
            if (e.button === 0 && e.altKey) {
                e.preventDefault();
                post('desktop-pet-drag');
            } else if (e.button === 2) {
                e.preventDefault();
                post('desktop-pet-context');
            }
        }, true);
        document.addEventListener('dblclick', function() {
            post('desktop-pet-tap');
        }, true);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bind, { once: true });
    } else {
        bind();
    }
})();";
        }
    }
}
