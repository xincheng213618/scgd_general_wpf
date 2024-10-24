using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Adorners
{
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    /// <summary>
    /// ドラッグユーザーコントロール修飾クラス
    /// </summary>
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    public class DragUserControlAdorner : UIElementAdornerBase
    {
        //=================================================
        // フィールド
        //=================================================

        #region フィールド
        /// <summary>コンテンツプレゼンター</summary>
        private readonly ContentPresenter _contentPresenter;

        /// <summary>移動トランスフォーム</summary>
        private TranslateTransform _translate = new(0.0, 0.0);

        /// <summary>オフセット位置</summary>
        private Point _offset;
        #endregion

        //=================================================
        // コンストラクター
        //=================================================

        #region コンストラクター
        /// <summary>コンストラクター</summary>
        /// <param name="adornedElement">装飾エレメント</param>
        /// <param name="draggedControl">ドラッグするコントロール</param>
        /// <param name="offset">固定オフセット量</param>
        public DragUserControlAdorner(UIElement adornedElement,
                                  FrameworkElement draggedControl,
                                  Point offset) : base(adornedElement)
        {
            // ドラッグするコントロールをイメージに変換
            Rect bounds = VisualTreeHelper.GetDescendantBounds(draggedControl);
            DrawingVisual dv = new();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new(draggedControl)
                {
                    Stretch = Stretch.None // 防止VisualBrush缩放
                };
                ctx.DrawRectangle(vb, null, new Rect(bounds.Size));
            }

            double dpiX = 96.0;
            double dpiY = 96.0;

            using (var source = new HwndSource(new HwndSourceParameters()))
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11 * 96.0;
                dpiY = source.CompositionTarget.TransformToDevice.M22 * 96.0;
            }

            var renderBmp = new RenderTargetBitmap(
                (int)(draggedControl.ActualWidth * dpiX / 96.0),
                (int)(draggedControl.ActualHeight * dpiY / 96.0),
                dpiX, dpiY, PixelFormats.Pbgra32);


            RenderOptions.SetEdgeMode(dv, EdgeMode.Aliased); // 设置抗锯齿模式

            RenderOptions.SetBitmapScalingMode(renderBmp, BitmapScalingMode.HighQuality);
            renderBmp.Render(dv);
            var image = new Image() { SnapsToDevicePixels = true};
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            image.Source = renderBmp;

            // コンテンツプレゼンターを生成して装飾ビジュアルオブジェクトに追加
            _contentPresenter = new ContentPresenter
            {
                Content = image,
                Opacity = 1
            };
            AdornerVisual.Children.Add(_contentPresenter);

            // コンテンツプレゼンターに移動トランスフォームを設定
            _contentPresenter.RenderTransform = _translate;

            // コンテンツプレゼンターに水平/垂直アライメントを設定
            _contentPresenter.HorizontalAlignment = HorizontalAlignment.Left;
            _contentPresenter.VerticalAlignment = VerticalAlignment.Top;

            // 固定オフセット量を保存
            _offset = offset;
        }
        #endregion

        //=================================================
        // パブリックメソッド
        //=================================================

        #region パブリックメソッド
        /// <summary>ドラッグコンテンツ修飾の表示位置設定</summary>
        /// <param name="screenPosition">表示位置(スクリーン座標)</param>
        public void SetScreenPosition(Point screenPosition)
        {
            // コンテンツプレゼンターの表示位置を変更
            var positionInControl = AdornedElement.PointFromScreen(screenPosition);
            _translate.X = positionInControl.X - _offset.X;
            _translate.Y = positionInControl.Y - _offset.Y;

            // 装飾描画サーフェースのレイアウト更新と再描画
            AdornerLayer.Update();
        }
        #endregion
    }
}
