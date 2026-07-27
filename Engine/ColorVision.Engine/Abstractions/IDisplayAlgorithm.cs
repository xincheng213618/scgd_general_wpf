using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;

namespace ColorVision.Engine
{
    public class DisplayAlgorithmParam
    {
        public Type Type { get; set; } = null!;
        public string? ImageFilePath { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DisplayAlgorithmAttribute : Attribute
    {
        public int Order { get; }
        public string Name { get; }
        public string Group { get; }
        public string DisplayName => Properties.Resources.ResourceManager.GetString(Name, Properties.Resources.Culture) ?? Name;

        public DisplayAlgorithmAttribute(int order, string name, string group)
        {
            Order = order;
            Name = name;
            Group = group;
        }
    }

    public interface IDisplayAlgorithm
    {
        string ImageFilePath { get; set; }
        DisplayAlgorithmConfigBase Configuration { get; }
        MsgRecord? Execute();
    }

    public abstract class DisplayAlgorithmBase : ViewModelBase, IDisplayAlgorithm
    {
        public abstract DisplayAlgorithmConfigBase Configuration { get; }
        public abstract MsgRecord? Execute();

        public string ImageFilePath
        {
            get => Configuration.ImageFilePath;
            set => Configuration.ImageFilePath = value;
        }

        public bool TryGetImageInput(out string imageFileName, out FileExtType fileExtType)
        {
            imageFileName = ImageFilePath;
            fileExtType = FileExtType.Tif;

            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ImageFileCannotBeEmpty, "ColorVision");
                return false;
            }

            fileExtType = ServicesHelper.ResolveFileExtType(imageFileName);
            return true;
        }

        protected static bool TryGetTemplate<T>(
            DisplayAlgorithmTemplateSelection selection,
            out T value)
        {
            if (selection.IsSelectionValid() && selection.TryGetValue(out value))
            {
                return true;
            }

            value = default!;
            MessageBox1.Show(
                Application.Current.GetActiveWindow(),
                selection.ValidationMessage,
                "ColorVision");
            return false;
        }

        protected static bool TryGetOptionalTemplate<T>(
            DisplayAlgorithmTemplateSelection selection,
            out T? value)
        {
            if (selection.SelectedIndex < 0)
            {
                value = default;
                return true;
            }

            if (selection.TryGetValue(out T selectedValue))
            {
                value = selectedValue;
                return true;
            }

            value = default;
            MessageBox1.Show(
                Application.Current.GetActiveWindow(),
                selection.ValidationMessage,
                "ColorVision");
            return false;
        }
    }

    public abstract class DisplayAlgorithmBase<TConfiguration> : DisplayAlgorithmBase
        where TConfiguration : DisplayAlgorithmConfigBase
    {
        public TConfiguration Config { get; }
        public sealed override DisplayAlgorithmConfigBase Configuration => Config;

        protected DisplayAlgorithmBase(TConfiguration configuration)
        {
            Config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
    }
}
