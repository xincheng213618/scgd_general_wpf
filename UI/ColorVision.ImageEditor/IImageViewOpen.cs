#pragma warning disable CS8625
using System.Collections.Generic;

namespace ColorVision.ImageEditor
{
    public interface IImageViewOpen
    {
        public List<string> Extension { get; }

        public void OpenImage(ImageView imageView, string? filePath);
    }
}
