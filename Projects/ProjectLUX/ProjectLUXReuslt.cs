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
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;


        [SugarColumn(IsNullable = true)]
        public string ViewResultJson { get; set; }

    }


}