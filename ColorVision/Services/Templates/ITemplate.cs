using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Templates
{
    public interface IITemplateLoad
    {
        public virtual void Load() { }
    }

    public class ITemplate
    {
        public virtual IEnumerable ItemsSource { get; }

        public string Title { get; set; }

        public string Code { get; set; }

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

        public virtual void  Save()
        {

        }

        public bool IsSideHide { get; set; }

        public virtual void PreviewMouseDoubleClick(int index)
        {

        }

        public virtual void Load() { }

        public virtual void Delete(int index)
        {
        }

        public virtual void Create(string templateName)
        {

        }
        public bool IsUserControl { get; set; }
        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
    }



    public class ITemplate<T> : ITemplate where T : ParamBase, new()
    {
        public ObservableCollection<TemplateModel<T>> TemplateParams { get; set; } = new ObservableCollection<TemplateModel<T>>();

        public override object GetValue() => TemplateParams;
        public override object GetValue(int index) => TemplateParams[index].Value;

        public override IEnumerable ItemsSource { get => TemplateParams; }


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

        public override void Save()
        {
            TemplateControl.Save2DB(TemplateParams);
        }

        public override void Load() => TemplateControl.LoadModParam(TemplateParams,Code);

        public override void Delete(int index)
        {
            if (index >= 0 && index < TemplateParams.Count)
            {
                int id = TemplateParams[index].Value.Id;
                List<ModDetailModel> de = ModDetailDao.Instance.GetAllByPid(id);
                int ret = ModMasterDao.Instance.DeleteById(id);
                ModDetailDao.Instance.DeleteAllByPid(id);
                if (de != null && de.Count > 0)
                {
                    string[] codes = new string[de.Count];
                    int idx = 0;
                    foreach (ModDetailModel model in de)
                    {
                        string code = model.GetValueMD5();
                        codes[idx++] = code;
                    }
                    VSysResourceDao.Instance.DeleteInCodes(codes);
                }
                TemplateParams.RemoveAt(index);
            }
        }

        public override void Create(string templateName)
        {
            T? param = TemplateControl.AddParamMode<T>(Code, templateName);
            if (param != null)
            {
                var a = new TemplateModel<T>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }
}
