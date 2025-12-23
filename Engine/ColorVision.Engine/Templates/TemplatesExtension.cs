using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ColorVision.Engine.Templates
{
    public static class TemplatesExtension
    {
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

                templateModels.CollectionChanged -= Params_CollectionChanged;
                templateModels.CollectionChanged += Params_CollectionChanged;
            }
            void Params_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
