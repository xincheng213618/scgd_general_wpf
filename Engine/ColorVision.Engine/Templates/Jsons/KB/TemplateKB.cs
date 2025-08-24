﻿#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{

    public class TemplateJsonKBParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonKBParam));
        public RelayCommand EditTemplatePoiCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public KBJson  KBJson { get 
            {
                try
                {
                    KBJson kBJson = JsonConvert.DeserializeObject<KBJson>(JsonValue);
                    if (kBJson == null)
                    {
                        kBJson = new KBJson();
                        JsonValue = JsonConvert.SerializeObject(kBJson);
                        return kBJson;
                    }
                    return kBJson;  
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    KBJson kBJson = new KBJson();
                    JsonValue = JsonConvert.SerializeObject(kBJson);
                    return kBJson;
                }
            }
            set {
                JsonValue = JsonConvert.SerializeObject(value);
                NotifyPropertyChanged(); 
            } }

        public TemplateJsonKBParam() : base()
        {
        }

        public TemplateJsonKBParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {

        }


    }



    public class TemplateKB : ITemplateJson<TemplateJsonKBParam>,IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonKBParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonKBParam>>();

        public TemplateKB()
        {
            Title = "键盘检测模板管理";
            Code = "KB";
            TemplateDicId = 150;
            TemplateParams = Params;
            IsSideHide = true;
        }
        public EditPoiParam1 EditWindow { get; set; }
        public override void PreviewMouseDoubleClick(int index)
        {
            EditWindow = new EditPoiParam1(Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.ShowDialog();
        }

        public override bool ExitsTemplateName(string templateName)
        {
            return Params.Any(a => a.Key.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditKBTemplateJson EditTemplateJson { get; set; } = new EditKBTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditKBTemplateJson();
        public override IMysqlCommand? GetMysqlCommand() => new MysqKB();


    }




}
