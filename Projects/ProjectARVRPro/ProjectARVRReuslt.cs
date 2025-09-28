#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Archive.Dao;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.Image;
using ColorVision.ImageEditor.Draw;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.LogImp;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using LiveChartsCore.Kernel;
using log4net;
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using ProjectARVRPro;
using ProjectARVRPro.Services;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace ProjectARVRPro
{
    public enum ARVR1TestType
    {
        None = 0,
        W51,
        /// <summary>
        /// 白画面绿图
        /// </summary>
        White,
        /// <summary>
        /// 黑画面25
        /// </summary>
        Black,
        /// <summary>
        /// 25的图像
        /// </summary>
        W25,
        /// <summary>
        /// 棋盘格
        /// </summary>
        Chessboard,
        /// <summary>
        /// MTFHV 
        /// </summary>
        MTFHV,
        /// <summary>
        /// 畸变，9点
        /// </summary>
        Distortion,
        /// <summary>
        /// 光轴偏角
        /// </summary>
        OpticCenter,
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
        BKscreeenDefectDetection,
    }

    [SugarTable("ARVRReuslt")]
    public class ProjectARVRReuslt : ViewEntity 
    {
        [SqlSugar.SugarColumn(IsIgnore =true)]
        public ContextMenu ContextMenu { get; set; }

        public ProjectARVRReuslt()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Copy, Header = "复制" });

            RelayCommand openFolderAndSelectFile = new RelayCommand(a => 
            {
                PlatformHelper.OpenFolderAndSelectFile(FileName);
            },e=>File.Exists(FileName));

            ContextMenu.Items.Add(new MenuItem() { Command = openFolderAndSelectFile, Header = "OpenFolderAndSelectFile" });

            RelayCommand BatchDataHistoryCommand = new RelayCommand(a => BatchDataHistory(), e => BatchId > 0);
            ContextMenu.Items.Add(new MenuItem() { Command = BatchDataHistoryCommand, Header = "流程结果查询" });
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
        public ARVR1TestType TestType { get; set; }

        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(IsIgnore = true)]
        public ViewResultW25 ViewResultW25 { get; set; } = new ViewResultW25();

        [Browsable(false)]
        public string ViewResultW25Json
        {
            get => JsonConvert.SerializeObject(ViewResultW25);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewResultW25 = JsonConvert.DeserializeObject<ViewResultW25>(value);
            }
        }

        [SugarColumn(IsIgnore = true)]
        public ViewReslutW51 ViewReslutW51 { get; set; } = new ViewReslutW51();

        [Browsable(false)]
        public string ViewReslutW51Json
        {
            get => JsonConvert.SerializeObject(ViewReslutW51);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewReslutW51 = JsonConvert.DeserializeObject<ViewReslutW51>(value);
            }
        }

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
        public ViewRelsultMTFHV ViewRelsultMTFH { get; set; } = new ViewRelsultMTFHV();

        [Browsable(false)]
        public string ViewRelsultMTFHJson
        {
            get => JsonConvert.SerializeObject(ViewRelsultMTFH);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ViewRelsultMTFH = JsonConvert.DeserializeObject<ViewRelsultMTFHV>(value);
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
        public string ViewResultOpticCentertJson
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
        public FindCrossDetailViewReslut FindCrossDetailViewReslut { get; set; }

        public FindCrossDetailViewReslut FindCrossDetailViewReslut1 { get; set; }

        /// <summary>
        /// X轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; }

        /// <summary>
        /// Y轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; }

        /// <summary>
        /// 旋转角(°) 测试项
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; }

        /// <summary>
        /// X轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; }

        /// <summary>
        /// Y轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; }

        /// <summary>
        /// 旋转角(°) 测试项
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; }
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

    public class ViewRelsultMTFHV
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

    public class ViewReslutW51
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; }
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

    public class ViewResultW25
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

    }


    public class ViewResultWhite
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        public DFovView DFovView { get; set; }
        /// <summary>
        /// 中心相关色温(K) 测试项
        /// </summary>
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem W255LuminanceUniformity { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem W255ColorUniformity { get; set; } = new ObjectiveTestItem();

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