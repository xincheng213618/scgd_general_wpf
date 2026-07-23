using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.IO;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    internal sealed class LocalFileImageNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
        public int Bpp { get; init; }
        public int Channels { get; init; }
        public bool HasRaw { get; init; }
        public bool HasCie { get; init; }
    }

    [STNode("Flow_CustomNodes", "本地图像内存输入")]
    public sealed class LocalFileImageNode : LocalFlowNodeBase
    {
        private string _ImageFilePath = string.Empty;

        [Category("本地图像")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("文件路径", "读取 CVRAW、CVCIE 或普通位图到流程内存", true)]
        public string ImageFilePath { get => _ImageFilePath; set { _ImageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

        public LocalFileImageNode() : base("本地图像内存输入", "LocalImageMemory", "Load", 60000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (string.IsNullOrWhiteSpace(ImageFilePath)) throw new InvalidOperationException("图像文件路径为空。");
            string filePath = Path.GetFullPath(ImageFilePath.Trim());
            LocalFlowFrame frame = LocalFrameFileService.Load(filePath);
            try
            {
                action.SetCurrentFrame(frame);
                LocalFlowFrame currentFrame = frame;
                frame = null!;
                LocalFileImageNodeResultData result = new()
                {
                    FrameId = currentFrame.FrameId.ToString("N"),
                    FilePath = filePath,
                    Width = currentFrame.Metadata.Width,
                    Height = currentFrame.Metadata.Height,
                    Bpp = currentFrame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie ? currentFrame.Metadata.CieBpp : currentFrame.Metadata.SourceBpp,
                    Channels = currentFrame.Metadata.Channels,
                    HasRaw = currentFrame.HasRaw,
                    HasCie = currentFrame.HasCie
                };
                return new LocalNodeExecutionResult { Data = result };
            }
            finally
            {
                frame?.Dispose();
            }
        }
    }
}
