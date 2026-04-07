using ScottPlot;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Themes
{
    /// <summary>
    /// ScottPlot 图表主题管理器
    /// 自动根据 ColorVision 主题切换 ScottPlot 图表的颜色配置
    /// </summary>
    public static class ScottPlotThemeManager
    {
        // 使用 ConditionalWeakTable 存储 WpfPlot 与事件处理器的映射，避免阻止 GC
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, ThemeChangedHandler> _plotHandlers = new();

        /// <summary>
        /// 浅色主题配色
        /// </summary>
        public static readonly ScottPlotThemeColors LightTheme = new()
        {
            FigureBackgroundColor = new ScottPlot.Color(255, 255, 255),
            DataBackgroundColor = new ScottPlot.Color(250, 250, 250),
            AxisFrameColor = new ScottPlot.Color(0, 0, 0),
            AxisTextColor = new ScottPlot.Color(0, 0, 0),
            GridLineColor = new ScottPlot.Color(200, 200, 200),
            LegendBackgroundColor = new ScottPlot.Color(255, 255, 255),
            LegendTextColor = new ScottPlot.Color(0, 0, 0),
            LegendOutlineColor = new ScottPlot.Color(150, 150, 150),
        };

        /// <summary>
        /// 深色主题配色
        /// </summary>
        public static readonly ScottPlotThemeColors DarkTheme = new()
        {
            FigureBackgroundColor = new ScottPlot.Color(30, 30, 30),
            DataBackgroundColor = new ScottPlot.Color(40, 40, 40),
            AxisFrameColor = new ScottPlot.Color(220, 220, 220),
            AxisTextColor = new ScottPlot.Color(220, 220, 220),
            GridLineColor = new ScottPlot.Color(80, 80, 80),
            LegendBackgroundColor = new ScottPlot.Color(50, 50, 50),
            LegendTextColor = new ScottPlot.Color(220, 220, 220),
            LegendOutlineColor = new ScottPlot.Color(100, 100, 100),
        };

        /// <summary>
        /// 粉色主题配色
        /// </summary>
        public static readonly ScottPlotThemeColors PinkTheme = new()
        {
            FigureBackgroundColor = new ScottPlot.Color(255, 245, 247),
            DataBackgroundColor = new ScottPlot.Color(255, 250, 251),
            AxisFrameColor = new ScottPlot.Color(80, 40, 60),
            AxisTextColor = new ScottPlot.Color(80, 40, 60),
            GridLineColor = new ScottPlot.Color(230, 200, 210),
            LegendBackgroundColor = new ScottPlot.Color(255, 245, 247),
            LegendTextColor = new ScottPlot.Color(80, 40, 60),
            LegendOutlineColor = new ScottPlot.Color(200, 160, 180),
        };

        /// <summary>
        /// 青色主题配色
        /// </summary>
        public static readonly ScottPlotThemeColors CyanTheme = new()
        {
            FigureBackgroundColor = new ScottPlot.Color(240, 255, 255),
            DataBackgroundColor = new ScottPlot.Color(245, 255, 255),
            AxisFrameColor = new ScottPlot.Color(0, 80, 80),
            AxisTextColor = new ScottPlot.Color(0, 80, 80),
            GridLineColor = new ScottPlot.Color(180, 220, 220),
            LegendBackgroundColor = new ScottPlot.Color(240, 255, 255),
            LegendTextColor = new ScottPlot.Color(0, 80, 80),
            LegendOutlineColor = new ScottPlot.Color(150, 200, 200),
        };

        /// <summary>
        /// 静态构造函数，订阅主题变更事件
        /// </summary>
        static ScottPlotThemeManager()
        {
            // 订阅主题变更事件
            ThemeManager.Current.CurrentUIThemeChanged += OnThemeChanged;
        }

        /// <summary>
        /// 主题变更时的全局处理
        /// </summary>
        private static void OnThemeChanged(Theme newTheme)
        {
            // 触发全局主题变更事件，让已注册的图表自行更新
            ThemeChanged?.Invoke(newTheme);
        }

        /// <summary>
        /// 主题变更事件（全局）
        /// </summary>
        public static event ThemeChangedHandler? ThemeChanged;

        /// <summary>
        /// 获取当前主题对应的颜色配置
        /// </summary>
        public static ScottPlotThemeColors GetThemeColors(Theme? theme = null)
        {
            theme ??= ThemeManager.Current.CurrentUITheme;

            return theme switch
            {
                Theme.Dark => DarkTheme,
                Theme.Pink => PinkTheme,
                Theme.Cyan => CyanTheme,
                _ => LightTheme, // Light 和 UseSystem 默认使用浅色
            };
        }

        /// <summary>
        /// 应用主题到 ScottPlot.WPF.WpfPlot 控件
        /// 使用弱引用事件订阅，不会阻止控件被 GC 回收
        /// </summary>
        /// <param name="plot">WpfPlot 控件</param>
        /// <param name="autoRefresh">是否自动刷新图表</param>
        public static void ApplyTheme(object plot, bool autoRefresh = true)
        {
            if (plot == null) return;

            var colors = GetThemeColors();
            ApplyThemeColors(plot, colors);

            if (autoRefresh)
            {
                RefreshPlot(plot);
            }
        }

        /// <summary>
        /// 注册图表控件以自动跟随主题变化
        /// 使用 ConditionalWeakTable 存储事件处理器，不会阻止 GC
        /// </summary>
        /// <param name="plot">WpfPlot 控件</param>
        /// <param name="autoRefresh">主题变更时是否自动刷新</param>
        public static void RegisterPlot(object plot, bool autoRefresh = true)
        {
            if (plot == null) return;

            // 如果已经注册过，先取消注册
            UnregisterPlot(plot);

            // 创建主题变更处理器
            ThemeChangedHandler handler = (theme) =>
            {
                ApplyTheme(plot, autoRefresh);
            };

            // 使用 ConditionalWeakTable 存储，不会阻止 plot 被 GC
            _plotHandlers.Add(plot, handler);

            // 订阅全局主题变更事件
            ThemeChanged += handler;

            // 立即应用当前主题
            ApplyTheme(plot, autoRefresh);
        }

        /// <summary>
        /// 取消注册图表控件，停止自动主题跟随
        /// </summary>
        /// <param name="plot">WpfPlot 控件</param>
        public static void UnregisterPlot(object plot)
        {
            if (plot == null) return;

            if (_plotHandlers.TryGetValue(plot, out var handler))
            {
                ThemeChanged -= handler;
                _plotHandlers.Remove(plot);
            }
        }

        /// <summary>
        /// 应用颜色配置到图表
        /// </summary>
        private static void ApplyThemeColors(object plot, ScottPlotThemeColors colors)
        {
            try
            {
                // 使用反射获取 Plot 属性，避免直接引用 ScottPlot.WPF
                var plotType = plot.GetType();
                var plotProperty = plotType.GetProperty("Plot");
                if (plotProperty == null) return;

                var scottPlot = plotProperty.GetValue(plot);
                if (scottPlot == null) return;

                // 设置背景色
                SetProperty(scottPlot, "FigureBackground", colors.FigureBackgroundColor);
                SetProperty(scottPlot, "DataBackground", colors.DataBackgroundColor);

                // 设置坐标轴样式
                var axesProperty = scottPlot.GetType().GetProperty("Axes");
                if (axesProperty != null)
                {
                    var axes = axesProperty.GetValue(scottPlot);
                    if (axes != null)
                    {
                        // 设置坐标轴颜色
                        SetProperty(axes, "Color", colors.AxisFrameColor);

                        // 设置标题颜色
                        var titleProperty = axes.GetType().GetProperty("Title");
                        if (titleProperty != null)
                        {
                            var title = titleProperty.GetValue(axes);
                            if (title != null)
                            {
                                var labelProperty = title.GetType().GetProperty("Label");
                                if (labelProperty != null)
                                {
                                    var label = labelProperty.GetValue(title);
                                    SetProperty(label, "ForeColor", colors.AxisTextColor);
                                }
                            }
                        }

                        // 设置 X 轴标签颜色
                        var bottomProperty = axes.GetType().GetProperty("Bottom");
                        if (bottomProperty != null)
                        {
                            var bottom = bottomProperty.GetValue(axes);
                            var labelProperty = bottom?.GetType().GetProperty("Label");
                            if (labelProperty != null)
                            {
                                var label = labelProperty.GetValue(bottom);
                                SetProperty(label, "ForeColor", colors.AxisTextColor);
                            }
                            // 设置刻度颜色
                            var tickLabelStyleProperty = bottom?.GetType().GetProperty("TickLabelStyle");
                            if (tickLabelStyleProperty != null)
                            {
                                var tickLabelStyle = tickLabelStyleProperty.GetValue(bottom);
                                SetProperty(tickLabelStyle, "ForeColor", colors.AxisTextColor);
                            }
                        }

                        // 设置 Y 轴标签颜色
                        var leftProperty = axes.GetType().GetProperty("Left");
                        if (leftProperty != null)
                        {
                            var left = leftProperty.GetValue(axes);
                            var labelProperty = left?.GetType().GetProperty("Label");
                            if (labelProperty != null)
                            {
                                var label = labelProperty.GetValue(left);
                                SetProperty(label, "ForeColor", colors.AxisTextColor);
                            }
                            // 设置刻度颜色
                            var tickLabelStyleProperty = left?.GetType().GetProperty("TickLabelStyle");
                            if (tickLabelStyleProperty != null)
                            {
                                var tickLabelStyle = tickLabelStyleProperty.GetValue(left);
                                SetProperty(tickLabelStyle, "ForeColor", colors.AxisTextColor);
                            }
                        }
                    }
                }

                // 设置网格线颜色
                var gridProperty = scottPlot.GetType().GetProperty("Grid");
                if (gridProperty != null)
                {
                    var grid = gridProperty.GetValue(scottPlot);
                    SetProperty(grid, "MajorLineColor", colors.GridLineColor);
                    SetProperty(grid, "MinorLineColor", colors.GridLineColor.WithAlpha(128));
                }

                // 设置图例样式
                var legendProperty = scottPlot.GetType().GetProperty("Legend");
                if (legendProperty != null)
                {
                    var legend = legendProperty.GetValue(scottPlot);
                    if (legend != null)
                    {
                        SetProperty(legend, "BackgroundColor", colors.LegendBackgroundColor);
                        SetProperty(legend, "FontColor", colors.LegendTextColor);
                        SetProperty(legend, "OutlineColor", colors.LegendOutlineColor);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScottPlotThemeManager: Failed to apply theme: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新图表显示
        /// </summary>
        private static void RefreshPlot(object plot)
        {
            try
            {
                var refreshMethod = plot.GetType().GetMethod("Refresh");
                refreshMethod?.Invoke(plot, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScottPlotThemeManager: Failed to refresh plot: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置对象属性（使用反射）
        /// </summary>
        private static void SetProperty(object obj, string propertyName, object value)
        {
            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                property?.SetValue(obj, value);
            }
            catch
            {
                // 忽略设置失败的属性
            }
        }

        /// <summary>
        /// 判断当前是否为深色主题
        /// </summary>
        public static bool IsDarkTheme()
        {
            return ThemeManager.Current.CurrentUITheme == Theme.Dark;
        }

        /// <summary>
        /// 获取适合当前主题的数据系列颜色
        /// </summary>
        public static ScottPlot.Color[] GetPaletteColors(int count)
        {
            if (IsDarkTheme())
            {
                // 深色主题使用更鲜艳的颜色
                return new[]
                {
                    new ScottPlot.Color(255, 99, 71),   // Tomato
                    new ScottPlot.Color(30, 144, 255),  // DodgerBlue
                    new ScottPlot.Color(50, 205, 50),   // LimeGreen
                    new ScottPlot.Color(255, 215, 0),   // Gold
                    new ScottPlot.Color(238, 130, 238), // Violet
                    new ScottPlot.Color(0, 255, 255),   // Cyan
                    new ScottPlot.Color(255, 165, 0),   // Orange
                    new ScottPlot.Color(255, 105, 180), // HotPink
                };
            }
            else
            {
                // 浅色主题使用标准颜色
                return new[]
                {
                    new ScottPlot.Color(0, 114, 178),   // Blue
                    new ScottPlot.Color(230, 159, 0),   // Orange
                    new ScottPlot.Color(0, 158, 115),   // Green
                    new ScottPlot.Color(204, 121, 167), // Purple
                    new ScottPlot.Color(86, 180, 233),  // SkyBlue
                    new ScottPlot.Color(213, 94, 0),    // Vermillion
                    new ScottPlot.Color(240, 228, 66),  // Yellow
                    new ScottPlot.Color(0, 0, 0),       // Black
                };
            }
        }
    }

    /// <summary>
    /// ScottPlot 主题颜色配置
    /// </summary>
    public class ScottPlotThemeColors
    {
        public ScottPlot.Color FigureBackgroundColor { get; set; }
        public ScottPlot.Color DataBackgroundColor { get; set; }
        public ScottPlot.Color AxisFrameColor { get; set; }
        public ScottPlot.Color AxisTextColor { get; set; }
        public ScottPlot.Color GridLineColor { get; set; }
        public ScottPlot.Color LegendBackgroundColor { get; set; }
        public ScottPlot.Color LegendTextColor { get; set; }
        public ScottPlot.Color LegendOutlineColor { get; set; }
    }
}
