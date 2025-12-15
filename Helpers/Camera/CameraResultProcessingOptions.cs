using System;
using Microsoft.Maui.Graphics;

namespace MauiCamera.Helpers
{
    /// <summary>
    /// Options to be used when performing image processing
    /// </summary>
    public sealed class CameraResultProcessingOptions
    {
        public int? Downsize { get; set; }
        public string Path { get; set; }
        public string PathWithExtension { get; set; }
        public string PhotoFileType { get; set; }
        public string ProcessingId { get; set; } = Guid.NewGuid().ToString();
        public int Quality { get; set; } = 100;
        public int Rotation { get; set; }
        public IImage SourceImage { get; set; }
        public string ThumbnailPath { get; set; }
        public DateTime? OriginalPhotoDateTime { get; set; }
    }
}
