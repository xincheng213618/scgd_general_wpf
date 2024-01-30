using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StackPanelDragAndDrop
{
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    /// <summary>
    /// UIElement修飾基本クラス
    /// </summary>
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    public class UIElementAdornerBase : Adorner
    {
        //=================================================
        // プロテクトCLRプロパティ
        //=================================================

        #region プロテクトCLRプロパティ
        /// <summary>装飾ビジュアルオブジェクト</summary>
        protected Grid AdornerVisual { get; private set; } = new Grid();

        /// <summary>装飾描画サーフェース</summary>
        protected AdornerLayer AdornerLayer { get; private set; } = null;
        #endregion

        //=================================================
        // コンストラクター
        //=================================================

        #region コンストラクター
        /// <summary>コンストラクター</summary>
        /// <param name="adornedElement">修飾エレメント</param>
        protected UIElementAdornerBase(UIElement adornedElement) : base(adornedElement)
        {
            // 装飾描画サーフェースの初期化
            AdornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            AdornerLayer?.Add(this);
        }
        #endregion

        //=================================================
        // パブリックメソッド
        //=================================================

        #region パブリックメソッド
        /// <summary>デタッチ</summary>
        public void Detach()
        {
            // 装飾描画サーフェースからこの装飾オブジェクトを削除
            AdornerLayer?.Remove(this);
        }
        #endregion

        //=================================================
        // 修飾基本クラスのオーバーライド
        //=================================================

        #region 修飾基本クラスのオーバーライド
        /// <summary>ビジュアル子要素数</summary>
        /// <remarks>常に1を返す</remarks>
        protected override int VisualChildrenCount
        {
            get { return 1; }
        }

        /// <summary>ビジュアル子要素の取得</summary>
        /// <param name="index">インデックス</param>
        /// <returns>ビジュアル要素</returns>
        /// <exception cref="IndexOutOfRangeException">インデックスの指定が間違っている</exception>
        protected override Visual GetVisualChild(int index)
        {
            if (VisualChildrenCount <= index)
            {
                throw new IndexOutOfRangeException();
            }
            return AdornerVisual;
        }

        /// <summary>子要素に必要なレイアウトのサイズを測定</summary>
        /// <param name="availableSize">親要素が子要素を割り当てることができる使用可能な領域</param>
        /// <returns>子要素に必要なレイアウトのサイズ</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            // 装飾ビジュアルオブジェクトのレイアウト更新
            AdornerVisual.Measure(availableSize);
            // 子要素に必要なレイアウトのサイズを測定して決定したサイズを返す
            return base.MeasureOverride(availableSize);
        }

        /// <summary>子要素を配置</summary>
        /// <param name="finalSize">要素自体と子を配置するために使用する親の末尾の領域</param>
        /// <returns>子要素を配置して決定したサイズ</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            // 装飾ビジュアルオブジェクトの子要素を配置
            AdornerVisual.Arrange(new Rect(finalSize));
            // 子要素を配置して決定したサイズを返す
            return base.ArrangeOverride(finalSize);
        }
        #endregion
    }
}
