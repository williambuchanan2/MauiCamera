using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Global
{
    public sealed class ImageUtils
    {
        public const int ThumbnailSize = 50;

        public static ImageUtils Instance { get; set; }
        public string CreateFilePathFromByteArray(byte[] imageBytes, string fileName, string filePath)
        {
            try
            {
                Stream imageStream = new MemoryStream(imageBytes);
                IImage newImage = PlatformImage.FromStream(imageStream);

                string newFn = Path.Combine(filePath, fileName);

                if (File.Exists(newFn))
                {
                    File.Delete(newFn);
                }

                using (MemoryStream memStream = new MemoryStream())
                {
                    newImage.Save(memStream);

                    if (!string.IsNullOrEmpty(fileName))
                    {
                        using (FileStream file = new FileStream(newFn, FileMode.Create, System.IO.FileAccess.Write))
                            memStream.CopyTo(file);

                    }

                    return newFn;
                }
            }
            catch (Exception e)
            {
                throw;
            }

        }

        /// <summary>
        /// Downsizes an image to the max height or width specified
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <param name="maxHeightOrWidth"></param>
        /// <param name="newFileName"></param>
        /// <returns></returns>
        public IImage DownsizeImage(byte[] imageBytes, int maxHeightOrWidth, string newFileName = "")
        {
            try
            {
                Stream imageStream = new MemoryStream(imageBytes);
                IImage newImage = PlatformImage.FromStream(imageStream);

                newImage = newImage.Downsize(maxHeightOrWidth, true);
                using (MemoryStream memStream = new MemoryStream())
                {

                    newImage.Save(memStream);

                    if (!string.IsNullOrEmpty(newFileName))
                    {
                        using (FileStream file = new FileStream(newFileName, FileMode.Create, System.IO.FileAccess.Write))
                            memStream.CopyTo(file);
                    }

                    return newImage;
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<IImage> DownsizeImage(IImage source, int maxHeightOrWidth)
        {
            try
            {
                IImage destination = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    return PlatformImage
                        .FromStream(source.AsStream())
                        .Downsize(maxHeightOrWidth, false);
                });

                using (MemoryStream ms = new())
                {
                    await destination.SaveAsync(ms, format: ImageFormat.Png, 0.8f);
                    return destination;
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public static async Task<IImage> DownsizeImage(IImage source, int maxWidth, int maxHeight)
        {
            try
            {
                return await Task.Run(async () =>
                {
                    IImage resizedImage = null;

                    try
                    {
                        resizedImage = await MainThread.InvokeOnMainThreadAsync(() =>
                            source.Resize(maxWidth, maxHeight));

                        var resultStream = new MemoryStream();
                        await resizedImage.SaveAsync(resultStream, ImageFormat.Png, 0.8f);
                        resultStream.Position = 0;

                        return PlatformImage.FromStream(resultStream);
                    }
                    finally
                    {
                        resizedImage?.Dispose();
                    }
                });
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public IImage DownsizeImage(byte[] imageBytes, int maxHeight, int maxWidth, string newFileName = "")
        {
            try
            {
                Stream imageStream = new MemoryStream(imageBytes);
                IImage newImage = PlatformImage.FromStream(imageStream);

                newImage = newImage.Downsize(maxHeight, maxWidth, true);
                using (MemoryStream memStream = new MemoryStream())
                {
                    newImage.Save(memStream);

                    if (!string.IsNullOrEmpty(newFileName))
                    {
                        using (FileStream file = new FileStream(newFileName, FileMode.Create, System.IO.FileAccess.Write))
                            memStream.CopyTo(file);
                    }

                    return newImage;
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public byte[] DownsizeImageToByteArray(byte[] imageBytes, int maxHeightOrWidth, string newFileName = "")
        {
            return DownsizeImage(imageBytes, maxHeightOrWidth, newFileName).AsBytes();
        }

        public ImageFormat GenerateFormat(string newFn)
        {
            ImageFormat format = ImageFormat.Jpeg;
            if (newFn != null)
            {
                if (newFn.IndexOf("png") > -1)
                {
                    format = ImageFormat.Png;
                }
                else if (newFn.IndexOf("gif") > -1)
                {
                    format = ImageFormat.Gif;
                }
                else if (newFn.IndexOf("tiff") > -1)
                {
                    format = ImageFormat.Tiff;
                }
                else if (newFn.IndexOf("bmp") > -1)
                {
                    format = ImageFormat.Bmp;
                }
            }
            return format;
        }

        public IImage ResizeImage(byte[] imageBytes, int maxHeight, int maxWidth, string newFileName = "")
        {
            try
            {
                Stream imageStream = new MemoryStream(imageBytes);
                IImage newImage = PlatformImage.FromStream(imageStream);

                newImage = newImage.Resize(maxHeight, maxWidth, ResizeMode.Fit, true);
                using (MemoryStream memStream = new MemoryStream())
                {
                    newImage.Save(memStream);

                    if (!string.IsNullOrEmpty(newFileName))
                    {
                        using (FileStream file = new FileStream(newFileName, FileMode.Create, System.IO.FileAccess.Write))
                            memStream.CopyTo(file);
                    }

                    return newImage;
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}
