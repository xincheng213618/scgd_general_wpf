using ColorVision.UI.Menus;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Win32;

namespace ColorVision.Engine.Reports
{

    public class ExportReportGenerator : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "ExportReportGenerator";
        public override string Header => "导出报表";
        public override int Order => 100;


        public override void Execute()
        {
            // 创建并配置 SaveFileDialog
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF 文件 (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = "CalibrationReport.pdf"
            };

            // 显示对话框并获取用户选择的文件路径
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                ReportGenerator.GenerateCalibrationReport(saveFileDialog.FileName);
            }

        }
    }

    public static class ReportGenerator
    {
        public static void GenerateCalibrationReport(string filePath)
        {
            // 创建 PDF 文档
            using var document = new Document(new PdfDocument(new PdfWriter(filePath)));


            // 添加抬头
            Paragraph title = new Paragraph("光谱校正数据报表")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(20);
            document.Add(title);

            // 添加空行
            document.Add(new Paragraph("\n"));

            // 创建表格
            Table table = new Table(3, true);
            table.AddCell("公司名称");
            table.AddCell("校正参数1");
            table.AddCell("校正参数2");

            // 添加数据
            table.AddCell("Company A");
            table.AddCell("1.23");
            table.AddCell("4.56");

            table.AddCell("Company B");
            table.AddCell("7.89");
            table.AddCell("0.12");

            // 添加表格到文档
            document.Add(table);

            // 关闭文档
            document.Close();

            // 打开 PDF
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

        }
    }
}
