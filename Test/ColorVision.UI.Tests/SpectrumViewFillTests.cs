using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using cvColorVision;
using Newtonsoft.Json;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Tests;

public sealed class SpectrumViewFillTests
{
    [Fact]
    public void FillPreferenceRefreshesBothSpectrumModesAndStopsObservingAfterDisposal()
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                var config = ViewSpectrum.Config;
                Assert.True(config.IsShowSpectrumFill);
                config.IsShowSpectrumFill = false;
                using DeviceSpectrum device = new(new SysResourceModel
                {
                    Id = -2, Code = $"spectrum-fill-{Guid.NewGuid():N}", Name = "Offline spectrum fill test", Value = "{}"
                });
                using ViewSpectrum view = new(device);
                var relative = Assert.IsType<WpfPlot>(view.FindName("wpfplot1"));
                var absolute = Assert.IsType<WpfPlot>(view.FindName("wpfplot2"));
                var type = Assert.IsType<ToggleButton>(view.FindName("ToggleSpectrumType"));
                var comparison = Assert.IsType<ToggleButton>(view.FindName("ToggleSpectrumComparison"));
                ViewResultSpectrum first = CreateResult();
                view.AddViewResultSpectrum(first);
                Assert.Same(first.ScatterPlot, Assert.Single(relative.Plot.PlottableList.OfType<Scatter>()));

                config.IsShowSpectrumFill = true;
                AssertFill(relative, true);
                type.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                AssertFill(absolute, true);
                config.IsShowSpectrumFill = false;
                AssertFill(relative, false);
                AssertFill(absolute, false);

                ViewResultSpectrum second = CreateResult();
                view.AddViewResultSpectrum(second);
                Assert.Same(second.AbsoluteScatterPlot, Assert.Single(absolute.Plot.PlottableList.OfType<Scatter>()));
                comparison.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                config.IsShowSpectrumFill = true;
                Assert.Equal(2, absolute.Plot.PlottableList.OfType<Scatter>().Count());
                AssertFill(absolute, false);
                comparison.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                AssertFill(absolute, true);
                type.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                AssertFill(relative, true);

                view.Dispose();
                config.IsShowSpectrumFill = false;
                AssertFill(relative, true);
            }
            finally
            {
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoredPreferenceRoundTripsAndLegacyConfigurationKeepsFillEnabled(bool enabled)
    {
        Assert.True(JsonConvert.DeserializeObject<ViewSpectrumConfig>("{}")!.IsShowSpectrumFill);
        var config = new ViewSpectrumConfig { IsShowSpectrumFill = enabled };
        Assert.Equal(enabled, JsonConvert.DeserializeObject<ViewSpectrumConfig>(JsonConvert.SerializeObject(config))!.IsShowSpectrumFill);
    }

    private static ViewResultSpectrum CreateResult() => new(new COLOR_PARA
    {
        fPL = [0, 1, 0], fRi = new float[15], fSpect1 = 500, fSpect2 = 502, fInterval = 1, fPlambda = 2
    });

    private static void AssertFill(WpfPlot plot, bool expected)
        => Assert.Equal(expected, plot.Plot.PlottableList.OfType<Scatter>().Any(scatter => scatter.FillY));
}
