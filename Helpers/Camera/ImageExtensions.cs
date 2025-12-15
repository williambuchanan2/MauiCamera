using System;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiCamera.Helpers
{
    public static partial class Extensions
    {
        public static SKBitmap AutoOrient(this SKBitmap bitmap, int rotation)
        {
            if (bitmap is null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            SKBitmap rotated;

            switch (rotation)
            {
                case 180:
                    using (var surface = new SKCanvas(bitmap))
                    {
                        surface.RotateDegrees(180, bitmap.Width / 2, bitmap.Height / 2);
                        surface.DrawBitmap(bitmap.Copy(), 0, 0);
                    }
                    return bitmap;
                case 90:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(rotated.Width, 0);
                        surface.RotateDegrees(90);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case 270:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, rotated.Height);
                        surface.RotateDegrees(270);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                default:
                    return bitmap;
            }
        }

        public static bool IsLandscape(this IImage image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.Width > image.Height;
        }
    }
}
