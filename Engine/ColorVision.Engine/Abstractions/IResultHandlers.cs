using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Sorts;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine
{
    /// <summary>
    /// 结果渲染配置 - 控制图像叠加层的文字格式与外观
    /// </summary>
    public class ResultRenderConfig : ViewModelBase
    {
        [DisplayName("小数位数"), Category("渲染")]
        public int DecimalPlaces { get => _DecimalPlaces; set { _DecimalPlaces = value; OnPropertyChanged(); } }
        private int _DecimalPlaces = 3;

        [DisplayName("字体大小"), Category("渲染")]
        public double FontSize { get => _FontSize; set { _FontSize = value; OnPropertyChanged(); } }
        private double _FontSize = 10;

        [DisplayName("线条粗细"), Category("渲染")]
        public double PenThickness { get => _PenThickness; set { _PenThickness = value; OnPropertyChanged(); } }
        private double _PenThickness = 1;

        /// <summary>
        /// 格式化数值，使用当前配置的小数位数
        /// </summary>
        public string FormatNumber(double? value)
        {
            if (value == null) return string.Empty;
            return value.Value.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 视图图像接口 - 定义图像查看器的基本功能
    /// </summary>
    public class ViewResultContext
    {
        public ImageView ImageView { get; set; }
        public ListView ListView { get; set; }
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; }
        public TextBox SideTextBox { get; set; }
    }

    /// <summary>
    /// 结果处理接口 - 定义算法结果的处理方式
    /// </summary>
    public interface IResultHandle
    {
        /// <summary>
        /// 判断是否可以处理指定的结果
        /// </summary>
        bool CanHandle1(ViewResultAlg result);

        /// <summary>
        /// 处理算法结果
        /// </summary>
        void Handle(ViewResultContext context, ViewResultAlg result);

        /// <summary>
        /// 保存侧边栏数据
        /// </summary>
        void SideSave(ViewResultAlg result, string selectedPath);
    }

    /// <summary>
    /// 结果处理基类 - 提供 IResultHandle 的默认实现
    /// </summary>
    public abstract class IResultHandleBase : IResultHandle
    {
        /// <summary>
        /// 处理器显示名称
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 渲染配置
        /// </summary>
        public ResultRenderConfig RenderConfig { get; set; } = new ResultRenderConfig();

        /// <summary>
        /// 可以处理的算法类型列表
        /// </summary>
        public abstract List<ViewResultAlgType> CanHandle { get; }

        public virtual bool CanHandle1(ViewResultAlg result) => CanHandle.Contains(result.ResultType);


        public abstract void Handle(ViewResultContext context, ViewResultAlg result);

        public virtual void Load(ViewResultContext ctx, ViewResultAlg result)
        {
        }

        public virtual void SideSave(ViewResultAlg result, string selectedPath)
        {
        }

        public async void AddPOIPoint(ImageView imageView, List<POIPoint> PoiPoints)
        {
            imageView.ImageShow.Clear();
            await Task.Delay(1000);
            for (int i = 0; i < PoiPoints.Count; i++)
            {
                if (i % 10000 == 0)
                    await Task.Delay(30);

                var item = PoiPoints[i];
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        CircleTextProperties circleTextProperties = new CircleTextProperties();
                        circleTextProperties.Center = new Point(item.PixelX, item.PixelY);
                        circleTextProperties.Radius = item.Radius;
                        circleTextProperties.Brush = Brushes.Transparent;
                        circleTextProperties.Pen = new Pen(Brushes.Red, 1);
                        circleTextProperties.Id = item.Id ?? -1;
                        circleTextProperties.Text = item.Name;

                        DVCircleText Circle = new DVCircleText(circleTextProperties);
                        Circle.Render();
                        imageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                        rectangleTextProperties.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        rectangleTextProperties.Brush = Brushes.Transparent;
                        rectangleTextProperties.Pen = new Pen(Brushes.Red, 1);
                        rectangleTextProperties.Id = item.Id ?? -1;
                        rectangleTextProperties.Text = item.Name;

                        DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                        Rectangle.Render();
                        imageView.AddVisual(Rectangle);
                        break;
                    case POIPointTypes.SolidPoint:
                        CircleProperties circleProperties = new CircleProperties();
                        circleProperties.Center = new Point(item.PixelX, item.PixelY);
                        circleProperties.Radius = 10;
                        circleProperties.Brush = Brushes.Red;
                        circleProperties.Pen = new Pen(Brushes.Red, 1);
                        circleProperties.Id = item.Id ?? -1;

                        DVCircle Circle1 = new DVCircle(circleProperties);
                        Circle1.Render();
                        imageView.AddVisual(Circle1);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
