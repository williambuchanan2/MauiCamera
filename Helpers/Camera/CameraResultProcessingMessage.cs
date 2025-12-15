using System;

namespace MauiCamera.Helpers
{
    /// <summary>
    /// The results of a background processing operation
    /// </summary>
    public record class CameraResultProcessingMessage(CameraResultProcessingOptions Options, TimeSpan Elapsed)
    {
        public int? Height;
        public int? Width;
        public byte[]? AttachmentBytes;
        public byte[]? ThumbnailBytes;
    }
}
