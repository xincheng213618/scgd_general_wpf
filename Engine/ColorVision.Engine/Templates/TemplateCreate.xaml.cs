using System;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public interface ITemplateUserControl
    {
        void SetParam(object param);
    }

    public partial class TemplateCreate : Window
    {
        private bool _created;

        public TemplateCreate(ITemplate template, bool isImport = false)
        {
            InitializeComponent();

            Title = $"{(isImport ? Properties.Resources.Import : Properties.Resources.Create)} {template.Title}";
            CreateView.Initialize(template, new TemplateCreateOptions
            {
                InitialSourceKind = template.HasCreateTemplateSource ? TemplateCreateSourceKind.Prepared : TemplateCreateSourceKind.Default,
                SuggestedName = string.IsNullOrWhiteSpace(template.ImportName) ? null : template.ImportName
            });
            CreateView.TemplateCreated += CreateView_TemplateCreated;
            CreateView.CancelRequested += CreateView_CancelRequested;
            Closed += TemplateCreate_Closed;
        }

        public string? CreateName { get; private set; }

        private void CreateView_TemplateCreated(object? sender, TemplateCreatedEventArgs e)
        {
            _created = true;
            CreateName = e.TemplateName;
            DialogResult = true;
        }

        private void CreateView_CancelRequested(object? sender, EventArgs e)
        {
            DialogResult = false;
        }

        private void TemplateCreate_Closed(object? sender, EventArgs e)
        {
            if (!_created)
                CreateView.Discard();
        }
    }
}
