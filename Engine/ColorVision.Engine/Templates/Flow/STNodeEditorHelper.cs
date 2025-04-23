#pragma warning disable CS8603,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.DataLoad;
using ColorVision.Engine.Templates.Distortion;
using ColorVision.Engine.Templates.FocusPoints;
using ColorVision.Engine.Templates.Ghost;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.JND;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.DFOV;
using ColorVision.Engine.Templates.Jsons.GhostQK;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.Jsons.SFRFindROI;
using ColorVision.Engine.Templates.LedCheck;
using ColorVision.Engine.Templates.LEDStripDetection;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Engine.Templates.ROI;
using ColorVision.Engine.Templates.SFR;
using ColorVision.Engine.Templates.Validate;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow
{

    public class STNodeEditorHelper:ViewModelBase
    {
        public STNodeEditor STNodeEditor { get; set; }

        public STNodePropertyGrid STNodePropertyGrid1 { get; set; }
        public StackPanel SignStackPanel { get; set; }

        public STNodeTreeView STNodeTreeView1 { get; set; }

        public STNodeEditorHelper(Control Paraent,STNodeEditor sTNodeEditor, STNodeTreeView sTNodeTreeView1, STNodePropertyGrid sTNodePropertyGrid, StackPanel stackPanel)
        {
            STNodeEditor = sTNodeEditor;
            STNodeTreeView1 = sTNodeTreeView1;
            STNodePropertyGrid1 = sTNodePropertyGrid;
            SignStackPanel = stackPanel;
            STNodeEditor.NodeAdded += StNodeEditor1_NodeAdded;
            STNodeEditor.ActiveChanged += STNodeEditorMain_ActiveChanged;
            STNodePropertyGrid1.SetInfoKey("Xincheng", "1791746286@qq.com", "https://xincheng213618.com/", "Xincheng");

            AddContentMenu();

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => 
            {
                foreach (var item in STNodeEditor.GetSelectedNode())
                    STNodeEditor.Nodes.Remove(item);
            } , (s, e) => { e.CanExecute = sTNodeEditor.GetSelectedNode().Length > 0; }));


            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => sTNodeEditor.Nodes.Clear(), (s, e) => { e.CanExecute = true; }));

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) => Copy(), (s, e) => { e.CanExecute = true; }));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (s, e) => Paste(), (s, e) => { e.CanExecute = CopyNodes.Count >0;}));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => sTNodeEditor.Nodes.Clear(), (s, e) => { e.CanExecute = true; }));
        }

        private List<STNode> CopyNodes = new List<STNode>();

        public void Copy()
        {
            CopyNodes.Clear();
            foreach (var item in STNodeEditor.GetSelectedNode())
            {
                CopyNodes.Add(item);
            }
        }

        public void Paste()
        {
            int offset = 10;

            foreach (var item in CopyNodes)
            {
                Type type = item.GetType();

                STNode sTNode1 = (STNode)Activator.CreateInstance(type);
                if (sTNode1 != null)
                {
                    sTNode1.Create();
                    PropertyInfo[] properties = type.GetProperties();
                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanRead && property.CanWrite)
                        {
                            object value = property.GetValue(item);
                            property.SetValue(sTNode1, value);
                        }
                    }
                    sTNode1.Left = item.Left + offset;
                    sTNode1.Top = item.Top + offset;
                    sTNode1.IsSelected = true;
                    STNodeEditor.Nodes.Add(sTNode1);
                    if (CopyNodes.Count == 1)
                    {
                        item.IsSelected = false;
                        STNodeEditor.RemoveSelectedNode(item);
                        STNodeEditor.AddSelectedNode(sTNode1);
                        STNodeEditor.SetActiveNode(sTNode1);
                    }
                    else
                    {
                        STNodeEditor.RemoveSelectedNode(item);
                        STNodeEditor.AddSelectedNode(sTNode1);
                    }
                }
            }

            CopyNodes.Clear();
            foreach (var item in STNodeEditor.GetSelectedNode())
            {
                CopyNodes.Add(item);
            }



        }



        #region Activate
        private void STNodeEditorMain_ActiveChanged(object? sender, EventArgs e)
        {

            STNodePropertyGrid1.SetNode(STNodeEditor.ActiveNode);
            SignStackPanel.Children.Clear();

            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.PG.PGNode pgnode)
            {
                AddStackPanel(name => pgnode.DeviceCode = name, pgnode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSensor>().ToList());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.POI.RealPOINode realPOINode)
            {
                AddStackPanel(name => realPOINode.FilterTemplateName = name, realPOINode.FilterTemplateName, "POI过滤", new TemplatePoiFilterParam());
                AddStackPanel(name => realPOINode.ReviseTemplateName = name, realPOINode.OutputTemplateName, "POI修正", new TemplatePoiReviseParam());
                AddStackPanel(name => realPOINode.OutputTemplateName = name, realPOINode.OutputTemplateName, "文件输出模板", new TemplatePoiOutputParam());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.SMUModelNode sMUModelNode)
            {
                AddStackPanel(name => sMUModelNode.DeviceCode = name, sMUModelNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList());
                AddStackPanel(name => sMUModelNode.ModelName = name, sMUModelNode.ModelName, "SMUParam设置", new TemplateSMUParam());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.SMUNode sMUNode)
            {
                AddStackPanel(name => sMUNode.DeviceCode = name, sMUNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList());
                AddStackPanel(name => sMUNode.NodeName = name, sMUNode.NodeName, "SMUParam设置", new TemplateSMUParam());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Spectrum.SpectrumNode spectrumNode)
            {
                AddStackPanel(name => spectrumNode.DeviceCode = name, spectrumNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Spectum.SpectrumLoopNode spectrumLoopNode)
            {
                AddStackPanel(name => spectrumLoopNode.DeviceCode = name, spectrumLoopNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.CamMotorNode camMotorNode)
            {
                AddStackPanel(name => camMotorNode.DeviceCode = name, camMotorNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
                AddStackPanel(name => camMotorNode.AutoFocusTemp = name, camMotorNode.AutoFocusTemp, "相机模板", new TemplateAutoFocus());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Camera.CommCameraNode commCaeraNode)
            {
                AddStackPanel(name => commCaeraNode.DeviceCode = name, commCaeraNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
                var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == commCaeraNode.DeviceCode);
                if (reuslt?.PhyCamera!=null)
                    AddStackPanel(name => commCaeraNode.CalibTempName = name, commCaeraNode.CalibTempName, "校正", new TemplateCalibrationParam(reuslt.PhyCamera));
                AddStackPanel(name => commCaeraNode.CamTempName = name, commCaeraNode.CamTempName, "相机模板", new TemplateCameraExposure());
                AddStackPanel(name => commCaeraNode.TempName = name, commCaeraNode.TempName, "曝光模板", new TemplateAutoExpTime());

                // Usage
                AddStackPanel(name => commCaeraNode.POITempName = name, commCaeraNode.POITempName, "POI模板", new TemplatePoi());
                AddStackPanel(name => commCaeraNode.POIFilterTempName = name, commCaeraNode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                AddStackPanel(name => commCaeraNode.POIReviseTempName = name, commCaeraNode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Algorithm.AlgorithmGhostNode algorithmGhostNode)
            {
                AddStackPanel(name => algorithmGhostNode.TempName = name, algorithmGhostNode.TempName, "Ghost", new TemplateGhostQK());
            }


            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode algorithmBlackMuraNode)
            {
                AddStackPanel(name => algorithmBlackMuraNode.DeviceCode = name, algorithmBlackMuraNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                AddStackPanel(name => algorithmBlackMuraNode.TempName = name, algorithmBlackMuraNode.TempName, "BlackMura", new TemplateBlackMura());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Algorithm.AlgorithmKBNode kbnode)
            {
                AddStackPanel(name => kbnode.DeviceCode = name, kbnode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());

                AddStackPanelKB(name => kbnode.TempName = name, kbnode.TempName, "KB", new TemplateKB());

            }


            if (STNodeEditor.ActiveNode is FlowEngineLib.Algorithm.CalibrationNode calibrationNode)
            {
                AddStackPanel(name => calibrationNode.DeviceCode = name, calibrationNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList());

                var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList().Find(a => a.Code == calibrationNode.DeviceCode);

                if (reuslt?.PhyCamera != null)
                    AddStackPanel(name => calibrationNode.TempName = name, calibrationNode.TempName, "校正", new TemplateCalibrationParam(reuslt.PhyCamera));
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Algorithm.AlgorithmNode algorithmNode)
            {
                void Refesh()
                {
                    SignStackPanel.Children.Clear();
                    AddStackPanel(name => algorithmNode.DeviceCode = name, algorithmNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());

                    switch (algorithmNode.Algorithm)
                    {
                        case FlowEngineLib.Algorithm.AlgorithmType.MTF:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "MTF", new TemplateMTF());
                            AddStackPanel(name => algorithmNode.POITempName = name, algorithmNode.POITempName, "POI", new TemplatePoi());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.SFR:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "SFR", new TemplateSFR());
                            AddStackPanel(name => algorithmNode.POITempName = name, algorithmNode.POITempName, "POI", new TemplatePoi());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.FOV:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "FOV", new TemplateDFOV());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.鬼影:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "鬼影", new TemplateGhost());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.畸变:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "畸变", new TemplateDistortionParam());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.灯珠检测:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯珠检测1", new TemplateLedCheck());
                            break;
                        //case FlowEngineLib.Algorithm.AlgorithmType.灯珠检测OLED:
                        //    AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯珠检测OLED", new TemplateMTF());
                        //    break;
                        case FlowEngineLib.Algorithm.AlgorithmType.灯带检测:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "灯带检测", new TemplateLEDStripDetection()); ;
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.发光区检测:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "发光区检测1", new TemplateFocusPoints());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.发光区检测OLED:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "发光区检测OLED", new TemplateRoi());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.JND:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "JND", new TemplateJND());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.SFR_FindROI:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "SFR_FindROI", new TemplateSFRFindROI());
                            AddStackPanel(name => algorithmNode.POITempName = name, algorithmNode.POITempName, "POI", new TemplatePoi());
                            break;
                        case FlowEngineLib.Algorithm.AlgorithmType.双目融合:
                            AddStackPanel(name => algorithmNode.TempName = name, algorithmNode.TempName, "双目融合", new TemplateBinocularFusion());
                            break;
                        default:
                            break;
                    }

                }
                algorithmNode.nodeEvent -= (s, e) => Refesh();
                algorithmNode.nodeEvent += (s, e) => Refesh();
                Refesh();
            }


            if (STNodeEditor.ActiveNode is FlowEngineLib.CVCameraNode cvCameraNode)
            {
                AddStackPanel(name => cvCameraNode.DeviceCode = name, cvCameraNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

                var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == cvCameraNode.DeviceCode);
                if (reuslt?.PhyCamera != null)
                    AddStackPanel(name => cvCameraNode.CalibTempName = name, cvCameraNode.CalibTempName, "校正", new TemplateCalibrationParam(reuslt.PhyCamera));

                AddStackPanel(name => cvCameraNode.POITempName = name, cvCameraNode.POITempName, "POI模板", new TemplatePoi());
                AddStackPanel(name => cvCameraNode.POIFilterTempName = name, cvCameraNode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                AddStackPanel(name => cvCameraNode.POIReviseTempName = name, cvCameraNode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());

            }


            if (STNodeEditor.ActiveNode is FlowEngineLib.LVCameraNode lcCameranode)
            {
                AddStackPanel(name => lcCameranode.DeviceCode = name, lcCameranode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
                var reuslt = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == lcCameranode.DeviceCode);
                if (reuslt?.PhyCamera != null)
                    AddStackPanel(name => lcCameranode.CaliTempName = name, lcCameranode.CaliTempName, "校正", new TemplateCalibrationParam(reuslt.PhyCamera));

                AddStackPanel(name => lcCameranode.POITempName = name, lcCameranode.POITempName, "POI模板", new TemplatePoi());
                AddStackPanel(name => lcCameranode.POIFilterTempName = name, lcCameranode.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
                AddStackPanel(name => lcCameranode.POIReviseTempName = name, lcCameranode.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());

            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.BuildPOINode buidpoi)
            {
                AddStackPanel(name => buidpoi.DeviceCode = name, buidpoi.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                AddStackPanel(name => buidpoi.TemplateName = name, buidpoi.TemplateName, "POI模板", new TemplateBuildPoi());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Algorithm.AlgDataLoadNode algDataLoadNode)
            {
                AddStackPanel(name => algDataLoadNode.DeviceCode = name, algDataLoadNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());

                AddStackPanel(name => algDataLoadNode.TempName = name, algDataLoadNode.TempName, "模板", new TemplateDataLoad());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.OLED.OLEDImageCroppingNode OLEDImageCroppingNode)
            {
                AddStackPanel(name => OLEDImageCroppingNode.DeviceCode = name, OLEDImageCroppingNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                AddStackPanel(name => OLEDImageCroppingNode.TempName = name, OLEDImageCroppingNode.TempName, "参数模板", new TemplateImageCropping());
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.POINode poinode)
            {
                AddStackPanel(name => poinode.DeviceCode = name, poinode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                AddStackPanel(name => poinode.TemplateName = name, poinode.TemplateName, "POI模板", new TemplatePoi());
                AddStackPanel(name => poinode.FilterTemplateName = name, poinode.FilterTemplateName, "POI过滤", new TemplatePoiFilterParam());
                AddStackPanel(name => poinode.ReviseTemplateName = name, poinode.ReviseTemplateName, "POI修正", new TemplatePoiReviseParam());
                AddStackPanel(name => poinode.OutputTemplateName = name, poinode.OutputTemplateName, "文件输出模板", new TemplatePoiOutputParam());
            }

            if (STNodeEditor.ActiveNode is FlowEngineLib.CommonSensorNode commonsendorNode)
            {
                AddStackPanel(name => commonsendorNode.DeviceCode = name, commonsendorNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSensor>().ToList());
                AddStackPanel(name => commonsendorNode.TempName = name, commonsendorNode.TempName, "模板名称", TemplateSensor.AllParams);
            }
            if (STNodeEditor.ActiveNode is FlowEngineLib.Node.Algorithm.AlgComplianceMathNode algComplianceMathNode)
            {
                void Refesh()
                {
                    SignStackPanel.Children.Clear();
                    AddStackPanel(name => algComplianceMathNode.DeviceCode = name, algComplianceMathNode.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
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
        }

        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, List<T> itemSource) where T : DeviceService
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName ,Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

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

            Grid myGrid = new Grid();
            myGrid.DataContext = selectedItem;

            // Create a Button
            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Code;
                    myGrid.DataContext = templateModel;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };

            // Create a ToggleButton
            var toggleButton = new ToggleButton
            {
                Style = (Style)Application.Current.FindResource("ButtonMQTTConnect"),
                Height = 10,
                Width = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsEnabled = false
            };
            // Create the binding for IsChecked
            var binding = new Binding("Config.IsAlive")
            {
                Mode = BindingMode.OneWay
            };
            // Set the binding to the ToggleButton
            toggleButton.SetBinding(ToggleButton.IsCheckedProperty, binding);
            // Create an Image
            var image = new Image
            {
                Source = (ImageSource)Application.Current.FindResource("DrawingImageProperty"),
                Height = 18,
                Margin = new Thickness(0)
            };



            // Create the binding for IsChecked
            var binding1 = new Binding("PropertyCommand")
            {
                Mode = BindingMode.OneWay
            };
            // Set the binding to the ToggleButton
            button.SetBinding(Button.CommandProperty, binding1);

            // Add elements to the Grid
            myGrid.Children.Add(toggleButton);
            myGrid.Children.Add(image);
            myGrid.Children.Add(button);

            // Optionally, set the DockPanel.Dock property if needed
            DockPanel.SetDock(myGrid, Dock.Right);

            dockPanel.Children.Add(myGrid);
            dockPanel.Children.Add(comboBox);
            SignStackPanel.Children.Add(dockPanel);
        }


        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 50, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

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
            SignStackPanel.Children.Add(dockPanel);
        }

        void AddStackPanelKB(Action<string> updateStorageAction, string tempName, string signName, TemplateKB template)
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 30, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });
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

                if (comboBox.SelectedValue is TemplateJsonKBParam templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                STNodePropertyGrid1.Refresh();
            };


            Grid grid = new Grid
            {
                Width = 20,
                Margin = new Thickness(0, 0, 0, 0),
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

            // Create a new Grid
            Grid grid1 = new Grid
            {
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Create an Image
            Image image = new Image
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageEdit"], // Assuming the resource is defined
                Width = 12,
                Margin = new Thickness(0)
            };

            // Add the Image to the Grid
            grid1.Children.Add(image);

            // Create a Button
            Button buttonEdit = new Button
            {
                Name = "ButtonEdit",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            buttonEdit.Click += (s, e) =>
            {

                if (comboBox.SelectedIndex >= 0)
                {
                    new EditPoiParam1(TemplateKB.Params[comboBox.SelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                }
            };

            // Add the Button to the Grid
            grid1.Children.Add(buttonEdit);
            dockPanel.Children.Add(grid1);
            SignStackPanel.Children.Add(dockPanel);
        }


        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 50  ,Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });
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
            SignStackPanel.Children.Add(dockPanel);
        }

        void AddStackPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 50, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });
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
            SignStackPanel.Children.Add(dockPanel);
        }

        #endregion

        #region ContextMenu

        public void AddNodeContext()
        {
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode node)
                {
                    node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    node.ContextMenuStrip.Items.Add("复制", null, (s, e1) => CopySTNode(node));
                    node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor.Nodes.Remove(node));
                    node.ContextMenuStrip.Items.Add("LockOption", null, (s, e1) => STNodeEditor.ActiveNode.LockOption = !STNodeEditor.ActiveNode.LockOption);
                    node.ContextMenuStrip.Items.Add("LockLocation", null, (s, e1) => STNodeEditor.ActiveNode.LockLocation = !STNodeEditor.ActiveNode.LockLocation);
                }
            }
        }


        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = e.Node;
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor.Nodes.Remove(node));
            node.ContextMenuStrip.Items.Add("复制", null, (s, e1) => CopySTNode(node));
            node.ContextMenuStrip.Items.Add("LockOption", null, (s, e1) => STNodeEditor.ActiveNode.LockOption = !STNodeEditor.ActiveNode.LockOption);
            node.ContextMenuStrip.Items.Add("LockLocation", null, (s, e1) => STNodeEditor.ActiveNode.LockLocation = !STNodeEditor.ActiveNode.LockLocation);
        }

        public void CopySTNode(STNode sTNode)
        {
            Type type = sTNode.GetType();

            STNode sTNode1 = (STNode)Activator.CreateInstance(type);
            if (sTNode1 != null)
            {
                sTNode1.Create();
                PropertyInfo[] properties = type.GetProperties();
                foreach (PropertyInfo property in properties)
                {
                    if (property.CanRead && property.CanWrite)
                    {
                        object value = property.GetValue(sTNode);
                        property.SetValue(sTNode1, value);
                    }
                }
                sTNode1.Left = sTNode.Left;
                sTNode1.Top = sTNode.Top;

                STNodeEditor.Nodes.Add(sTNode1);
            }
        }

        public void AddContentMenu()
        {
            STNodeEditor.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
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
                        if (values.TryGetValue(item.Value, out List<Type>? value1))
                        {
                            value1.Add(item.Key);
                        }
                        else
                        {
                            values.Add(item.Value, new List<Type>() { item.Key });
                        }
                    }

                    foreach (var nodetype in values.OrderBy(x => x.Key, Comparer<string>.Create((x, y) => Common.NativeMethods.Shlwapi.CompareLogical(x, y))))
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
                                            var p = STNodeEditor.PointToClient(lastMousePosition);
                                            p = STNodeEditor.ControlToCanvas(p);
                                            sTNode1.Left = p.X;
                                            sTNode1.Top = p.Y;
                                            STNodeEditor.Nodes.Add(sTNode1);
                                        }
                                    });
                                }
                            }

                        }
                        STNodeEditor.ContextMenuStrip.Items.Add(toolStripItem);

                    }

                }
            }


            STNodeEditor.ContextMenuStrip.Opening += (s, e) =>
            {
                if (IsOptionDisConnected) e.Cancel = true;
                if (IsHover())
                    e.Cancel = true;
                IsOptionDisConnected = false;
            };
            STNodeEditor.OptionDisConnected += (s, e) =>
            {
                IsOptionDisConnected = true;
            };
        }
        bool IsOptionDisConnected;


        private System.Drawing.Point lastMousePosition;

        public bool IsHover()
        {
            lastMousePosition = System.Windows.Forms.Cursor.Position;
            var p = STNodeEditor.PointToClient(System.Windows.Forms.Cursor.Position);
            p = STNodeEditor.ControlToCanvas(p);

            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode sTNode)
                {
                    bool result = sTNode.Rectangle.Contains(p);
                    if (result)
                        return true;

                    if (sTNode.GetInputOptions() is STNodeOption[] inputOptions)
                    {
                        foreach (STNodeOption inputOption in inputOptions)
                        {
                            if (inputOption != STNodeOption.Empty && inputOption.DotRectangle.Contains(p))
                            {
                                return true;
                            }
                        }
                    }

                    if (sTNode.GetOutputOptions() is STNodeOption[] outputOptions)
                    {
                        foreach (STNodeOption outputOption in outputOptions)
                        {
                            if (outputOption != STNodeOption.Empty && outputOption.DotRectangle.Contains(p))
                            {
                                return true;
                            }
                        }

                    }
                }
            }
            return false;
        }

        #endregion

        #region AutoLayout
        public ConnectionInfo[] ConnectionInfo { get; set; }
        public float CanvasScale { get => STNodeEditor.CanvasScale; set { STNodeEditor.ScaleCanvas(value, STNodeEditor.CanvasValidBounds.X + STNodeEditor.CanvasValidBounds.Width / 2, STNodeEditor.CanvasValidBounds.Y + STNodeEditor.CanvasValidBounds.Height / 2); NotifyPropertyChanged(); } }
        public void AutoSize()
        {
            // Calculate the centers
            var boundsCenterX = STNodeEditor.Bounds.Width / 2;
            var boundsCenterY = STNodeEditor.Bounds.Height / 2;

            // Calculate the scale factor to fit CanvasValidBounds within Bounds
            var scaleX = (float)STNodeEditor.Bounds.Width / (float)STNodeEditor.CanvasValidBounds.Width;
            var scaleY = (float)STNodeEditor.Bounds.Height / (float)STNodeEditor.CanvasValidBounds.Height;
            CanvasScale = Math.Min(scaleX, scaleY);
            CanvasScale = CanvasScale > 1 ? 1 : CanvasScale;
            // Apply the scale
            STNodeEditor.ScaleCanvas(CanvasScale, STNodeEditor.CanvasValidBounds.X + STNodeEditor.CanvasValidBounds.Width / 2, STNodeEditor.CanvasValidBounds.Y + STNodeEditor.CanvasValidBounds.Height / 2);

            var validBoundsCenterX = STNodeEditor.CanvasValidBounds.Width / 2;
            var validBoundsCenterY = STNodeEditor.CanvasValidBounds.Height / 2;

            // Calculate the offsets to move CanvasValidBounds to the center of Bounds
            var offsetX = boundsCenterX - validBoundsCenterX * CanvasScale - 50 * CanvasScale;
            var offsetY = boundsCenterY - validBoundsCenterY * CanvasScale - 50 * CanvasScale;


            // Move the canvas
            STNodeEditor.MoveCanvas(offsetX, STNodeEditor.CanvasOffset.Y, bAnimation: true, CanvasMoveArgs.Left);
            STNodeEditor.MoveCanvas(offsetX, offsetY, bAnimation: true, CanvasMoveArgs.Top);
        }

        public void ApplyTreeLayout(int startX, int startY, int horizontalSpacing, int verticalSpacing)
        {
            ConnectionInfo = STNodeEditor.GetConnectionInfo();
            STNode rootNode = GetRootNode();
            if (rootNode == null) return;
            int currentY = startY;
            HashSet<STNode> MoreParens = new HashSet<STNode>();

            void LayoutNode(STNode node, int current)
            {
                int depeth = GetMaxDepth(node);
                // 设置当前节点的位置
                node.Left = startX + depeth * horizontalSpacing;
                node.Top = current;

                var parent = GetParent(node);
                // 递归布局子节点
                var children = GetChildren(node);

                foreach (var child in children)
                {
                    if (GetParent(child).Count > 1)
                    {
                        MoreParens.Add(child);
                    }
                    else
                    {
                        LayoutNode(child, currentY);
                        var childrenWithout1 = GetChildrenWithout(node);
                        if (childrenWithout1.Count > 1)
                        {
                            currentY += verticalSpacing;
                        }
                    }
                }
                var childrenWithout = GetChildrenWithout(node);
                if (childrenWithout.Count > 1)
                {
                    currentY = childrenWithout.Last().Top;
                }

                // 调整父节点位置到子节点的中心
                if (childrenWithout.Count != 0)
                {
                    int firstChildY = childrenWithout.First().Top;
                    int lastChildY = childrenWithout.Last().Top;
                    node.Top = (firstChildY + lastChildY) / 2;
                }

                if (parent.Count > 1)
                {
                    int firstChildY = parent.First().Top;
                    int lastChildY = parent.Last().Top;
                    node.Top = (firstChildY + lastChildY) / 2;
                }
            }

            void MoreParentsLayoutNode(STNode node)
            {
                node.Left = startX + GetMaxDepth(node) * horizontalSpacing;
                var parent = GetParent(node);
                // 递归布局子节点
                var children = GetChildren(node);

                int minParentY = parent.Min(c => c.Top);
                int maxParentY = parent.Max(c => c.Top);

                node.Top = (minParentY + maxParentY) / 2;

                SetCof(node, verticalSpacing);
                int currenty = node.Top;
                foreach (var child in children)
                {
                    LayoutNode(child, currenty);
                    currenty += verticalSpacing;
                }
                MoreParens.Remove(node);
            }
            LayoutNode(rootNode, currentY);
            while (MoreParens.Count > 0)
            {
                foreach (var item in MoreParens.Cast<STNode>().ToList())
                {
                    MoreParentsLayoutNode(item);
                }
            }

        }

        public void SetCof(STNode node, int verticalSpacing)
        {
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode onode)
                {
                    if (onode != node && onode.Left == node.Left && onode.Top == node.Top)
                    {
                        onode.Top += verticalSpacing;
                        SetCof(node, verticalSpacing);
                    }
                }
            }
        }


        public int GetMaxDepth(STNode node)
        {
            var parent = GetParent(node);
            if (parent.Count == 0)
            {
                return 0;
            }
            return parent.Max(c => GetMaxDepth(c)) + 1;
        }

        List<STNode> GetParent(STNode node)
        {
            var list = ConnectionInfo.Where(c => c.Input.Owner == node);
            List<STNode> children = new();
            foreach (var item in list)
            {
                children.Add(item.Output.Owner);

            }
            return children;
        }
        List<STNode> GetChildrenWithout(STNode node)
        {
            var list = ConnectionInfo.Where(c => c.Output.Owner == node);
            List<STNode> children = new();
            foreach (var item in list)
            {
                if (GetParent(item.Input.Owner).Count == 1)
                {
                    children.Add(item.Input.Owner);
                }
            }
            return children;
        }

        List<STNode> GetChildren(STNode node)
        {
            var list = ConnectionInfo.Where(c => c.Output.Owner == node);
            List<STNode> children = new();
            foreach (var item in list)
            {
                children.Add(item.Input.Owner);

            }
            return children;
        }

        public STNode GetRootNode()
        {
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode sTNode && sTNode is MQTTStartNode startNode)
                    return startNode;
            }
            return null;
        }

        public bool CheckFlow()
        {
            ConnectionInfo = STNodeEditor.GetConnectionInfo();

            bool isContainsMQTTStartNode = false;
            bool isContainsCVEndNode = false;
            STNode startNode = null;
            STNode endNode = null;

            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is MQTTStartNode mqttStartNode)
                {
                    isContainsMQTTStartNode = true;
                    startNode = mqttStartNode;
                }
                else if (item is CVEndNode cvEndNode)
                {
                    isContainsCVEndNode = true;
                    endNode = cvEndNode;
                }
            }

            if (!isContainsMQTTStartNode)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程起始结点");
                return false;
            }

            if (!isContainsCVEndNode)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程结束结点");
                return false;
            }

            // 检查从起点到终点的路径
            if (!IsPathExists(startNode, endNode))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法找到从起始结点到结束结点的有效路径");
                return false;
            }
            return true;
        }

        private bool IsPathExists(STNode startNode, STNode endNode)
        {
            var visited = new HashSet<STNode>();
            var queue = new Queue<STNode>();
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                if (currentNode == endNode)
                {
                    return true;
                }

                visited.Add(currentNode);

                var children = GetChildren(currentNode);
                foreach (var child in children)
                {
                    if (!visited.Contains(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return false;
        }
        #endregion
    }
}
