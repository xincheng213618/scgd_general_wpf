using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ColorVision.Engine.Templates
{
    public enum InsertMode
    {
        Default,
        SortedByName
    }


    public class TemplateConfig:ViewModelBase,IConfig
    {
        public static TemplateConfig Instance => ConfigService.Instance.GetRequiredService<TemplateConfig>();

        public InsertMode InsertMode { get => _InsertMode; set { _InsertMode = value;  NotifyPropertyChanged(); } } 
        private InsertMode _InsertMode = InsertMode.Default;
    }

    public static class Extension
    {
        public static ObservableCollection<TemplateModel<T>> CreateDefault<T>(this ObservableCollection<TemplateModel<T>>? templateModels) where T : ParamBase, new()
        {
            var templateModels1 = new ObservableCollection<TemplateModel<T>>();
            templateModels1.Insert(0, new TemplateModel<T>("Default", new T() { Id = -2 }));

            if (templateModels != null)
            {
                foreach (var item in templateModels)
                    templateModels1.Add(item);

                templateModels.CollectionChanged -= CalibrationParams_CollectionChanged;
                templateModels.CollectionChanged += CalibrationParams_CollectionChanged;
            }
            void CalibrationParams_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                            foreach (TemplateModel<T> newItem in e.NewItems)
                                templateModels1.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                            foreach (TemplateModel<T> newItem in e.OldItems)
                                templateModels1.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        templateModels1.Clear();
                        templateModels1.Insert(0, new TemplateModel<T>("Default", new T()) { Id = -2 });
                        break;
                }
            }
            return templateModels1;
        }


        public static ObservableCollection<TemplateModel<T>> CreateDefaultEmpty<T>(this ObservableCollection<TemplateModel<T>>? templateModels) where T : ParamBase, new()
        {
            var templateModels1 = new ObservableCollection<TemplateModel<T>>();
            templateModels1.Insert(0, new TemplateModel<T>("Empty", new T() { Id = -1 }));
            templateModels1.Insert(1, new TemplateModel<T>("Default", new T() { Id = -2 }));

            if (templateModels != null)
            {
                foreach (var item in templateModels)
                    templateModels1.Add(item);

                templateModels.CollectionChanged -= CalibrationParams_CollectionChanged;
                templateModels.CollectionChanged += CalibrationParams_CollectionChanged;
            }
            void CalibrationParams_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                            foreach (TemplateModel<T> newItem in e.NewItems)
                                templateModels1.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                            foreach (TemplateModel<T> newItem in e.OldItems)
                                templateModels1.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        templateModels1.Clear();
                        templateModels1.Insert(0, new TemplateModel<T>("Empty", new T()) { Id = -1 });
                        templateModels1.Insert(1, new TemplateModel<T>("Default", new T() { Id = -2 }));

                        break;
                }
            }
            return templateModels1;
        }

        public static ObservableCollection<TemplateModel<T>> Create<T>(this ObservableCollection<TemplateModel<T>>? templateModels) where T : ParamBase, new()
        {
            if (TemplateConfig.Instance.InsertMode == InsertMode.Default)
            {
                return templateModels;
            }
            else
            {

            }
            return CreateTemplateModelEmpty(templateModels);
        }

        public static ObservableCollection<TemplateModel<T>> CreateEmpty<T>(this ObservableCollection<TemplateModel<T>>? templateModels) where T : ParamBase, new()
        {
            return CreateTemplateModelEmpty(templateModels);
        }

        public static ObservableCollection<TemplateModel<T>> CreateTemplateModelEmpty<T>(ObservableCollection<TemplateModel<T>>? templateModels) where T : ParamBase, new()
        {
            var templateModels1 = new ObservableCollection<TemplateModel<T>>();
            templateModels1.Insert(0, new TemplateModel<T>("Empty", new T() { Id = -1 }));

            if (templateModels != null)
            {
                foreach (var item in templateModels)
                    templateModels1.Add(item);

                templateModels.CollectionChanged -= CalibrationParams_CollectionChanged;
                templateModels.CollectionChanged += CalibrationParams_CollectionChanged;
            }
            void CalibrationParams_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                            foreach (TemplateModel<T> newItem in e.NewItems)
                                templateModels1.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                            foreach (TemplateModel<T> newItem in e.OldItems)
                                templateModels1.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        templateModels1.Clear();
                        templateModels1.Insert(0, new TemplateModel<T>("Empty", new T()) { Id = -1 });
                        break;
                }
            }
            return templateModels1;
        }

    }
}
