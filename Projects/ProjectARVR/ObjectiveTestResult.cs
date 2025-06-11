#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.Image;
using ColorVision.ImageEditor.Draw;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI.Extension;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using Panuon.WPF.UI;
using ProjectARVR.Config;
using ProjectARVR.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVR
{
    public class ObjectiveTestItem
    {
        public string Name { get; set; }         // 项目名称
        public string TestValue { get; set; }    // 测试值
        public string LowLimit { get; set; }     // 下限
        public string UpLimit { get; set; }      // 上限
        public bool TestResult { get; set; } = true;   // 结果
    }

    /// <summary>
    /// 表示一组客观测试项的测试结果，每个属性对应一个具体的测试项目，包含测试值、上下限、结果等信息。
    /// </summary>
    public class ObjectiveTestResult
    {
        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; }

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; }

        /// <summary>
        /// 中心相关色温(K) 测试项
        /// </summary>
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; }

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

        /// <summary>
        /// FOFO对比度 测试项
        /// </summary>
        public ObjectiveTestItem FOFOContrast { get; set; }

        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; }

        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

        /// <summary>
        /// MTF_H 中心_0F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_Center_0F { get; set; }

        /// <summary>
        /// MTF_H 左上_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_LeftUp_0_5F { get; set; }

        /// <summary>
        /// MTF_H 右上_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_RightUp_0_5F { get; set; }

        /// <summary>
        /// MTF_H 右下_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_RightDown_0_5F { get; set; }

        /// <summary>
        /// MTF_H 左下_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_LeftDown_0_5F { get; set; }

        /// <summary>
        /// MTF_H 左上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_LeftUp_0_8F { get; set; }

        /// <summary>
        /// MTF_H 右上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_RightUp_0_8F { get; set; }

        /// <summary>
        /// MTF_H 右下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_RightDown_0_8F { get; set; }

        /// <summary>
        /// MTF_H 左下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_H_LeftDown_0_8F { get; set; }

        /// <summary>
        /// MTF_V 中心_0F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_Center_0F { get; set; }

        /// <summary>
        /// MTF_V 左上_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_LeftUp_0_5F { get; set; }

        /// <summary>
        /// MTF_V 右上_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_RightUp_0_5F { get; set; }

        /// <summary>
        /// MTF_V 右下_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_RightDown_0_5F { get; set; }

        /// <summary>
        /// MTF_V 左下_0.5F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_LeftDown_0_5F { get; set; }

        /// <summary>
        /// MTF_V 左上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_LeftUp_0_8F { get; set; }

        /// <summary>
        /// MTF_V 右上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_RightUp_0_8F { get; set; }

        /// <summary>
        /// MTF_V 右下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_RightDown_0_8F { get; set; }

        /// <summary>
        /// MTF_V 左下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_V_LeftDown_0_8F { get; set; }

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

        /// <summary>
        /// 鬼影(%) 测试项
        /// </summary>
        public ObjectiveTestItem Ghost { get; set; }

        /// <summary>
        /// 总体测试结果（true表示通过，false表示不通过）
        /// </summary>
        public bool TotalResult { get; set; } = true;

        /// <summary>
        /// 总体测试结果字符串（如“pass”或“fail”）
        /// </summary>
        public string TotalResultString { get; set; } = "PASS";
    }
}