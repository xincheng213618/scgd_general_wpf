using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;
using System.Drawing;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectBlackMura
 {
    public class Measurements : ViewModelBase
    {
        public double Mean { get => _Mean; set { _Mean = value; NotifyPropertyChanged(); } }
        private double _Mean;

        public double Max { get => _Max; set { _Max = value; NotifyPropertyChanged(); } }
        private double _Max;

        public double Min { get => _Min; set { _Min = value; NotifyPropertyChanged(); } }
        private double _Min;

        public double Uniformity { get => _Uniformity; set { _Uniformity = value; NotifyPropertyChanged(); } }
        private double _Uniformity;

        public double? X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private double? _X;
        public double? Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private double? _Y;

        public double Wavelength { get => _Wavelength; set { _Wavelength = value; NotifyPropertyChanged(); } }
        private double _Wavelength;

        public double Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private double _Saturation;

        public double ZaRelmax { get => _ZaRelmax; set { _ZaRelmax = value; NotifyPropertyChanged(); } }
        private double _ZaRelmax;

        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }



    public class BlackMudraResult:ViewModelBase
    {
        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model;

        public DateTime DateTime { get => _DateTime; set { _DateTime = value; NotifyPropertyChanged(); } }
        private DateTime _DateTime = DateTime.Now;

        public BlackMuraTestType BlackMuraTestType { get => _BlackMuraTestType; set { _BlackMuraTestType = value; NotifyPropertyChanged(); } }
        private BlackMuraTestType _BlackMuraTestType = BlackMuraTestType.None;

        public double Contrast { get => _Contrast; set { _Contrast = value; NotifyPropertyChanged(); } }
        private double _Contrast = 1;

        public Measurements WhiteImage { get; set; } = new Measurements();
        public Measurements BlackImage { get; set; } = new Measurements();
        public Measurements RedImage { get; set; } = new Measurements();
        public Measurements GreenImage { get; set; } = new Measurements();
        public Measurements BlueImage { get; set; } = new Measurements();

        public double WhiteUniformityLimit { get; set; } = 80;
        public double BlackUniformitylimit { get; set; } = 40;
        public double BlackMaxLimit { get; set; }
        public double GradientMaxLimit { get; set; } = 0.005;

        public bool Result { get; set; }
    }

    public class ExcelReportGenerator
    {
        public static void GenerateExcel(string filePath, BlackMudraResult blackMudraResult)
        {
            blackMudraResult.Contrast = blackMudraResult.WhiteImage.Mean / blackMudraResult.BlackImage.Mean;

            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("KernelData");

                // 1. 左上角信息
                ws.Cells["A1"].Value = "Serial number:";
                ws.Cells["B1"].Value = blackMudraResult.SN;
                ws.Cells["A2"].Value = "Model:";
                ws.Cells["B2"].Value = blackMudraResult.Model;
                ws.Cells["A3"].Value = "Date time:";
                ws.Cells["B3"].Value = DateTime.Now.ToString("yyyyMMdd/HH:mm:ss");

                // 2. 表头标题
                ws.Cells["B1"].Value = "Measurements";
                ws.Cells["C1"].Value = "White Image";
                ws.Cells["D1"].Value = "Black Image";
                ws.Cells["F1"].Value = "Red Image";
                ws.Cells["G1"].Value = "Green Image";
                ws.Cells["H1"].Value = "Blue Image";

                // 3. 测量数据s
                ws.Cells["B2"].Value = "Mean (cd/m2)";
                ws.Cells["B3"].Value = "Max (cd/m2)";
                ws.Cells["B4"].Value = "Min (cd/m2)";
                ws.Cells["B5"].Value = "Uniformity %";
                ws.Cells["B6"].Value = "x";
                ws.Cells["B7"].Value = "y";
                ws.Cells["B8"].Value = "Wavelength (nm)";
                ws.Cells["B9"].Value = "Saturation (%)";

                ws.Cells["C2"].Value = blackMudraResult.WhiteImage.Mean;
                ws.Cells["C3"].Value = blackMudraResult.WhiteImage.Max;
                ws.Cells["C4"].Value = blackMudraResult.WhiteImage.Min;
                ws.Cells["C5"].Value = blackMudraResult.WhiteImage.Uniformity;
                ws.Cells["C6"].Value = blackMudraResult.WhiteImage.X;
                ws.Cells["C7"].Value = blackMudraResult.WhiteImage.Y;
                ws.Cells["C8"].Value = blackMudraResult.WhiteImage.Wavelength;
                ws.Cells["C9"].Value = blackMudraResult.WhiteImage.Saturation;

                ws.Cells["E1"].Value = "Gradient W - %/Dpixel";
                ws.Cells["E2"].Value = blackMudraResult.WhiteImage.ZaRelmax;

                ws.Cells["D2"].Value = blackMudraResult.BlackImage.Mean;
                ws.Cells["D3"].Value = blackMudraResult.BlackImage.Max;
                ws.Cells["D4"].Value = blackMudraResult.BlackImage.Min;
                ws.Cells["D5"].Value = blackMudraResult.BlackImage.Uniformity;
                ws.Cells["D6"].Value = blackMudraResult.BlackImage.X;
                ws.Cells["D7"].Value = blackMudraResult.BlackImage.Y;
                ws.Cells["D8"].Value = blackMudraResult.BlackImage.Wavelength;
                ws.Cells["D9"].Value = blackMudraResult.BlackImage.Saturation;

                ws.Cells["E3"].Value = "Gradient B - %/Dpixel";
                ws.Cells["E4"].Value = blackMudraResult.BlackImage.ZaRelmax;
                ws.Cells["E5"].Value = "Contrast";
                ws.Cells["E6"].Value = blackMudraResult.Contrast;


                ws.Cells["F2"].Value = blackMudraResult.RedImage.Mean;
                ws.Cells["F3"].Value = blackMudraResult.RedImage.Max;
                ws.Cells["F4"].Value = blackMudraResult.RedImage.Min;
                ws.Cells["F5"].Value = blackMudraResult.RedImage.Uniformity;
                ws.Cells["F6"].Value = blackMudraResult.RedImage.X;
                ws.Cells["F7"].Value = blackMudraResult.RedImage.Y;
                ws.Cells["F8"].Value = blackMudraResult.RedImage.Wavelength;
                ws.Cells["F9"].Value = blackMudraResult.RedImage.Saturation;



                ws.Cells["G2"].Value = blackMudraResult.GreenImage.Mean;
                ws.Cells["G3"].Value = blackMudraResult.GreenImage.Max;
                ws.Cells["G4"].Value = blackMudraResult.GreenImage.Min;
                ws.Cells["G5"].Value = blackMudraResult.GreenImage.Uniformity;
                ws.Cells["G6"].Value = blackMudraResult.GreenImage.X;
                ws.Cells["G7"].Value = blackMudraResult.GreenImage.Y;
                ws.Cells["G8"].Value = blackMudraResult.GreenImage.Wavelength;
                ws.Cells["G9"].Value = blackMudraResult.GreenImage.Saturation;


                ws.Cells["H2"].Value = blackMudraResult.BlueImage.Mean;
                ws.Cells["H3"].Value = blackMudraResult.BlueImage.Max;
                ws.Cells["H4"].Value = blackMudraResult.BlueImage.Min;
                ws.Cells["H5"].Value = blackMudraResult.BlueImage.Uniformity;
                ws.Cells["H6"].Value = blackMudraResult.BlueImage.X;
                ws.Cells["H7"].Value = blackMudraResult.BlueImage.Y;
                ws.Cells["H8"].Value = blackMudraResult.BlueImage.Wavelength;
                ws.Cells["H9"].Value = blackMudraResult.BlueImage.Saturation;

                // 4. Border size, Filter size
                ws.Cells["A10"].Value = "Border size";
                ws.Cells["B10"].Value = "Result";
                ws.Cells["A11"].Value = "Filter size";
                ws.Cells["B11"].Value = "Result";
                ws.Cells["C10"].Value = "27";
                ws.Cells["C11"].Value = "27";
                ws.Cells["D10"].Value = "Max Coordinate";
                ws.Cells["D11"].Value = "Mura Coordinate";
                ws.Cells["E10"].Value = "X,Y";
                ws.Cells["E11"].Value = "X,Y";
                ws.Cells["F10"].Value = "";
                ws.Cells["F11"].Value = "";

                // 5. Tolerances / Limits
                ws.Cells["A14"].Value = "Tolerances / Limits";
                ws.Cells["B14"].Value = "Result";

                ws.Cells["A15"].Value = "WhiteUniformity %";
                ws.Cells["B15"].Value = blackMudraResult.WhiteUniformityLimit;
                ws.Cells["C15"].Value = "PASS";
                ws.Cells["C15"].Style.Font.Color.SetColor(blackMudraResult.Result ? Color.Green : Color.Red);

                ws.Cells["A16"].Value = "BlackUniformity %";
                ws.Cells["B16"].Value = blackMudraResult.BlackUniformitylimit;
                ws.Cells["C16"].Value = "PASS";
                ws.Cells["C16"].Style.Font.Color.SetColor(blackMudraResult.Result ? Color.Green : Color.Red);

                ws.Cells["A17"].Value = "BlackMax cd/m²";
                ws.Cells["B17"].Value = blackMudraResult.BlackMaxLimit;
                ws.Cells["C17"].Value = "PASS";
                ws.Cells["C17"].Style.Font.Color.SetColor(blackMudraResult.Result ? Color.Green : Color.Red);

                ws.Cells["A18"].Value = "GradientMax %/Dpixel";
                ws.Cells["B18"].Value = blackMudraResult.GradientMaxLimit;
                ws.Cells["C18"].Value = "PASS";
                ws.Cells["C18"].Style.Font.Color.SetColor(blackMudraResult.Result ? Color.Green : Color.Red);

                ws.Cells["A19"].Value = "Final Result";
                ws.Cells["C19"].Value = blackMudraResult.Result?"Pass":"Fail";

                ws.Cells["C19"].Style.Font.Color.SetColor(blackMudraResult.Result?Color.Green: Color.Red);

                // 6. 九宫格数据表头
                ws.Cells["B22"].Value = "Nine p";
                ws.Cells["C22"].Value = "White";
                ws.Cells["D22"].Value = "Black";
                ws.Cells["E22"].Value = "Red";
                ws.Cells["F22"].Value = "Green";
                ws.Cells["G22"].Value = "Blue";
                ws.Cells["H22"].Value = "";


                // 7. 九宫格数据
                double[,] nineGrid = new double[,]
                {
                {1,1041.004761, 0.62441444, 214.6376953, 747.2717896, 89.85207367},
                {2,1047.505249, 0.57731575, 215.9981384, 750.315979, 91.97728729},
                {3,1037.700439, 0.69402897, 213.9006598, 743.163208, 90.33718872},
                {4,1034.109741, 0.60744447, 211.7969513, 742.5627944, 89.55202484},
                {5,1082.60791,  0.58938539, 221.7706604, 776.0324986, 95.26428986},
                {6,1035.923828, 0.61586839, 211.269226, 739.8544912, 90.75215912},
                {7,1127.72998,  0.84371829, 229.008228, 812.3952026, 99.05169678},
                {8,1049.366943, 0.58044493, 211.8704529, 753.9787598, 93.85507202},
                {9,1103.226685, 0.77524012, 225.2050323, 791.1521606, 98.2789881},
                };

                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        ws.Cells[23 + i, 2 + j].Value = nineGrid[i, j];
                    }
                }
                for (int i = 0; i < blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count; i++)
                {
                    ws.Cells[23 + i, 2 + 1].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas[i].Y;
                }
                for (int i = 0; i < blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Count; i++)
                {
                    ws.Cells[23 + i, 2 + 2].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas[i].Y;
                }
                for (int i = 0; i < blackMudraResult.RedImage.PoiResultCIExyuvDatas.Count; i++)
                {
                    ws.Cells[23 + i, 2 + 3].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas[i].Y;
                }
                for (int i = 0; i < blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Count; i++)
                {
                    ws.Cells[23 + i, 2 + 4].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas[i].Y;
                }
                for (int i = 0; i < blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Count; i++)
                {
                    ws.Cells[23 + i, 2 + 5].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas[i].Y;
                }


                // 8. 九宫格统计数据
                ws.Cells["B32"].Value = "Mean";
                ws.Cells["C32"].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Average(x => x.Y);
                ws.Cells["D32"].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Average(x => x.Y);
                ws.Cells["E32"].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas.Average(x => x.Y);
                ws.Cells["F32"].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Average(x => x.Y);
                ws.Cells["G32"].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Average(x => x.Y);

                ws.Cells["B33"].Value = "Max";
                ws.Cells["C33"].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Max(x => x.Y);
                ws.Cells["D33"].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["E33"].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["F33"].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["G33"].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Min(x => x.Y);

                ws.Cells["B34"].Value = "Min";
                ws.Cells["C34"].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["D34"].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["E34"].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["F34"].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Min(x => x.Y);
                ws.Cells["G34"].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Min(x => x.Y);

                ws.Cells["B35"].Value = "Unifor";
                ws.Cells["C35"].Value = blackMudraResult.WhiteImage.Uniformity;
                ws.Cells["D35"].Value = blackMudraResult.BlackImage.Uniformity;
                ws.Cells["E35"].Value = blackMudraResult.RedImage.Uniformity;
                ws.Cells["F35"].Value = blackMudraResult.GreenImage.Uniformity;
                ws.Cells["G35"].Value = blackMudraResult.BlueImage.Uniformity;

                ws.Cells["B36"].Value = "x";
                ws.Cells["C36"].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Average(x => x.x);
                ws.Cells["D36"].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Average(x => x.x);
                ws.Cells["E36"].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas.Average(x => x.x);
                ws.Cells["F36"].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Average(x => x.x);
                ws.Cells["G36"].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Average(x => x.x);

                ws.Cells["B37"].Value = "y";
                ws.Cells["C37"].Value = blackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Average(x => x.y);
                ws.Cells["D37"].Value = blackMudraResult.BlackImage.PoiResultCIExyuvDatas.Average(x => x.y);
                ws.Cells["E37"].Value = blackMudraResult.RedImage.PoiResultCIExyuvDatas.Average(x => x.y);
                ws.Cells["F37"].Value = blackMudraResult.GreenImage.PoiResultCIExyuvDatas.Average(x => x.y);
                ws.Cells["G37"].Value = blackMudraResult.BlueImage.PoiResultCIExyuvDatas.Average(x => x.y);

                // 9. 美化样式和自动列宽
                ws.Cells[1, 1, 36, 8].AutoFitColumns();
                ws.Cells["A1:H37"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Cells["A1:H37"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells["A1:H37"].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                // 标题和表头加粗
                ws.Cells["A1:A3"].Style.Font.Bold = true;
                ws.Cells["B1:H1"].Style.Font.Bold = true;
                ws.Cells["B22:H22"].Style.Font.Bold = true;

                // 10. 创建第二个sheet（Image），如需填充可补充
                package.Workbook.Worksheets.Add("Image");

                // 保存
                package.SaveAs(new FileInfo(filePath));
            }
        }
    }

}
