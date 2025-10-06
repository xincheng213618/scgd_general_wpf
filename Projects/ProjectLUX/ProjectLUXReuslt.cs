#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine;
using ColorVision.Engine.Archive.Dao;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using LiveChartsCore.Kernel;
using log4net;
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Asn1.Ocsp;
using ProjectLUX;
using ProjectLUX.Services;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectLUX
{

    public enum ARVRTestType
    {
        None,
        /// <summary>
        /// 白画面
        /// </summary>
        White,
        White2,
        White1,
        /// <summary>
        /// 黑画面
        /// </summary>
        Black,
        /// <summary>
        /// 棋盘格
        /// </summary>
        Chessboard,
        /// <summary>
        /// MTF 横
        /// </summary>
        MTFH,
        /// <summary>
        /// MTF垂直
        /// </summary>
        MTFV,
        /// <summary>
        /// 畸变
        /// </summary>
        Distortion,
        /// <summary>
        /// 光轴偏角
        /// </summary>
        OpticCenter,
        /// <summary>
        /// 鬼影
        /// </summary>
        Ghost,
        /// <summary>
        /// 屏幕定位
        /// </summary>
        DotMatrix,
        /// <summary>
        /// 白画面瑕疵检测
        /// </summary>
        WscreeenDefectDetection,
        /// <summary>
        /// 黑画面瑕疵检测
        /// </summary>
        BKscreeenDefectDetection
    }

    [SugarTable("ARVRReuslt")]
    public class ProjectLUXReuslt : ViewEntity 
    {
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public ContextMenu ContextMenu { get; set; }

        public ProjectLUXReuslt()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Copy, Header = "复制" });

            RelayCommand openFolderAndSelectFile = new RelayCommand(a =>
            {
                PlatformHelper.OpenFolderAndSelectFile(FileName);
            }, e => File.Exists(FileName));


            ContextMenu.Items.Add(new MenuItem() { Command = openFolderAndSelectFile, Header = "OpenFolderAndSelectFile" });

            RelayCommand BatchDataHistoryCommand = new RelayCommand(a => BatchDataHistory(),e => BatchId > 0);
            ContextMenu.Items.Add(new MenuItem() { Command = BatchDataHistoryCommand ,Header ="流程结果查询"});

        }

        public void BatchDataHistory()
        {
            var Batch = MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().Where(a => a.Id == BatchId).First();
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            Frame frame = new Frame();
            MeasureBatchPage batchDataHistory = new MeasureBatchPage(frame, Batch);
            Window window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.Content = batchDataHistory;
            window.Show();
        }



        public int BatchId { get => _BatchId; set { _BatchId = value; OnPropertyChanged(); } }
        private int _BatchId;

        public string Model { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;

        public string SN { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;

        public bool Result { get; set; } = true;
        public ARVRTestType TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(IsIgnore = true)]
        public ViewResultWhite ViewResultWhite { get; set; } = new ViewResultWhite();

        [Browsable(false)]
        public string ViewResultWhiteJson
        {
            get => JsonConvert.SerializeObject(ViewResultWhite);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewResultWhite = JsonConvert.DeserializeObject<ViewResultWhite>(value);
            }
        }

        [SugarColumn(IsIgnore = true)]
        public ViewResultBlack ViewResultBlack { get; set; } = new ViewResultBlack();

        [Browsable(false)]
        public string ViewResultBlackJson
        {
            get => JsonConvert.SerializeObject(ViewResultBlack);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewResultBlack = JsonConvert.DeserializeObject<ViewResultBlack>(value);
            }
        }


        [SugarColumn(IsIgnore = true)]
        public ViewReslutCheckerboard ViewReslutCheckerboard { get; set; } = new ViewReslutCheckerboard();

        [Browsable(false)]
        public string ViewReslutCheckerboardJson
        {
            get => JsonConvert.SerializeObject(ViewReslutCheckerboard);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewReslutCheckerboard = JsonConvert.DeserializeObject<ViewReslutCheckerboard>(value);
            }
        }

        [SugarColumn(IsIgnore = true)]
        public ViewRelsultMTFH ViewRelsultMTFH { get; set; } = new ViewRelsultMTFH();

        [Browsable(false)]
        public string ViewRelsultMTFHJson
        {
            get => JsonConvert.SerializeObject(ViewRelsultMTFH);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewRelsultMTFH = JsonConvert.DeserializeObject<ViewRelsultMTFH>(value);
            }
        }
        
        
        [SugarColumn(IsIgnore = true)]
        public ViewRelsultMTFV ViewRelsultMTFV { get; set; } = new ViewRelsultMTFV();

        [Browsable(false)]
        public string ViewRelsultMTFVJson
        {
            get => JsonConvert.SerializeObject(ViewRelsultMTFV);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewRelsultMTFV = JsonConvert.DeserializeObject<ViewRelsultMTFV>(value);
            }
        }


        [SugarColumn(IsIgnore = true)]
        public ViewReslutDistortionGhost ViewReslutDistortionGhost { get; set; } = new ViewReslutDistortionGhost();


        [Browsable(false)]
        public string ViewReslutDistortionGhostJson
        {
            get => JsonConvert.SerializeObject(ViewReslutDistortionGhost);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewReslutDistortionGhost = JsonConvert.DeserializeObject<ViewReslutDistortionGhost>(value);
            }
        }
        [SugarColumn(IsIgnore = true)]
        public ViewResultOpticCenter ViewResultOpticCenter { get; set; } = new ViewResultOpticCenter();

        [Browsable(false)]
        public string ViewResultOpticCenterJson
        {
            get => JsonConvert.SerializeObject(ViewResultOpticCenter);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewResultOpticCenter = JsonConvert.DeserializeObject<ViewResultOpticCenter>(value);
            }
        }

    }


    public class ViewResultOpticCenter
    {
        public BinocularFusionModel BinocularFusionModel { get; set; }

        /// <summary>
        /// X轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem XTilt { get; set; }

        /// <summary>
        /// Y轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem YTilt { get; set; }

        /// <summary>
        /// 旋转角(°) 测试项
        /// </summary>
        public ObjectiveTestItem Rotation { get; set; }
    }

    public class ViewReslutDistortionGhost
    {
        public ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View Distortion2View { get; set; }

        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

    }

    public class ViewRelsultMTFH
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }
    public class ViewRelsultMTFV
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class ViewResultBlack
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        /// <summary>
        /// FOFO对比度 测试项
        /// </summary>
        public ObjectiveTestItem FOFOContrast { get; set; }


    }

    public class ViewResultWhite
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; }

        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        public DFovView DFovView { get; set; }

        /// <summary>
        /// 中心相关色温(K) 测试项
        /// </summary>
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; }

        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; }

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; }

        /// <summary>
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }


    }

    public class ViewReslutCheckerboard
    {
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; }
    }

}