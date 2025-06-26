using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;
using System.Drawing;

public class ExcelReportGenerator
{
    public static void GenerateExcel(string filePath)
    {
        ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");
        using (var package = new ExcelPackage())
        {
            var ws = package.Workbook.Worksheets.Add("KernelData");

            // 1. 左上角信息
            ws.Cells["A1"].Value = "Serial number:";
            ws.Cells["B1"].Value = "SN123456789";
            ws.Cells["A2"].Value = "Model:";
            ws.Cells["B2"].Value = "COG-ABC123456789-01";
            ws.Cells["A3"].Value = "Date time:";
            ws.Cells["B3"].Value = "20250117/15:04:04";

            // 2. 表头标题
            ws.Cells["B1"].Value = "Measurements";
            ws.Cells["C1"].Value = "White Image";
            ws.Cells["D1"].Value = "Black Image";
            ws.Cells["E1"].Value = "Gradient W - %/Dpixel";
            ws.Cells["F1"].Value = "Red Image";
            ws.Cells["G1"].Value = "Green Image";
            ws.Cells["H1"].Value = "Blue Image";

            // 3. 测量数据
            ws.Cells["B2"].Value = "Mean (cd/m2)";
            ws.Cells["C2"].Value = 1062.13;
            ws.Cells["D2"].Value = 0.66;
            ws.Cells["E2"].Value = 217.26;
            ws.Cells["F2"].Value = 761.85;
            ws.Cells["G2"].Value = 93.21;

            ws.Cells["B3"].Value = "Max (cd/m2)";
            ws.Cells["C3"].Value = 1127.73;
            ws.Cells["D3"].Value = 0.84;
            ws.Cells["E3"].Value = 229;
            ws.Cells["F3"].Value = 812.4;
            ws.Cells["G3"].Value = 99.05;

            ws.Cells["B4"].Value = "Min (cd/m2)";
            ws.Cells["C4"].Value = 1034.11;
            ws.Cells["D4"].Value = 0.58;
            ws.Cells["E4"].Value = 211.13;
            ws.Cells["F4"].Value = 739.85;
            ws.Cells["G4"].Value = 89.55;

            ws.Cells["B5"].Value = "Uniformity %";
            ws.Cells["C5"].Value = 91.7;
            ws.Cells["D5"].Value = 68.43;
            ws.Cells["E5"].Value = ""; // 空白
            ws.Cells["F5"].Value = 92.19;
            ws.Cells["G5"].Value = 91.07;
            ws.Cells["H5"].Value = 90.41;

            ws.Cells["B6"].Value = "x";
            ws.Cells["C6"].Value = 0.3005;
            ws.Cells["D6"].Value = ""; // 空白
            ws.Cells["E6"].Value = 1618.09;
            ws.Cells["F6"].Value = 0.6806;
            ws.Cells["G6"].Value = 0.2766;
            ws.Cells["H6"].Value = 0.1528;

            ws.Cells["B7"].Value = "y";
            ws.Cells["C7"].Value = 0.3298;
            ws.Cells["D7"].Value = ""; // 空白
            ws.Cells["E7"].Value = ""; // 空白
            ws.Cells["F7"].Value = 0.3163;
            ws.Cells["G7"].Value = 0.6743;
            ws.Cells["H7"].Value = 0.0618;

            ws.Cells["B8"].Value = "Wavelength (nm)";
            ws.Cells["C8"].Value = 491.67;
            ws.Cells["D8"].Value = ""; // 空白
            ws.Cells["E8"].Value = ""; // 空白
            ws.Cells["F8"].Value = 616.47;
            ws.Cells["G8"].Value = 546.60;
            ws.Cells["H8"].Value = 465.78;

            ws.Cells["B9"].Value = "Saturation (%)";
            ws.Cells["C9"].Value = 11.34;
            ws.Cells["D9"].Value = ""; // 空白
            ws.Cells["E9"].Value = ""; // 空白
            ws.Cells["F9"].Value = 99.13;
            ws.Cells["G9"].Value = 87.77;
            ws.Cells["H9"].Value = 91.93;

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
            ws.Cells["B15"].Value = 80;
            ws.Cells["C15"].Value = "PASS";
            ws.Cells["C15"].Style.Font.Color.SetColor(Color.Green);

            ws.Cells["A16"].Value = "BlackUniformity %";
            ws.Cells["B16"].Value = 40;
            ws.Cells["C16"].Value = "PASS";
            ws.Cells["C16"].Style.Font.Color.SetColor(Color.Green);

            ws.Cells["A17"].Value = "BlackMax cd/m²";
            ws.Cells["B17"].Value = 0;
            ws.Cells["C17"].Value = "PASS";
            ws.Cells["C17"].Style.Font.Color.SetColor(Color.Green);

            ws.Cells["A18"].Value = "GradientMax %/Dpixel";
            ws.Cells["B18"].Value = 0.005;
            ws.Cells["C18"].Value = "PASS";
            ws.Cells["C18"].Style.Font.Color.SetColor(Color.Green);

            ws.Cells["A19"].Value = "Final Result";
            ws.Cells["C19"].Value = "Pass";
            ws.Cells["C19"].Style.Font.Color.SetColor(Color.Green);

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
                {1041.004761, 0.62441444, 214.6376953, 747.2717896, 89.85207367, 0},
                {1047.505249, 0.57731575, 215.9981384, 750.315979, 91.97728729, 0},
                {1037.700439, 0.69402897, 213.9006598, 743.163208, 90.33718872, 0},
                {1034.109741, 0.60744447, 211.7969513, 742.5627944, 89.55202484, 0},
                {1082.60791,  0.58938539, 221.7706604, 776.0324986, 95.26428986, 0},
                {1035.923828, 0.61586839, 211.269226, 739.8544912, 90.75215912, 0},
                {1127.72998,  0.84371829, 229.008228, 812.3952026, 99.05169678, 0},
                {1049.366943, 0.58044493, 211.8704529, 753.9787598, 93.85507202, 0},
                {1103.226685, 0.77524012, 225.2050323, 791.1521606, 98.2789881, 0},
            };

            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    ws.Cells[23 + i, 2 + j].Value = nineGrid[i, j];
                }
            }

            // 8. 九宫格统计数据
            ws.Cells["B32"].Value = "Mean";
            ws.Cells["C32"].Value = 1062.130615;
            ws.Cells["D32"].Value = 0.6564089;
            ws.Cells["E32"].Value = 217.2566071;
            ws.Cells["F32"].Value = 761.8544992;
            ws.Cells["G32"].Value = 93.21342468;
            ws.Cells["H32"].Value = 0;

            ws.Cells["B33"].Value = "Max";
            ws.Cells["C33"].Value = 1127.72998;
            ws.Cells["D33"].Value = 0.84371829;
            ws.Cells["E33"].Value = 229.008228;
            ws.Cells["F33"].Value = 812.3952026;
            ws.Cells["G33"].Value = 99.05169678;
            ws.Cells["H33"].Value = 0;

            ws.Cells["B34"].Value = "Min";
            ws.Cells["C34"].Value = 1034.109741;
            ws.Cells["D34"].Value = 0.57731575;
            ws.Cells["E34"].Value = 211.1269226;
            ws.Cells["F34"].Value = 739.8544992;
            ws.Cells["G34"].Value = 89.55202484;
            ws.Cells["H34"].Value = 0;

            ws.Cells["B35"].Value = "Unifor";
            ws.Cells["C35"].Value = 91.69402313;
            ws.Cells["D35"].Value = 68.42517865;
            ws.Cells["E35"].Value = 92.19402313;
            ws.Cells["F35"].Value = 91.07076263;
            ws.Cells["G35"].Value = 90.40937805;
            ws.Cells["H35"].Value = 0;

            ws.Cells["B36"].Value = "x";
            ws.Cells["C36"].Value = 0.300512254;
            ws.Cells["D36"].Value = 0;
            ws.Cells["E36"].Value = 0.680644899;
            ws.Cells["F36"].Value = 0.276552618;
            ws.Cells["G36"].Value = 0.15280357;
            ws.Cells["H36"].Value = 0;

            // 9. 美化样式和自动列宽
            ws.Cells[1, 1, 36, 8].AutoFitColumns();
            ws.Cells["A1:H36"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells["A1:H36"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells["A1:H36"].Style.Border.BorderAround(ExcelBorderStyle.Thin);

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