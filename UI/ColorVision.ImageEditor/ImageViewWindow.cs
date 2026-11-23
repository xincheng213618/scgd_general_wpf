using ColorVision.ImageEditor.Abstractions;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor;

public static class ImageViewWindow
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ImageViewWindow));

    public static string OpenInNewWindowText => DisplayText.Get("在新窗口中打开图像");

    internal static RoutedUICommand OpenCurrentImageCommand { get; } = new(
        OpenInNewWindowText, nameof(OpenCurrentImageCommand), typeof(ImageViewWindow));

    /// <summary>Checks the file is available and has a registered image format.</summary>
    public static bool CanOpenFile(string? filePath)
    {
        string? extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && File.Exists(filePath) && AssemblyHandler.GetInstance().GetImplementationTypes(typeof(IImageOpen))
            .Any(type => type.GetCustomAttribute<FileExtensionAttribute>()?.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase) == true);
    }

    public static async Task OpenFileAsync(string? filePath, Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        owner ??= Application.Current?.GetActiveWindow();
        ImageView? view = null;
        try
        {
            view = new ImageView();
            if (!view.TryOpenImage(filePath))
                throw new NotSupportedException(string.Format(Properties.Resources.ImageView_UnsupportedImageFormat, Path.GetExtension(filePath)));
            await view.ImageOpenCompletion;
            if (view.Presentation.DisplaySource == null)
                throw new InvalidDataException(DisplayText.Get("图像文件不存在或无法访问。"));

            ShowCore(Path.GetFileName(filePath), view, owner);
            view = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError(owner, ex); }
        finally { view?.Dispose(); }
    }

    public static void Show(Action<ImageView> initializeImage, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(initializeImage);
        owner ??= Application.Current?.GetActiveWindow();
        ImageView? view = null;
        try
        {
            view = new ImageView();
            initializeImage(view);
            ShowCore(OpenInNewWindowText, view, owner);
            view = null;
        }
        catch (Exception ex) { ShowError(owner, ex); }
        finally { view?.Dispose(); }
    }

    private static void ShowCore(string title, ImageView view, Window? owner)
    {
        Window window = new() { Title = title, Width = 960, Height = 720, Content = view };
        window.Closed += (_, _) => view.Dispose();
        try
        {
            if (owner?.IsLoaded == true)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
            window.ApplyCaption();
            window.Show();
            view.UpdateZoomAndScale();
        }
        catch
        {
            window.Close();
            throw;
        }
    }

    private static void ShowError(Window? owner, Exception error)
    {
        log.Warn("Could not open an image in a new window.", error);
        string message = error is FileNotFoundException
            ? DisplayText.Get("图像文件不存在或无法访问。")
            : DisplayText.Format($"打开图像失败：{error.Message}");
        if (owner?.IsLoaded == true)
            MessageBox.Show(owner, message, OpenInNewWindowText, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(message, OpenInNewWindowText, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
