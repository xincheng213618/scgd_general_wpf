using ColorVision.Services.Devices.PG.Templates;
using OpenCvSharp.Flann;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Services.Templates
{
    public class ITemplate
    {
        public virtual List<TemplateModelBase> Enumerables { get; }

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
    }

    public class ITemplate<T> : ITemplate where T : ParamBase, new()
    {
        public ObservableCollection<TemplateModel<T>> TemplateParams { get; set; } = new ObservableCollection<TemplateModel<T>>();

        public override object GetValue() => TemplateParams;
        public override object GetValue(int index) => TemplateParams[index].Value;

        public override List<TemplateModelBase> Enumerables => TemplateParams.OfType<TemplateModelBase>().ToList();


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
    }
}
