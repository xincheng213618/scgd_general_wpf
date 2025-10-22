using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using cvColorVision;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Media
{
    public class PoiImageViewComponent : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            imageView.ToolBarAl.Visibility  = Visibility.Visible;
            if (MySqlControl.GetInstance().IsConnect)
            {
                imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                imageView.ComboxPOITemplate.SelectedIndex = 0;
                imageView.ToolBarAl.Visibility = Visibility.Visible;   
            }
            else
            {
                Task.Run(() => LoadMysql(imageView));
            }
            imageView.ComboxPOITemplate.SelectionChanged += ComboxPOITemplate_SelectionChanged;


            async Task LoadMysql(ImageView imageView)
            {
                if (MySqlControl.GetInstance().IsConnect)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new TemplatePoi().Load();
                        imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                        imageView.ComboxPOITemplate.SelectedIndex = 0;
                        imageView.ToolBarAl.Visibility = Visibility.Visible;
                    });
                }
                else
                {
                    await Task.Delay(100);
                    await MySqlControl.GetInstance().Connect();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MySqlControl.GetInstance().IsConnect)
                        {
                            new TemplatePoi().Load();
                            imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                            imageView.ComboxPOITemplate.SelectedIndex = 0;
                            imageView.ToolBarAl.Visibility = Visibility.Visible;
                        }
                    });
                }


            }

            void ComboxPOITemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (sender is ComboBox comboBox && comboBox.SelectedValue is PoiParam poiParams)
                {
                    imageView.ImageShow.Clear();
                    imageView.DrawingVisualLists.Clear();

                    if (poiParams.Id == -1) return;

                    PoiParam.LoadPoiDetailFromDB(poiParams);
                    foreach (var item in poiParams.PoiPoints)
                    {
                        switch (item.PointType)
                        {
                            case GraphicTypes.Circle:
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                                Circle.Attribute.Radius = item.PixHeight / 2;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Circle.Attribute.Id = item.Id;
                                Circle.Attribute.Text = item.Name;
                                Circle.Render();
                                imageView.ImageShow.AddVisualCommand(Circle);
                                break;
                            case GraphicTypes.Rect:
                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Rectangle.Attribute.Id = item.Id;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Render();
                                imageView.ImageShow.AddVisualCommand(Rectangle);
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                        }
                    }
                }
            }



            WindowCIE windowCIE = null;

            void ButtonCIE1931_Click(object sender, RoutedEventArgs e)
            {
                if (windowCIE == null)
                {
                    windowCIE = new WindowCIE() { Owner = Application.Current.GetActiveWindow() };
                    void mouseMoveColorHandler(object s, ImageInfo e)
                    {
                        if (imageView.Config.Properties.TryGetValue("IsCVCIE", out object obj)&& obj is  bool iscvice &&iscvice)
                        {
                            int xx = e.X;
                            int yy = e.Y;
                            float dXVal = 0;
                            float dYVal = 0;
                            float dZVal = 0;
                            float dx = 0, dy = 0, du = 0, dv = 0;
                            int result = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, xx, yy, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, (int)imageView.ImageViewModel.MouseMagnifier.RectWidth, (int)imageView.ImageViewModel.MouseMagnifier.RectHeight);
                            
                            windowCIE.ChangeSelect(dx, dy);
                        }
                        else
                        {
                            windowCIE.ChangeSelect(e);
                        }
                    }

                    imageView.ImageViewModel.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
                    windowCIE.Closed += (s, e) =>
                    {
                        imageView.ImageViewModel.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                        imageView.ImageViewModel.MouseMagnifier.IsChecked = false;
                        windowCIE = null;
                    };
                }
                windowCIE.Show();
                windowCIE.Activate();
            }

            imageView.Button1931.Click += ButtonCIE1931_Click;             
        }

    }
}
