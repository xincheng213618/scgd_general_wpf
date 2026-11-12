using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Testing;
using ProjectARVRPro.Process;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Tests;

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class ProcessFlowTemplateTests
{
    [Fact]
    public void AddingStepSuggestsAnAvailableTemplateNameAndPreservesManualEdits()
    {
        StaTest.Run(() =>
        {
            var white = new TemplateModel<FlowParam>("White51_Test", new FlowParam());
            var black = new TemplateModel<FlowParam>("Black_Test", new FlowParam());
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "white51_test", "White51_Test（1）" };
            var editor = new ProcessMetaEditViewModel(new[] { white, black }, Array.Empty<IProcess>(), "", "", null,
                true, true, false, ProcessMetaEditTarget.Choice, existingNames.Contains);

            Assert.Null(editor.SelectedTemplate);
            Assert.Empty(editor.MetaName);

            editor.SelectedTemplate = white;
            Assert.Equal("White51_Test（2）", editor.MetaName);
            editor.SelectedTemplate = black;
            Assert.Equal("Black_Test", editor.MetaName);

            editor.MetaName = "Custom name";
            editor.GoToProcessPage();
            editor.GoBack();
            editor.SelectedTemplate = white;
            Assert.Equal("Custom name", editor.MetaName);
        });
    }

    [Fact]
    public void RenamedOrDeletedFlowKeepsSavedNameAndReportsHowToRepairIt()
    {
        StaTest.Run(() =>
        {
            var originalTemplates = TemplateFlow.Params;
            try
            {
                var template = new TemplateModel<FlowParam>("OriginalFlow", new FlowParam());
                TemplateFlow.Params = new ObservableCollection<TemplateModel<FlowParam>> { template };
                var meta = new ProcessMeta { Name = "MTF", FlowTemplate = "originalflow" };
                Assert.Same(template, meta.RequireFlowTemplate());
                Assert.False(meta.IsFlowTemplateMissing);

                template.Key = "RenamedFlow";
                Assert.True(meta.IsFlowTemplateMissing);
                var error = Assert.Throws<InvalidOperationException>(() => meta.RequireFlowTemplate());
                Assert.Contains("MTF", error.Message);
                Assert.Contains("originalflow", error.Message);
                Assert.Contains("流程处理配置", error.Message);
                Assert.Contains("重新选择", error.Message);
                Assert.Equal("originalflow", meta.FlowTemplate);

                meta.FlowTemplate = "RenamedFlow";
                Assert.Same(template, meta.RequireFlowTemplate());
                Assert.Empty(meta.FlowTemplateError);
                TemplateFlow.Params.Clear();
                Assert.True(meta.IsFlowTemplateMissing);
                TemplateFlow.Params.Add(template);
                Assert.False(meta.IsFlowTemplateMissing);
            }
            finally
            {
                TemplateFlow.Params = originalTemplates;
            }
        });
    }

    [Fact]
    public void EditingMissingTemplateRequiresExplicitSelectionInsteadOfFirstTemplate()
    {
        StaTest.Run(() =>
        {
            var template = new TemplateModel<FlowParam>("AvailableFlow", new FlowParam());
            var templates = new[] { template };
            var editor = new ProcessMetaEditViewModel(templates, Array.Empty<IProcess>(), "MTF", "OldFlow", null,
                true, true, true, ProcessMetaEditTarget.Template);
            Assert.Null(editor.SelectedTemplate);
            editor.SelectedTemplate = template;
            Assert.Same(template, editor.SelectedTemplate);
            Assert.Equal("MTF", editor.MetaName);

            var matchingEditor = new ProcessMetaEditViewModel(templates, Array.Empty<IProcess>(), "MTF", "availableflow", null,
                true, true, true, ProcessMetaEditTarget.Template);
            Assert.Same(template, matchingEditor.SelectedTemplate);
        });
    }
}
