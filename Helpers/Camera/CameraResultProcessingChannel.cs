using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Global;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using SkiaSharp;

namespace MauiCamera.Helpers
{
    public sealed class CameraResultProcessingChannel : IDisposable
    {
        private const int ThumbSize = 50;
        private const int MaxImageSizeBytes = 512_000; // 500 KB in bytes
        private readonly Channel<CameraResultProcessingOptions> channel;
        private readonly System.Timers.Timer timer;
        private bool disposedValue;

        public CameraResultProcessingChannel(ImageUtils imageHelper)
        {
            ImageHelper = imageHelper;
            channel = Channel.CreateBounded<CameraResultProcessingOptions>(25);

            timer = new System.Timers.Timer(1000 * 0.1)
            {
                AutoReset = true,
                Enabled = false
            };

            timer.Elapsed += OnTimerElapsed;
        }

        private ImageUtils ImageHelper { get; }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Default behaviour is to wait indefinitely until a space is available for the job. Must be explicitly aborted using cancellation token
        /// </summary>
        public async Task WriteAsync(CameraResultProcessingOptions options, CancellationToken cancellationToken = default)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (await channel.Writer.WaitToWriteAsync(cancellationToken))
            {
                await channel.Writer.WriteAsync(options, cancellationToken);

                if (!timer.Enabled)
                {
                    timer.Enabled = true;
                }
            }
        }

        private async Task<CameraResultProcessingMessage?> CreateAttachment(IImage attachment, CameraResultProcessingOptions options)
        {
            var start = DateTime.Now;

            try
            {
                var originalHeight = (int)attachment.Height;
                var originalWidth = (int)attachment.Width;
                var initialBytes = await attachment.AsBytesAsync();

                // Check if downsizing is needed
                if (options.Downsize is null && initialBytes.Length <= MaxImageSizeBytes)
                {
                    await File.WriteAllBytesAsync(options.Path, initialBytes);
                    return new CameraResultProcessingMessage(options, DateTime.Now - start)
                    {
                        AttachmentBytes = initialBytes,
                        Height = originalHeight,
                        Width = originalWidth
                    };
                }

                // Downsizing required
                var targetBytes = await DownsizeImageToTarget(attachment, options);
                await File.WriteAllBytesAsync(options.Path, targetBytes.bytes);

                return new CameraResultProcessingMessage(options, DateTime.Now - start)
                {
                    AttachmentBytes = targetBytes.bytes,
                    Height = targetBytes.height,
                    Width = targetBytes.width
                };
            }
            finally
            {
                attachment?.Dispose();
            }
        }

        private async Task<(byte[] bytes, int width, int height)> DownsizeImageToTarget(IImage sourceImage, CameraResultProcessingOptions options)
        {
            var currentImage = sourceImage;
            var iterations = 0;
            const int maxIterations = 10;
            const int minDimension = 50;
            const double scaleMargin = 0.85;

            try
            {
                while (iterations < maxIterations)
                {
                    var currentBytes = await currentImage.AsBytesAsync();

                    if (currentBytes.Length <= MaxImageSizeBytes)
                    {
                       // Logger.LogInformation($"Successfully downsized image to {currentBytes.Length} bytes after {iterations} iterations for processing ID: {options.ProcessingId}");
                        return (currentBytes, (int)currentImage.Width, (int)currentImage.Height);
                    }

                    iterations++;
                    var currentWidth = (int)currentImage.Width;
                    var currentHeight = (int)currentImage.Height;

                    var fileSizeRatio = (double)MaxImageSizeBytes / currentBytes.Length;
                    var scaleFactor = Math.Sqrt(fileSizeRatio) * scaleMargin;

                    var newWidth = Math.Max(minDimension, (int)(currentWidth * scaleFactor));
                    var newHeight = Math.Max(minDimension, (int)(currentHeight * scaleFactor));

                    // Ensure even numbers for better encoding
                    newWidth = (newWidth / 2) * 2;
                    newHeight = (newHeight / 2) * 2;

                    if (newWidth < minDimension || newHeight < minDimension)
                    {
                        //Logger.LogWarning($"Reached minimum dimensions ({newWidth}x{newHeight}) but image still too large for processing ID: {options.ProcessingId}");
                        break;
                    }

                    //Logger.LogInformation($"Iteration {iterations}: Downsizing from {currentWidth}x{currentHeight} to {newWidth}x{newHeight} for processing ID: {options.ProcessingId}");

                    var newImage = await ImageUtils.DownsizeImage(currentImage, newWidth, newHeight);

                    // Dispose previous image if it's not the original
                    if (currentImage != sourceImage)
                    {
                        currentImage.Dispose();
                    }

                    currentImage = newImage;
                }

                var finalBytes = await currentImage.AsBytesAsync();
                if (finalBytes.Length > MaxImageSizeBytes)
                {
                    throw new InvalidOperationException($"Image too large ({finalBytes.Length} bytes) after {iterations} downsizing attempts for processing ID: {options.ProcessingId}");
                }

                return (finalBytes, (int)currentImage.Width, (int)currentImage.Height);
            }
            finally
            {
                if (currentImage != sourceImage)
                {
                    currentImage?.Dispose();
                }
            }
        }

