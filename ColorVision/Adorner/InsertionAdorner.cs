using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StackPanelDragAndDrop
{
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    /// <summary>
    /// 挿入位置修飾クラス
    /// </summary>
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    public class InsertionAdorner : UIElementAdornerBase
    {
        //=================================================
        // フィールド
        //=================================================

        #region フィールド
        /// <summary>挿入位置カーソルコントロール</summary>
        private readonly Path _insertionCursor = new Path();
        #endregion

        //=================================================
        // コンストラクター
        //=================================================

        #region コンストラクター
        /// <summary>コンストラクター</summary>
        /// <param name="adornedElement">装飾エレメント</param>
        /// <param name="showInRightSide">挿入位置が右側の場合true、左側の場合falseを指定する</param>
        public InsertionAdorner(UserControl adornedElement,
                                bool showInRightSide = false) : base(adornedElement)
        {
            // 装飾エレメントの高さを取得
            double height = adornedElement.ActualHeight;
            
            // 装飾ビジュアルオブジェクトに挿入位置カーソルコントロール追加
            _insertionCursor.Data = new LineGeometry(new Point(0, 0), new Point(0, height));
            _insertionCursor.Stroke = new SolidColorBrush(Colors.Red);
            _insertionCursor.StrokeThickness = 2.0;
            AdornerVisual.Children.Add(_insertionCursor);

            // 挿入位置カーソルコントロールの水平/垂直アライメントの設定
            _insertionCursor.HorizontalAlignment = showInRightSide? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _insertionCursor.VerticalAlignment = VerticalAlignment.Stretch;
        }
        #endregion
    }
}
