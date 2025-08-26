#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Engine.
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Database;
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
using System;
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
    public static class ObjectiveTestResultCsvExporter
    {
        public static void ExportToCsv(List<ObjectiveTestResult> results, string filePath)
        {
            // 获取所有 ObjectiveTestItem 类型的属性
            var itemProps = typeof(ObjectiveTestResult).GetProperties()
                .Where(p => p.PropertyType == typeof(ObjectiveTestItem))
                .ToList();

            // CSV 列头（每项后缀可自定义，比如 TestValue/Name/LowLimit/UpLimit/TestResult 均输出）
            var headers = new List<string>();
            foreach (var prop in itemProps)
            {
                headers.Add($"{prop.Name}_TestValue");
                headers.Add($"{prop.Name}_TestResult");
                headers.Add($"{prop.Name}_LowLimit");
                headers.Add($"{prop.Name}_UpLimit");
            }
            headers.Add("TotalResult");
            headers.Add("TotalResultString");

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var result in results)
            {
                var row = new List<string>();
                foreach (var prop in itemProps)
                {
                    var item = (ObjectiveTestItem)prop.GetValue(result);
                    row.Add(item?.TestValue ?? "");
                    row.Add(item?.TestResult.ToString() ?? "");
                    row.Add(item?.LowLimit.ToString());
                    row.Add(item?.UpLimit.ToString());
                }
                row.Add(result.TotalResult.ToString());
                row.Add(result.TotalResultString);
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // 防止逗号、引号等导致格式问题
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    public class ObjectiveTestItem
    {
        public string Name { get; set; }         // 项目名称

        //这里有可能添加符号
        public string TestValue { get; set; }    // 测试值
        public double Value { get; set; }    // 测试值

        public double LowLimit { get; set; }     // 下限
        public double UpLimit { get; set; }      // 上限

        public bool TestResult {
            get
            {
                // 判断是否低于下限
                bool isAboveLowLimit = LowLimit == 0 || Value >= LowLimit;
                // 判断是否高于上限
                bool isBelowUpLimit = UpLimit == 0 || Value <= UpLimit;
                // 只有同时满足上下限才返回 true
                return isAboveLowLimit && isBelowUpLimit;
            } 
        }
    }

    /// <summary>
    /// 表示一组客观测试项的测试结果，每个属性对应一个具体的测试项目，包含测试值、上下限、结果等信息。
    /// </summary>
    public class ObjectiveTestResult:ViewModelBase
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
        /// 新增中心相关色温(K) 测试项
        /// </summary>
        public ObjectiveTestItem White1CenterCorrelatedColorTemperature { get; set; }

        /// <summary>
        /// 白画面的中心亮度
        /// </summary>
        public ObjectiveTestItem CenterLuminace { get; set; }

        /// <summary>
        /// 新增白画面的中心亮度
        /// </summary>
        public ObjectiveTestItem White1CenterLuminace { get; set; }



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
        public bool TotalResult { get => _TotalResult; set { _TotalResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalResultString)); } } 
        private bool _TotalResult = false;

        /// <summary>
        /// 总体测试结果字符串（如“pass”或“fail”）
        /// </summary>
        public string TotalResultString => TotalResult?"PASS":"Fail";

        public bool FlowWhiteTestReslut { get; set; } = false;
        public bool FlowWhite1TestReslut { get; set; } = false;

        public bool FlowBlackTestReslut { get; set; } = false;
        public bool FlowChessboardTestReslut { get; set; } = false;
        public bool FlowMTFHTestReslut { get; set; } = false;

        public bool FlowMTFVTestReslut { get; set; } = false;
        public bool FlowDistortionTestReslut { get; set; } = false;
        public bool FlowOpticCenterTestReslut { get; set; } = false;
    }
}