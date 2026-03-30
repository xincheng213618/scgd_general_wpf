using Spectrum.Data;
using Spectrum.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        //导出data数据至excel
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selectedItemsCopy = new List<object>();
            foreach (var item in ViewResultList.SelectedItems)
            {
                selectedItemsCopy.Add(item);
            }

            bool isEqeMode = MainWindowConfig.Instance.EqeEnabled;

            if (!isEqeMode)
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = "SpectrometerExport" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("No");
                properties.Add("IP");
                properties.Add("Luminance(Lv)(cd/m2)");
                properties.Add("Blue Light Intensity");
                properties.Add("CIEx");
                properties.Add("CIEy");
                properties.Add("CIEz");
                properties.Add("Cx");
                properties.Add("Cy");
                properties.Add("u'");
                properties.Add("v'");
                properties.Add("Correlated Color Temperature(CCT)(K)");
                properties.Add("DW(Ld)(nm)");
                properties.Add("Color Purity(%)");
                properties.Add("Peak Wavelength(Lp)(nm)");
                properties.Add("Color Rendering(Ra)");
                properties.Add("FWHM");
                properties.Add("Excitation Purity(%)");
                properties.Add("CIE2015X");
                properties.Add("CIE2015Y");
                properties.Add("CIE2015Z");
                properties.Add("CIE2015x");
                properties.Add("CIE2015y");
                properties.Add("CIE2015u");
                properties.Add("CIE2015v");

                for (int i = 380; i <= 780; i++)
                {
                    properties.Add(i.ToString());
                }
                for (int i = 380; i <= 780; i++)
                {
                    properties.Add("sp" + i.ToString());
                }

                for (int i = 0; i < properties.Count; i++)
                {
                    csvBuilder.Append(properties[i]);
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                csvBuilder.AppendLine();

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSpectrum result)
                    {
                        csvBuilder.Append(result.Id + ",");
                        csvBuilder.Append(result.IP + ",");
                        csvBuilder.Append(result.Lv + ",");
                        csvBuilder.Append(result.Blue + ",");
                        csvBuilder.Append(result.fCIEx + ",");
                        csvBuilder.Append(result.fCIEy + ",");
                        csvBuilder.Append(result.fCIEz + ",");
                        csvBuilder.Append(result.fx + ",");
                        csvBuilder.Append(result.fy + ",");
                        csvBuilder.Append(result.fu + ",");
                        csvBuilder.Append(result.fv + ",");
                        csvBuilder.Append(result.fCCT + ",");
                        csvBuilder.Append(result.fLd + ",");
                        csvBuilder.Append(result.ColorPurityPercent + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.fRa + ",");
                        csvBuilder.Append(result.fHW + ",");
                        csvBuilder.Append(result.ExcitationPurityPercent + ",");
                        csvBuilder.Append(result.fCIEx2015 + ",");
                        csvBuilder.Append(result.fCIEy2015 + ",");
                        csvBuilder.Append(result.fCIEz2015 + ",");
                        csvBuilder.Append(result.fx2015 + ",");
                        csvBuilder.Append(result.fy2015 + ",");
                        csvBuilder.Append(result.fu2015 + ",");
                        csvBuilder.Append(result.fv2015 + ",");

                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                            csvBuilder.Append(',');
                        }
                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                            if (i < result.SpectralDatas.Count - 1)
                                csvBuilder.Append(',');
                        }
                        csvBuilder.AppendLine();
                    }
                }
                File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
            else
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = "EQE" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("No");
                properties.Add("IP");
                properties.Add("EQE(%)");
                properties.Add("LuminousFlux(lm)");
                properties.Add("RadiantFlux(W)");
                properties.Add("LuminousEfficacy(lm/W)");
                properties.Add("Cx");
                properties.Add("Cy");
                properties.Add("Correlated Color Temperature(CCT)(K)");
                properties.Add("Peak Wavelength(Lp)(nm)");
                properties.Add("Excitation Purity(%)");
                properties.Add("Voltage(V)");
                properties.Add("Current(mA)");
                properties.Add("CIE2015X");
                properties.Add("CIE2015Y");
                properties.Add("CIE2015Z");
                properties.Add("CIE2015x");
                properties.Add("CIE2015y");
                properties.Add("CIE2015u");
                properties.Add("CIE2015v");

                for (int i = 380; i <= 780; i++)
                {
                    properties.Add(i.ToString());
                }
                for (int i = 380; i <= 780; i++)
                {
                    properties.Add("sp" + i.ToString());
                }

                for (int i = 0; i < properties.Count; i++)
                {
                    csvBuilder.Append(properties[i]);
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                csvBuilder.AppendLine();

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSpectrum result)
                    {
                        csvBuilder.Append(result.Id + ",");
                        csvBuilder.Append(result.IP + ",");
                        csvBuilder.Append(result.EqePercent + ",");
                        csvBuilder.Append(result.LuminousFlux + ",");
                        csvBuilder.Append(result.RadiantFlux + ",");
                        csvBuilder.Append(result.LuminousEfficacy + ",");
                        csvBuilder.Append(result.fx + ",");
                        csvBuilder.Append(result.fy + ",");
                        csvBuilder.Append(result.fCCT + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.ExcitationPurityPercent + ",");
                        csvBuilder.Append(result.V + ",");
                        csvBuilder.Append(result.I + ",");
                        csvBuilder.Append(result.fCIEx2015 + ",");
                        csvBuilder.Append(result.fCIEy2015 + ",");
                        csvBuilder.Append(result.fCIEz2015 + ",");
                        csvBuilder.Append(result.fx2015 + ",");
                        csvBuilder.Append(result.fy2015 + ",");
                        csvBuilder.Append(result.fu2015 + ",");
                        csvBuilder.Append(result.fv2015 + ",");

                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                            csvBuilder.Append(',');
                        }
                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                            if (i < result.SpectralDatas.Count - 1)
                                csvBuilder.Append(',');
                        }
                        csvBuilder.AppendLine();
                    }
                }
                File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
        }
    }
}
