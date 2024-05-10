using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Flow;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates.POI;
using NPOI.SS.Formula.Functions;
using OpenCvSharp.Flann;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Templates
{
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

    public class TemplateSpectrumResourceParam : ITemplate<SpectrumResourceParam>
    {
        public TemplateSpectrumResourceParam()
        {
            IsUserControl = true;
            Code = ModMasterType.SpectrumResource;
        }

        public SpectrumResourceControl SpectrumResourceControl { get; set; }
        public override UserControl GetUserControl() => SpectrumResourceControl;

        public DeviceSpectrum Device { get; set; }


        public override void Load()
        {
            base.Load();
            SpectrumResourceParam.Load(TemplateParams, Device.SysResourceModel.Id, Code);
        }

        public override void Create(string templateName)
        {
            SpectrumResourceParam? param = TemplateControl.AddParamMode<SpectrumResourceParam>(Code, templateName, Device.SysResourceModel.Id);
            if (param != null)
            {
                var a = new TemplateModel<SpectrumResourceParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }

    public class TemplateCalibrationParam : ITemplate<CalibrationParam>
    {
        public TemplateCalibrationParam()
        {
            IsUserControl = true;
            Code = ModMasterType.Calibration;
        }

        public CalibrationControl CalibrationControl { get; set; }
        public override UserControl GetUserControl() => CalibrationControl;

        public ICalibrationService<BaseResourceObject> Device { get; set; }


        public override void Load()
        {
            base.Load();
            CalibrationParam.LoadResourceParams(TemplateParams, Device.SysResourceModel.Id, Code);
        }
        public override void Create(string templateName)
        {
            CalibrationParam? param = TemplateControl.AddParamMode<CalibrationParam>(Code, templateName, Device.SysResourceModel.Id);
            if (param != null)
            {
                var a = new TemplateModel<CalibrationParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }
    public class TemplateFlow: ITemplate<FlowParam>
    {
        public TemplateFlow()
        {
            Title = "流程引擎";
            Code = ModMasterType.Flow;
        }

        public override void Load() => FlowParam.LoadFlowParam();

        public override void Create(string templateName)
        {
            FlowParam? param = FlowParam.AddFlowParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<FlowParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }

    public class TemplatePOI : ITemplate<PoiParam>
    {
        public TemplatePOI()
        {
            Title = "关注点设置";
            Code = ModMasterType.POI;
        }

        public override void Load() => FlowParam.LoadFlowParam();

        public override void Create(string templateName)
        {
            PoiParam? param = PoiParam.AddPoiParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<PoiParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
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

        public override void Load()
        {

        }

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