        private async Task<CameraResultProcessingMessage?> CreateThumbnail(IImage image, CameraResultProcessingOptions options)
        {
            var start = DateTime.Now;

            if (string.IsNullOrWhiteSpace(options.ThumbnailPath))
            {
                return null;
            }

            using var thumbnail = await ImageHelper.DownsizeImage(image, ThumbSize);
            var thumbnailBytes = await thumbnail.AsBytesAsync();

            return new CameraResultProcessingMessage(options, DateTime.Now - start)
            {
                ThumbnailBytes = thumbnailBytes
            };
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    timer.Stop();
                    timer.Dispose();
                }

                disposedValue = true;
            }
        }

        private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (channel.Reader.Count > 0)
            {
                if (channel.Reader.TryRead(out var options))
                {
                    ProcessImage(options).ContinueWith(o =>
                    {
                        if (o.Exception is null)
                        {
                            timer.Enabled = channel.Reader.Count > 0;
                        }
                        else
                        {
                            //Logger.LogError(o.Exception.InnerException);
                        }
                    });
                }
            }
        }

        private async Task<IImage> Preprocess(CameraResultProcessingOptions options)
        {
            Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 1");
            using (SKManagedStream inputStream = new(options.SourceImage.AsStream()))
            {
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 2");
                using (var codec = SKCodec.Create(inputStream))
                {
                    Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 3");
                    using (var bitmap = SKBitmap.Decode(codec))
                    {

                        Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 4");
                        using (MemoryStream ms = new())
                        {
                            Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 5");
                            SKImage.FromBitmap(bitmap.AutoOrient(options.Rotation))
                                .Encode(SKEncodedImageFormat.Png, options.Quality)
                                .SaveTo(ms);

                            Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 6");
                            await ms.FlushAsync();

                            ms.Position = (int)SeekOrigin.Begin;

                            Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* Preprocess 7");
                            return PlatformImage.FromStream(ms);
                        }
                    }
                }
            }
        }

        private async Task ProcessImage(CameraResultProcessingOptions options)
        {
            IImage image = null;
            IImage thumbnailImage = null;

            try
            {
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage");
                image = await Preprocess(options);
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 2");

                // Create a copy for thumbnail if needed
                if (!string.IsNullOrWhiteSpace(options.ThumbnailPath))
                {
                    Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 3");
                    var imageBytes = await image.AsBytesAsync();
                    Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 4");
                    using var stream = new MemoryStream(imageBytes);
                    thumbnailImage = PlatformImage.FromStream(stream);
                    Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 5");
                }

                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 6");
                var attachmentTask = CreateAttachment(image, options);
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 7");
                var thumbnailTask = thumbnailImage != null ? CreateThumbnail(thumbnailImage, options) : Task.FromResult<CameraResultProcessingMessage?>(null);

                CameraResultProcessingMessage? attachmentResult = null;
                CameraResultProcessingMessage? thumbnailResult = null;
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage 8");

                try
                {
                    attachmentResult = await attachmentTask;
                }
                catch (Exception ex)
                {
                   // Logger.LogError(ex);
                }

                try
                {
                    thumbnailResult = await thumbnailTask;
                }
                catch (Exception ex)
                {
                    //Logger.LogError(ex);
                }

                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* ProcessImage Done - sending message");
                if (attachmentResult != null)
                {
                    //Logger.LogInformation($"Sending attachment result for processing ID: {options.ProcessingId}");
                    WeakReferenceMessenger.Default.Send(attachmentResult);
                }

                if (thumbnailResult != null)
                {
                    //Logger.LogInformation($"Sending thumbnail result for processing ID: {options.ProcessingId}");
                    WeakReferenceMessenger.Default.Send(thumbnailResult);
                }

                if (attachmentResult == null && thumbnailResult == null)
                {
                   // Logger.LogWarning($"No results to send for processing ID: {options.ProcessingId}");
                }
            }
            catch (Exception ex)
            {
                //Logger.LogError(ex);
            }
            finally
            {
                image?.Dispose();
                thumbnailImage?.Dispose();
            }
        }
    }
}
