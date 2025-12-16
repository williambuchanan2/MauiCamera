using System;
using System.IO;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using SkiaSharp;

namespace Global
{
    internal static class CameraCompressionHelper
    {
        internal const int TargetFileSizeBytes = 500 * 1024;
        private const int InitialJpegQuality = 100;
        private const int MinJpegQuality = 80;
        private const int QualityStep = 5;
        private const double ResizeFactor = 0.98;
        private const int MaxResizeAttempts = 6;
        private const int MinShortEdge = 50;

        internal static (IImage Image, byte[] Bytes) CreateOrientedImage(IImage source, int rotation)
        {
            using var managedStream = new SKManagedStream(source.AsStream());
            using var codec = SKCodec.Create(managedStream);
            using var bitmap = SKBitmap.Decode(codec);
            SKBitmap oriented = CameraHelper.AutoOrient(bitmap, rotation);

            try
            {
                using var skImage = SKImage.FromBitmap(oriented);
                using var data = skImage.Encode(SKEncodedImageFormat.Jpeg, 100);
                byte[] bytes = data.ToArray();
                IImage orientedImage = PlatformImage.FromStream(new MemoryStream(bytes));
                return (orientedImage, bytes);
            }
            finally
            {
                if (!ReferenceEquals(oriented, bitmap))
                {
                    oriented.Dispose();
                }
            }
        }

        internal static CompressionOutcome CompressToTargetSize(IImage source, ImageFormat format, ImageUtils imageUtils)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (imageUtils is null)
            {
                throw new ArgumentNullException(nameof(imageUtils));
            }

            IImage workingImage = source;
            int quality = InitialJpegQuality;
            byte[] encoded = EncodeImage(workingImage, format, quality);
            encoded = ReduceQuality(workingImage, format, encoded, ref quality);

            int resizeAttempt = 0;
            while (encoded.Length > TargetFileSizeBytes && resizeAttempt < MaxResizeAttempts)
            {
                resizeAttempt++;
                IImage? resized = TryResizeTowardsTarget(workingImage, imageUtils);
                if (resized is null)
                {
                    break;
                }

                if (!ReferenceEquals(workingImage, source))
                {
                    workingImage.Dispose();
                }

                workingImage = resized;
                quality = InitialJpegQuality;
                encoded = EncodeImage(workingImage, format, quality);
                encoded = ReduceQuality(workingImage, format, encoded, ref quality);
            }

            int width = (int)Math.Round(workingImage.Width);
            int height = (int)Math.Round(workingImage.Height);

            if (!ReferenceEquals(workingImage, source))
            {
                workingImage.Dispose();
            }

            return new CompressionOutcome(encoded, width, height);
        }

        private static byte[] ReduceQuality(IImage image, ImageFormat format, byte[] currentBytes, ref int quality)
        {
            byte[] encoded = currentBytes;

            while (encoded.Length > TargetFileSizeBytes && quality > MinJpegQuality)
            {
                int nextQuality = Math.Max(MinJpegQuality, quality - QualityStep);
                if (nextQuality == quality)
                {
                    break;
                }

                quality = nextQuality;
                encoded = EncodeImage(image, format, quality);
            }

            return encoded;
        }

        private static IImage? TryResizeTowardsTarget(IImage source, ImageUtils imageUtils)
        {
            int width = (int)Math.Round(source.Width);
            int height = (int)Math.Round(source.Height);

            if (width < 1 || height < 1)
            {
                return null;
            }

            int shortEdge = Math.Min(width, height);
            if (shortEdge <= MinShortEdge)
            {
                return null;
            }

            int targetWidth = Math.Max(1, (int)Math.Round(width * ResizeFactor));
            int targetHeight = Math.Max(1, (int)Math.Round(height * ResizeFactor));

            if (targetWidth == width && targetHeight == height)
            {
                return null;
            }

            return imageUtils.DownsizeImage(source.AsBytes(), targetHeight, targetWidth, string.Empty);
        }

        private static byte[] EncodeImage(IImage image, ImageFormat format, int quality)
        {
            using MemoryStream ms = new();
            float normalizedQuality = Math.Clamp(quality, 1, 100) / 100f;
            image.Save(ms, format, normalizedQuality);
            return ms.ToArray();
        }
    }

    internal readonly struct CompressionOutcome
    {
        public CompressionOutcome(byte[] bytes, int width, int height)
        {
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            Width = width;
            Height = height;
        }

        public byte[] Bytes { get; }

        public int Width { get; }

        public int Height { get; }
    }
}
