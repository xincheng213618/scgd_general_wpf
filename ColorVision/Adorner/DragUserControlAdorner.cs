using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StackPanelDragAndDrop
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
        private TranslateTransform _translate = new TranslateTransform(0.0, 0.0);

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
                                  UserControl draggedControl,
                                  Point offset) : base(adornedElement)
        {
            // ドラッグするコントロールをイメージに変換
            Rect bounds = VisualTreeHelper.GetDescendantBounds(draggedControl);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(draggedControl);
                ctx.DrawRectangle(vb, null, new Rect(bounds.Size));
            }
            var renderBmp = new RenderTargetBitmap((int)draggedControl.ActualWidth,
                                                   (int)draggedControl.ActualHeight,
                                                   96.0, 96.0, PixelFormats.Pbgra32);
            renderBmp.Render(dv);
            var image = new Image();
            image.Source = renderBmp;

            // コンテンツプレゼンターを生成して装飾ビジュアルオブジェクトに追加
            _contentPresenter = new ContentPresenter
            {
                Content = image,
                Opacity = 0.7
            };
            base.AdornerVisual.Children.Add(_contentPresenter);

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
            var positionInControl = base.AdornedElement.PointFromScreen(screenPosition);
            _translate.X = positionInControl.X - _offset.X;
            _translate.Y = positionInControl.Y - _offset.Y;

            // 装飾描画サーフェースのレイアウト更新と再描画
            base.AdornerLayer.Update();
        }
        #endregion
    }
}
