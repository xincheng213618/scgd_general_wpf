using ColorVision.ImageEditor.Draw;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// The type of shape to crop/select
    /// </summary>
    public enum CropShapeType
    {
        Rectangle,
        Circle
    }

    /// <summary>
    /// Result of shape selection in ImageCropperWindow
    /// </summary>
    public class CropResult
    {
        /// <summary>
        /// The bounding rectangle of the selected area
        /// </summary>
        public Rect Rect { get; set; }

        /// <summary>
        /// For circle selections: the center point
        /// </summary>
        public Point Center { get; set; }

        /// <summary>
        /// For circle selections: the radius
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// The shape type that was selected
        /// </summary>
        public CropShapeType ShapeType { get; set; }
    }

    /// <summary>
    /// A reusable dialog window for selecting an area on an image.
    /// Opens an image, lets the user draw a rectangle or circle, and returns the shape properties.
    /// 
    /// Usage:
    ///   var cropper = new ImageCropperWindow(imagePath, CropShapeType.Rectangle);
    ///   cropper.Owner = this;
    ///   if (cropper.ShowDialog() == true)
    ///   {
    ///       Rect selectedArea = cropper.CropResult.Rect;
    ///   }
    /// </summary>
    public partial class ImageCropperWindow : Window
    {
        /// <summary>
        /// The result of the crop operation. Available after DialogResult == true.
        /// </summary>
        public CropResult CropResult { get; private set; }

        /// <summary>
        /// The shape type to select
        /// </summary>
        public CropShapeType ShapeType { get; set; }

        private IDrawingVisual _drawnVisual;

        /// <summary>
        /// Create a cropper window with an image file path
        /// </summary>
        public ImageCropperWindow(string imagePath, CropShapeType shapeType = CropShapeType.Rectangle)
        {
            ShapeType = shapeType;
            InitializeComponent();
            Loaded += (s, e) =>
            {
                ImageView.OpenImage(imagePath);
                SetupDrawingMode();
            };
        }

        /// <summary>
        /// Create a cropper window with a BitmapSource (converts to WriteableBitmap if needed)
        /// </summary>
        public ImageCropperWindow(BitmapSource bitmapSource, CropShapeType shapeType = CropShapeType.Rectangle)
        {
            ShapeType = shapeType;
            InitializeComponent();
            Loaded += (s, e) =>
            {
                WriteableBitmap wb = bitmapSource as WriteableBitmap ?? new WriteableBitmap(bitmapSource);
                ImageView.OpenImage(wb);
                SetupDrawingMode();
            };
        }

        /// <summary>
        /// Create a cropper window with image width/height (white canvas)
        /// </summary>
        public ImageCropperWindow(int width, int height, CropShapeType shapeType = CropShapeType.Rectangle)
        {
            ShapeType = shapeType;
            InitializeComponent();
            Loaded += (s, e) =>
            {
                var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                byte[] pixels = new byte[width * height * 3];
                Array.Fill(pixels, (byte)255);
                wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 3, 0);
                ImageView.OpenImage(wb);
                SetupDrawingMode();
            };
        }

        private void SetupDrawingMode()
        {
            ImageView.ImageViewModel.ImageEditMode = true;

            switch (ShapeType)
            {
                case CropShapeType.Rectangle:
                    if (ImageView.ImageViewModel.IEditorToolFactory.GetIEditorTool<RectangleManager>() is RectangleManager rectMgr)
                    {
                        rectMgr.Config.IsContinuous = false;
                        rectMgr.IsChecked = true;
                    }
                    break;
                case CropShapeType.Circle:
                    if (ImageView.ImageViewModel.IEditorToolFactory.GetIEditorTool<CircleManager>() is CircleManager circleMgr)
                    {
                        circleMgr.Config.IsContinuous = false;
                        circleMgr.IsChecked = true;
                    }
                    break;
            }

            ImageView.ImageShow.VisualsAdd += OnVisualAdded;
        }

        private void OnVisualAdded(object sender, VisualChangedEventArgs e)
        {
            if (_drawnVisual != null)
            {
                ImageView.ImageShow.RemoveVisual((Visual)_drawnVisual);
            }

            if (e.Visual is DVRectangleText rectText)
            {
                _drawnVisual = rectText;
                UpdateResultFromRect(rectText.Attribute.Rect);
                rectText.Attribute.PropertyChanged += (s2, e2) =>
                {
                    UpdateResultFromRect(rectText.Attribute.Rect);
                };
                OKButton.IsEnabled = true;
            }
            else if (e.Visual is DVCircleText circleText)
            {
                _drawnVisual = circleText;
                UpdateResultFromCircle(circleText.Attribute.Center, circleText.Attribute.Radius);
                circleText.Attribute.PropertyChanged += (s2, e2) =>
                {
                    UpdateResultFromCircle(circleText.Attribute.Center, circleText.Attribute.Radius);
                };
                OKButton.IsEnabled = true;
            }
        }

        private void UpdateResultFromRect(Rect rect)
        {
            CropResult = new CropResult
            {
                Rect = rect,
                Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                Radius = Math.Min(rect.Width, rect.Height) / 2,
                ShapeType = CropShapeType.Rectangle
            };
            ResultText.Text = $"X={rect.X:F0}, Y={rect.Y:F0}, W={rect.Width:F0}, H={rect.Height:F0}";
        }

        private void UpdateResultFromCircle(Point center, double radius)
        {
            CropResult = new CropResult
            {
                Rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2),
                Center = center,
                Radius = radius,
                ShapeType = CropShapeType.Circle
            };
            ResultText.Text = $"Center=({center.X:F0},{center.Y:F0}), R={radius:F0}";
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (CropResult != null)
            {
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
