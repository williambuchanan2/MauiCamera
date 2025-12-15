using MauiCamera.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace MauiCamera.Helpers
{
    public sealed class CameraResult : ImageProcessingStatus
    {
        public ImageFormat Format { get; set; }
        public string PhotoFileName { get; set; }
        public int PhotoFileSize { get; set; }
        public string PhotoFileType { get; set; }
        public string PhotoFullPath { get; set; }
        public string PhotoFullPathAndFilename
        {
            get
            {
                return System.IO.Path.Combine(PhotoFullPath, PhotoFileName);
            }
        }
        public int PhotoHeight { get; set; }
        public byte[] PhotoImageBytes { get; set; }
        public string PhotoImageData { get; set; }
        public ImageSource PhotoImageSource { get; set; }
        public int PhotoWidth { get; set; }
        public bool ProcessingDone { get => ProcessingId is not null; }
        public string ProcessingId { get; set; }
        object? ProcessingStatus.ProcessingId { get; set; }
        public int Rotation { get; set; }
        public byte[] ThumbBytes { get; set; }
        public DateTime? OriginalPhotoDateTime { get; set; }
    }
}
