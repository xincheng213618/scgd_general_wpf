using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Themes.Examples
{
    /// <summary>
    /// ColorVision.Themes 性能优化示例
    /// </summary>
    public class ThemeOptimizationExample
    {
        /// <summary>
        /// 示例1: 基本主题切换（使用自动缓存）
        /// </summary>
        public void BasicThemeSwitch()
        {
            // 切换到深色主题 - 资源会自动缓存
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Dark);
            
            // 切换到浅色主题 - 如果之前加载过，会从缓存读取
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Light);
        }

        /// <summary>
        /// 示例2: 预加载主题提升切换性能
        /// </summary>
        public async Task PreloadThemesAsync()
        {
            // 应用启动时预加载所有主题
            await ThemeManager.Current.PreloadThemesAsync();
            
            // 之后的主题切换将会非常快速
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Pink);
        }

        /// <summary>
        /// 示例3: 监控主题切换性能
        /// </summary>
        public void MonitorThemePerformance()
        {
            var stopwatch = Stopwatch.StartNew();
            
            // 第一次切换（需要加载资源）
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Dark);
            stopwatch.Stop();
            Debug.WriteLine($"First switch to Dark: {stopwatch.ElapsedMilliseconds}ms");
            
            stopwatch.Restart();
            
            // 切换到其他主题
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Light);
            stopwatch.Stop();
            Debug.WriteLine($"Switch to Light: {stopwatch.ElapsedMilliseconds}ms");
            
            stopwatch.Restart();
            
            // 再次切换回深色（使用缓存）
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Dark);
            stopwatch.Stop();
            Debug.WriteLine($"Second switch to Dark (cached): {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 示例4: 监控缓存状态
        /// </summary>
        public void MonitorCacheStatus()
        {
            // 获取缓存统计信息
            var (total, alive) = ThemeManager.Current.GetCacheStats();
            Debug.WriteLine($"Resource cache status:");
            Debug.WriteLine($"  - Total cached resources: {total}");
            Debug.WriteLine($"  - Currently alive: {alive}");
            Debug.WriteLine($"  - GC collected: {total - alive}");
        }

        /// <summary>
        /// 示例5: 内存优化 - 清理缓存
        /// </summary>
        public void MemoryOptimization()
        {
            // 在内存紧张时，可以手动清理缓存
            var beforeMemory = GC.GetTotalMemory(false);
            Debug.WriteLine($"Memory before cleanup: {beforeMemory / 1024}KB");
            
            // 清理缓存
            ThemeManager.Current.ClearResourceCache();
            
            // 强制GC（仅用于演示，实际应用中谨慎使用）
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            var afterMemory = GC.GetTotalMemory(true);
            Debug.WriteLine($"Memory after cleanup: {afterMemory / 1024}KB");
            Debug.WriteLine($"Memory freed: {(beforeMemory - afterMemory) / 1024}KB");
        }

        /// <summary>
        /// 示例6: 应用启动时的最佳实践
        /// </summary>
        public class OptimizedAppExample
        {
            public async Task OnApplicationStartup()
            {
                // 1. 应用初始主题（同步，快速）
                Application.Current.ApplyTheme(Theme.Dark);
                
                // 2. 创建并显示主窗口
                // var mainWindow = new MainWindow();
                // mainWindow.Show();
                
                // 3. 异步预加载其他主题（不阻塞UI）
                await ThemeManager.Current.PreloadThemesAsync();
                
                Debug.WriteLine("All themes preloaded in background");
            }
        }

        /// <summary>
        /// 示例7: 性能测试对比
        /// </summary>
        public async Task PerformanceComparison()
        {
            Debug.WriteLine("=== Theme Performance Test ===");
            
            // 测试1: 不预加载
            ThemeManager.Current.ClearResourceCache();
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Dark);
            sw.Stop();
            var withoutPreload = sw.ElapsedMilliseconds;
            Debug.WriteLine($"Without preload: {withoutPreload}ms");
            
            // 测试2: 预加载后
            await ThemeManager.Current.PreloadThemesAsync();
            
            sw.Restart();
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Light);
            sw.Stop();
            var withPreload = sw.ElapsedMilliseconds;
            Debug.WriteLine($"With preload: {withPreload}ms");
            
            Debug.WriteLine($"Performance improvement: {withoutPreload - withPreload}ms faster");
            
            // 检查缓存状态
            var (total, alive) = ThemeManager.Current.GetCacheStats();
            Debug.WriteLine($"Cache status: {alive}/{total} resources alive");
        }
    }
}
