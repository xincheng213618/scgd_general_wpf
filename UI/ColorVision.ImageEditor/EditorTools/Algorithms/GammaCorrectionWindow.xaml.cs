using ColorVision.Common.Utilities;
using ColorVision.Core;
using log4net;
using System.Diagnostics;
using System.Threading.Tasks; // 必须引用
using System.Windows;
using System.Windows.Media.Imaging; // 引用 WriteableBitmap

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// GammaCorrectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GammaCorrectionWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GammaCorrectionWindow));
        private readonly ImageView _imageView;

        // 建议：持有 Source HImage 的只读引用或克隆，视 HImageCache 生命周期而定
        // 如果 HImageCache 可能被外部 Dispose，这里最好 Clone 一份
        private readonly HImage _sourceHImage;

        public GammaCorrectionWindow(ImageView imageView)
        {
            InitializeComponent();
            _imageView = imageView;

            // 确保有一个源数据，如果 ImageView 没有缓存，就新建一个空的（或者根据逻辑处理）
            _sourceHImage = imageView.HImageCache ?? new HImage();
        }

        private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            // 1. 快照参数：立即获取 Slider 的值
            double gammaValue = e.NewValue;

            // 2. 调用异步任务，利用新的 TaskConflator 支持 await 的特性
            // 设置 20ms 的节流，避免高频计算
            TaskConflator.RunOrUpdate("ApplyGammaCorrection", async () =>
            {
                await ApplyGammaCorrectionAsync(gammaValue);
            }, throttleDelayMs: 100);
        }

        private async Task ApplyGammaCorrectionAsync(double gamma)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // ---------------------------------------------------------
            // 步骤 1: 算法处理 (后台线程)
            // ---------------------------------------------------------
            // 假设 M_ApplyGammaCorrection 是耗时的，且内部线程安全（只读输入）
            // 如果 M_ApplyGammaCorrection 不是纯函数，需要加锁，但通常 OpenCV 封装是独立的
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection(_sourceHImage, out HImage hImageProcessed, gamma);

            // 注意：上面的 Task.Run 写法稍微有点伪代码，通常 OpenCVMediaHelper 方法签名如果是同步的：
            /*
            HImage hImageProcessed = null;
            int ret = await Task.Run(() => OpenCVMediaHelper.M_ApplyGammaCorrection(_sourceHImage, out hImageProcessed, gamma));
            */

            double algoMs = stopwatch.Elapsed.TotalMilliseconds;

            if (ret != 0)
            {
                hImageProcessed.Dispose();
                return;
            }

            // ---------------------------------------------------------
            // 步骤 2: 渲染更新 (切回 UI 线程)
            // ---------------------------------------------------------
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // 再次检查窗口是否关闭（如果用户在计算时关闭了窗口）
                if (!IsVisible)
                {
                    hImageProcessed.Dispose();
                    return;
                }

                bool updateSuccess = false;

                // 尝试复用现有的 WriteableBitmap
                if (_imageView.FunctionImage is WriteableBitmap validBitmap)
                {
                    // 使用之前优化过的 UpdateWriteableBitmapAsync (并行拷贝)
                    updateSuccess = await HImageExtension.UpdateWriteableBitmapAsync(validBitmap, hImageProcessed);
                }

                // 如果复用失败（尺寸不匹配或为空），创建新的
                if (!updateSuccess)
                {
                    double dpiX = _imageView.Config.GetProperties<double>("DpiX");
                    double dpiY = _imageView.Config.GetProperties<double>("DpiY");

                    // 使用之前优化过的 ToWriteableBitmapAsync (并行拷贝)
                    var newBitmap = await hImageProcessed.ToWriteableBitmapAsync(dpiX, dpiY);
                    _imageView.FunctionImage = newBitmap;

                    // 创建新图后，源数据可以释放
                    hImageProcessed.Dispose();
                }

                // 绑定显示
                if (_imageView.ImageShow.Source != _imageView.FunctionImage)
                {
                    _imageView.ImageShow.Source = _imageView.FunctionImage;
                }

                stopwatch.Stop();
                double renderMs = stopwatch.Elapsed.TotalMilliseconds;

                if (log.IsInfoEnabled)
                {
                    log.Info($"[Gamma] Algo: {algoMs:F2}ms | Render: {renderMs - algoMs:F2}ms | Total: {renderMs:F2}ms | Val: {gamma:F2}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 停止之前的任何预览任务
            TaskConflator.RunOrUpdate("ApplyGammaCorrection", () => Task.CompletedTask);

            // 应用更改到原始图像
            if (_imageView.FunctionImage is WriteableBitmap writeableBitmap)
            {
                _imageView.ViewBitmapSource = writeableBitmap;
                _imageView.ImageShow.Source = _imageView.ViewBitmapSource;

                // 注意：这里把 HImageCache 置空了，意味着下次进来 _imageView.HImageCache 为 null
                // 确保你的系统逻辑允许这样做，或者在这里根据 FunctionImage 重新生成 HImageCache
                _imageView.HImageCache = null;
                _imageView.FunctionImage = null;
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 停止任务
            TaskConflator.RunOrUpdate("ApplyGammaCorrection", () => Task.CompletedTask);

            // 取消更改，恢复原始图像
            _imageView.ImageShow.Source = _imageView.ViewBitmapSource;
            _imageView.FunctionImage = null;
            Close();
        }

        // 窗口关闭时也要清理
        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            // 这里也可以清理 TaskConflator，如果不希望它在后台继续跑
        }
    }
}