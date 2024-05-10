using ColorVision.Services.Devices.Algorithm.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Services.Templates
{
    public class ITemplate
    {
        public List<TemplateModelBase> Enumerables { get; }

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }
        public virtual object GetValue(int index)
        {
            throw new NotImplementedException();
        }


        public virtual string NewCreateFileName(string FileName)
        {
            throw new NotImplementedException();
        }
    }

    public class ITemplate<T>: ITemplate where T: ParamBase,new ()
    {
        public ObservableCollection<TemplateModel<T>> TemplateParams { get; set; } = new ObservableCollection<TemplateModel<T>>();
       
        public override object GetValue() => TemplateParams;
        public override object GetValue(int index) => TemplateParams[index].Value;

        public new List<TemplateModelBase> Enumerables => TemplateParams.OfType<TemplateModelBase>().ToList();


        public override string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in TemplateParams)
            {
                Names.Add(item.Key);
            }
            for (int i = 1; i < 9999; i++)
            {
                if (!Names.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }
    }

    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTemplate : Window
    {

        public ITemplate ITemplate { get; set; }

        public CreateTemplate(ITemplate template)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = ITemplate.NewCreateFileName("default");
        }


        public string CreateName { get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateName = CreateCode.Text;
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
