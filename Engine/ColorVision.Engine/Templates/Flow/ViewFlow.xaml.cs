using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates.DataLoad;
using ColorVision.Engine.Templates.Distortion;
using ColorVision.Engine.Templates.FocusPoints;
using ColorVision.Engine.Templates.FOV;
using ColorVision.Engine.Templates.Ghost;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.JND;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.LedCheck;
using ColorVision.Engine.Templates.LEDStripDetection;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.ROI;
using ColorVision.Engine.Templates.SFR;
using ColorVision.Engine.Templates.Validate;
using ColorVision.Engine.Templates;
using ColorVision.UI.Views;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Engine.Templates.Jsons;
using System.Collections.Generic;
using System.Windows.Media;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.Engine.Services.Flow
{
    public class FlowRecord:ViewModelBase
    {
        public FlowRecord(STNode sTNode)
        {
            Guid = sTNode.Guid;
            Name = sTNode.Title;
            DateTime date = DateTime.Now;
            DateTimeFlowRun = date;
            DateTimeRun = date;
            DateTimeStop = date;

        }

        public ContextMenu ContextMenu { get; set; }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;
        public Guid Guid { get; set; }
        public string Name { get => _Name; set { _Name =value; NotifyPropertyChanged(); } }
        private string _Name;
        public DateTime DateTimeFlowRun { get => _DateTimeFlowRun; set { _DateTimeFlowRun = value; NotifyPropertyChanged(); } }
        private DateTime _DateTimeFlowRun;

        public DateTime DateTimeRun { get => _DateTimeRun; set { _DateTimeRun = value; NotifyPropertyChanged(); } }
        private DateTime _DateTimeRun;

        public DateTime DateTimeStop { get => _DateTimeStop; set { _DateTimeStop = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(RunTime)); NotifyPropertyChanged(nameof(FlowTime)); } }
        private DateTime _DateTimeStop;

        public TimeSpan RunTime { get => _DateTimeStop - _DateTimeRun; }
        public TimeSpan FlowTime { get => _DateTimeStop - _DateTimeFlowRun; }
    }

    /// <summary>
    /// CVFlowView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewFlow : UserControl,IView, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public FlowEngineLib.FlowEngineControl FlowEngineControl { get; set; }
        public View View { get; set; }
        public ObservableCollection<FlowRecord> FlowRecords { get; set; } = new ObservableCollection<FlowRecord>();

        public ViewFlow()
        {
            FlowEngineControl = new FlowEngineLib.FlowEngineControl(false);
            InitializeComponent();
        }
        public FlowParam FlowParam { get; set; }

        public float CanvasScale { get => STNodeEditorMain.CanvasScale; set { STNodeEditorMain.ScaleCanvas(value, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2); NotifyPropertyChanged(); } }

        STNodeTreeView STNodeTreeView1 = new STNodeTreeView();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            listViewRecord.ItemsSource = FlowRecords;
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");

            STNodeEditorMain.ActiveChanged += (s, e) =>
            {
                STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);

                SignStackPannel.Children.Clear();

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Node.Camera.CommCameraNode commCaeraNode)
                {
                    AddStackPanel(name => commCaeraNode.DeviceCode = name, commCaeraNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
                    var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == commCaeraNode.DeviceCode);
                    AddStackPanel(name => commCaeraNode.CalibTempName = name, commCaeraNode.CalibTempName, "校正", reuslt?.PhyCamera?.CalibrationParams ?? new ObservableCollection<TemplateModel<Services.PhyCameras.Group.CalibrationParam>>());


                    // Usage
                    AddStackPanel(name => commCaeraNode.TempName = name, commCaeraNode.TempName, "曝光模板", new TemplateCameraExposureParam());
                    AddStackPanel(name => commCaeraNode.POITempName = name, commCaeraNode.POITempName, "POI模板", new TemplatePoi());
                    AddStackPanel(name => commCaeraNode.POIFilterTempName = name, commCaeraNode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                    AddStackPanel(name => commCaeraNode.POIReviseTempName = name, commCaeraNode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());
                }

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Node.Algorithm.AlgorithmKBNode kbnode)
                {
                    AddStackPanel(name => kbnode.TempName = name, kbnode.TempName, "KB", new TemplateKB());

                }


                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Algorithm.CalibrationNode calibrationNode)
                {
                    AddStackPanel(name => calibrationNode.DeviceCode = name, calibrationNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList());

                    var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList().Find(a => a.Code == calibrationNode.DeviceCode);
                    AddStackPanel(name => calibrationNode.TempName = name, calibrationNode.TempName, "校正", reuslt?.PhyCamera?.CalibrationParams ?? new ObservableCollection<TemplateModel<Services.PhyCameras.Group.CalibrationParam>>());
                }

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Algorithm.AlgorithmNode algorithmNode)
                {
                    void Refesh()
                    {
                        SignStackPannel.Children.Clear();

                        switch (algorithmNode.Algorithm)
                        {
                            case FlowEngineLib.Algorithm.AlgorithmType.MTF:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "MTF", new TemplateMTF());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.SFR:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "SFR", new TemplateSFR());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.FOV:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "FOV", new TemplateFOV());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.鬼影:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "鬼影", new TemplateGhost());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.畸变:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "畸变", new TemplateDistortionParam());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.灯珠检测1:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯珠检测1", new TemplateLedCheck());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.灯珠检测OLED:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯珠检测OLED", new TemplateMTF());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.灯带检测:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯带检测", new TemplateLEDStripDetection()); ;
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.发光区检测1:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "发光区检测1", new TemplateFocusPoints());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.发光区检测OLED:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "发光区检测OLED", new TemplateRoi());
                                break;
                            case FlowEngineLib.Algorithm.AlgorithmType.JND:
                                AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "JND", new TemplateJND());
                                break;
                            default:
                                break;
                        }
                    }
                    algorithmNode.nodeEvent -= (s, e) => Refesh();
                    algorithmNode.nodeEvent += (s, e) => Refesh();
                    Refesh();
                }


                if (STNodeEditorMain.ActiveNode is FlowEngineLib.CVCameraNode cvCameraNode)
                {
                    AddStackPanel(name => cvCameraNode.DeviceCode = name, cvCameraNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

                    var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == cvCameraNode.DeviceCode);
                    AddStackPanel(name => cvCameraNode.CalibTempName = name, cvCameraNode.CalibTempName, "校正", reuslt?.PhyCamera?.CalibrationParams ?? new ObservableCollection<TemplateModel<Services.PhyCameras.Group.CalibrationParam>>());

                    AddStackPanel(name => cvCameraNode.POITempName = name, cvCameraNode.POITempName, "POI模板", new TemplatePoi());
                    AddStackPanel(name => cvCameraNode.POIFilterTempName = name, cvCameraNode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                    AddStackPanel(name => cvCameraNode.POIReviseTempName = name, cvCameraNode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());

                }


                if (STNodeEditorMain.ActiveNode is FlowEngineLib.LVCameraNode lcCameranode)
                {
                    AddStackPanel(name => lcCameranode.DeviceCode = name, lcCameranode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
                    var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == lcCameranode.DeviceCode);
                    AddStackPanel(name => lcCameranode.CaliTempName = name, lcCameranode.CaliTempName, "校正", reuslt?.PhyCamera?.CalibrationParams ?? new ObservableCollection<TemplateModel<Services.PhyCameras.Group.CalibrationParam>>());

                    AddStackPanel(name => lcCameranode.POITempName = name, lcCameranode.POITempName, "POI模板", new TemplatePoi());
                    AddStackPanel(name => lcCameranode.POIFilterTempName = name, lcCameranode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                    AddStackPanel(name => lcCameranode.POIReviseTempName = name, lcCameranode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());

                }

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.BuildPOINode buidpoi)
                {
                    AddStackPanel(name => buidpoi.TemplateName = name, buidpoi.TemplateName, "POI模板", new TemplateBuildPoi());
                }

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Node.Algorithm.AlgDataLoadNode algDataLoadNode)
                {
                    AddStackPanel(name => algDataLoadNode.TempName = name, algDataLoadNode.TempName, "模板", new TemplateDataLoad());
                }
                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Node.OLED.OLEDImageCroppingNode OLEDImageCroppingNode)
                {
                    AddStackPanel(name => OLEDImageCroppingNode.TempName = name, OLEDImageCroppingNode.TempName, "参数模板", new TemplateImageCropping());
                }
                if (STNodeEditorMain.ActiveNode is FlowEngineLib.POINode poinode)
                {
                    AddStackPanel(name => poinode.TemplateName = name, poinode.TemplateName, "POI模板", new TemplatePoi());
                    AddStackPanel(name => poinode.FilterTemplateName = name, poinode.FilterTemplateName, "POI过滤", new TemplatePoiFilterParam());
                    AddStackPanel(name => poinode.ReviseTemplateName = name, poinode.ReviseTemplateName, "POI修正", new TemplatePoiReviseParam());
                    AddStackPanel(name => poinode.OutputTemplateName = name, poinode.OutputTemplateName, "文件输出模板", new TemplatePoiOutputParam());
                }

                if (STNodeEditorMain.ActiveNode is FlowEngineLib.CommonSensorNode commonsendorNode)
                {
                    AddStackPanel(name => commonsendorNode.TempName = name, commonsendorNode.TempName, "模板名称", TemplateSensor.AllParams);
                }
                if (STNodeEditorMain.ActiveNode is FlowEngineLib.Node.Algorithm.AlgComplianceMathNode algComplianceMathNode)
                {
                    void Refesh()
                    {
                        SignStackPannel.Children.Clear();
                        switch (algComplianceMathNode.ComplianceMath)
                        {
                            case FlowEngineLib.Node.Algorithm.ComplianceMathType.CIE:
                                AddStackPanel(name => algComplianceMathNode.TempName = name, algComplianceMathNode.TempName, "CIE", new ObservableCollection<TemplateModel<ValidateParam>>(TemplateComplyParam.CIEParams.SelectMany(p => p.Value)));
                                break;
                            case FlowEngineLib.Node.Algorithm.ComplianceMathType.JND:
                                AddStackPanel(name => algComplianceMathNode.TempName = name, algComplianceMathNode.TempName, "JND", new TemplateComplyParam("Comply.JND"));
                                break;
                            default:
                                break;
                        }
                    }
                    algComplianceMathNode.nodeEvent -= (s, e) => Refesh();
                    algComplianceMathNode.nodeEvent += (s, e) => Refesh();
                    Refesh();
                }

            };


            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                {
                    if (STNodeEditorMain.ActiveNode != null)
                        STNodeEditorMain.Nodes.Remove(STNodeEditorMain.ActiveNode);

                    foreach (var item in STNodeEditorMain.GetSelectedNode())
                    {
                        STNodeEditorMain.Nodes.Remove(item);
                    }
                }
            };

            FlowEngineControl.AttachNodeEditor(STNodeEditorMain);

            View = new View();
            ViewGridManager.GetInstance().AddView(0, this);

            View.ViewIndexChangedEvent += (s, e) =>
            {
                if (e == -2)
                {
                    STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    STNodeEditorMain.ContextMenuStrip.Items.Add("还原到主窗口中", null, (s, e1) =>
                    {

                        if (ViewGridManager.GetInstance().IsGridEmpty(View.PreViewIndex))
                        {
                            View.ViewIndex = View.PreViewIndex;
                        }
                        else
                        {
                            View.ViewIndex = -1;
                        }
                    }

                    );
                }
            };
            AddContentMenu();
        }


        public void AddContentMenu()
        {
            STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            Type STNodeTreeViewtype = STNodeTreeView1.GetType();

            // 获取私有字段信息
            FieldInfo fieldInfo = STNodeTreeViewtype.GetField("m_dic_all_type", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                // 获取字段的值
                var value = fieldInfo.GetValue(STNodeTreeView1);
                Dictionary<string, List<Type>> values = new Dictionary<string, List<Type>>();
                if (value is Dictionary<Type, string> m_dic_all_type)
                {
                    foreach (var item in m_dic_all_type)
                    {
                        if (values.ContainsKey(item.Value))
                        {
                            values[item.Value].Add(item.Key);
                        }
                        else
                        {
                            values.Add(item.Value, new List<Type>() { item.Key });
                        }
                    }

                    foreach (var nodetype in values)
                    {
                        string header = nodetype.Key.Replace("FlowEngineLib/", "");
                        var toolStripItem = new System.Windows.Forms.ToolStripMenuItem(header);


                        foreach (var type in nodetype.Value)
                        {
                            if (type.IsSubclassOf(typeof(STNode)))
                            {
                                if (Activator.CreateInstance(type) is STNode sTNode)
                                {
                                    toolStripItem.DropDownItems.Add(sTNode.Title, null, (s, e) =>
                                    {
                                        STNode sTNode1 = (STNode)Activator.CreateInstance(type);
                                        if (sTNode1 != null)
                                        {
                                            sTNode1.Create();
                                            var p = STNodeEditorMain.PointToClient(lastMousePosition);
                                            p = STNodeEditorMain.ControlToCanvas(p);
                                            sTNode1.Left = p.X;
                                            sTNode1.Top = p.Y;
                                            STNodeEditorMain.Nodes.Add(sTNode1);
                                        }
                                    });
                                }
                            }

                        }
                        STNodeEditorMain.ContextMenuStrip.Items.Add(toolStripItem);

                    }

                }
            }


            STNodeEditorMain.ContextMenuStrip.Opening += (s, e) =>
            {
                if (IsOptionDisConnected) e.Cancel = true;
                if (IsHover())
                    e.Cancel = true;
                IsOptionDisConnected = false;
            };
            STNodeEditorMain.OptionDisConnected += (s, e) =>
            {
                IsOptionDisConnected = true;
            };
        }
        bool IsOptionDisConnected;
        public bool IsHover()
        {
            lastMousePosition = System.Windows.Forms.Cursor.Position;
            var p = STNodeEditorMain.PointToClient(System.Windows.Forms.Cursor.Position);
            var info = STNodeEditorMain.FindNodeFromPoint(p);
            if (info.Node !=null || info.NodeOption != null)
            {
                return true;
            }
            return false;
        }

        public void AutoSize()
        {
            // Calculate the centers
            var boundsCenterX = STNodeEditorMain.Bounds.Width / 2;
            var boundsCenterY = STNodeEditorMain.Bounds.Height / 2;

            // Calculate the scale factor to fit CanvasValidBounds within Bounds
            var scaleX = (float)STNodeEditorMain.Bounds.Width / (float)STNodeEditorMain.CanvasValidBounds.Width;
            var scaleY = (float)STNodeEditorMain.Bounds.Height / (float)STNodeEditorMain.CanvasValidBounds.Height;
            CanvasScale = Math.Min(scaleX, scaleY);
            CanvasScale = CanvasScale>1?1:CanvasScale;
            // Apply the scale
            STNodeEditorMain.ScaleCanvas(CanvasScale, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);

            var validBoundsCenterX = STNodeEditorMain.CanvasValidBounds.Width / 2;
            var validBoundsCenterY = STNodeEditorMain.CanvasValidBounds.Height / 2;

            // Calculate the offsets to move CanvasValidBounds to the center of Bounds
            var offsetX = boundsCenterX - validBoundsCenterX * CanvasScale - 50 * CanvasScale;
            var offsetY = boundsCenterY - validBoundsCenterY * CanvasScale - 50 * CanvasScale;


            // Move the canvas
            STNodeEditorMain.MoveCanvas(offsetX, STNodeEditorMain.CanvasOffset.Y, bAnimation: true, CanvasMoveArgs.Left);
            STNodeEditorMain.MoveCanvas(offsetX, offsetY, bAnimation: true, CanvasMoveArgs.Top);
        }



        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth > 200)
            {
                winf1.Height = (int)ActualHeight;
                winf1.Width = (int)ActualWidth;
            }
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX + 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX - 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY + 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY - 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Add)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
        }

        private bool IsMouseDown;
        private System.Drawing.Point lastMousePosition;
        private void STNodeEditorMain_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            lastMousePosition = e.Location;
            System.Drawing.PointF m_pt_down_in_canvas = new System.Drawing.PointF();
            m_pt_down_in_canvas.X = ((float)e.X - STNodeEditorMain.CanvasOffsetX) / STNodeEditorMain.CanvasScale;
            m_pt_down_in_canvas.Y = ((float)e.Y - STNodeEditorMain.CanvasOffsetY) / STNodeEditorMain.CanvasScale;
            NodeFindInfo nodeFindInfo = STNodeEditorMain.FindNodeFromPoint(m_pt_down_in_canvas);

            if (!string.IsNullOrEmpty(nodeFindInfo.Mark))
            {

            }
            else if (nodeFindInfo.Node != null)
            {

            }
            else if (nodeFindInfo.NodeOption != null)
            {

            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                IsMouseDown = true;
            }
        }

        private void STNodeEditorMain_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            IsMouseDown = false;
        }

        private void STNodeEditorMain_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (IsMouseDown)
            {        // 计算鼠标移动的距离
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                // 更新画布偏移
                STNodeEditorMain.MoveCanvas(
                    STNodeEditorMain.CanvasOffsetX + deltaX,
                    STNodeEditorMain.CanvasOffsetY + deltaY,
                    bAnimation: false,
                    CanvasMoveArgs.All
                );

                // 更新最后的鼠标位置
                lastMousePosition = e.Location;
            }
        }


        private void STNodeEditorMain_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var mousePosition = STNodeEditorMain.PointToClient(e.Location);

            if (e.Delta < 0)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.05f, mousePosition.X, mousePosition.Y);
            }
            else
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.05f, mousePosition.X, mousePosition.Y);
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, List<T> itemSource) where T : DeviceService
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName });

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                DisplayMemberPath = "Code",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = itemSource;
            var selectedItem = itemSource.FirstOrDefault(x => x.Code == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = itemSource.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Code;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };

            dockPanel.Children.Add(comboBox);
            SignStackPannel.Children.Add(dockPanel);
        }


        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName });

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = itemSource;
            var selectedItem = itemSource.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = itemSource.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };

            dockPanel.Children.Add(comboBox);
            SignStackPannel.Children.Add(dockPanel);
        }


        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 50 });
            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small"),
                Width = 120
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = template.TemplateParams;
            var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = template.TemplateParams.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };


            Grid grid = new Grid
            {
                Width = 20,
                Margin = new Thickness(5, 0, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            // 创建 TextBlock
            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            // 创建 Button
            Button button = new Button
            {
                Width = 20,
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            button.Click += (s, e) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            };

            // 将控件添加到 Grid
            grid.Children.Add(textBlock);
            grid.Children.Add(button);


            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(grid);
            SignStackPannel.Children.Add(dockPanel);
        }

        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 50 });
            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small"),
                Width = 120
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = template.TemplateParams;
            var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = template.TemplateParams.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };


            Grid grid = new Grid
            {
                Width = 20,
                Margin = new Thickness(5, 0, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            // 创建 TextBlock
            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            // 创建 Button
            Button button = new Button
            {
                Width = 20,
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            button.Click += (s, e) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            };

            // 将控件添加到 Grid
            grid.Children.Add(textBlock);
            grid.Children.Add(button);


            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(grid);
            SignStackPannel.Children.Add(dockPanel);
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            FlowParam.DataBase64 = Convert.ToBase64String(STNodeEditorMain.GetCanvasData()); 
            FlowParam.Save();
            MessageBox.Show("保存成功");
        }
        public event EventHandler Refresh;
        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            Refresh?.Invoke(this, new EventArgs());
        }
    }
}
