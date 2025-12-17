using MauiCamera.Helpers;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using NativeMedia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace Global
{
    public sealed class MediaHelper
    {
        private static class RotateOptions
        {
            public const string LEFT_SIDE_BOTTOM_ROTATE_270 = "Left side, bottom (Rotate 270 CW)";
            public const string LEFT_SIDE_TOP_ROTATE_270 = "Left side, top (Mirror horizontal and rotate 270 CW)";
            public const string ROTATE_180 = "Bottom, right side (Rotate 180)";
            public const string ROTATE_90 = "Right side, top (Rotate 90 CW)";
        }

        private const int THUMB_SIZE = 50;
        public const string KEYNAME_SAVE_PHOTOS_TO_GALLERY = "SavePhotosToGallery";

        private bool _savePhotoToDeviceGallery => Preferences.Get(KEYNAME_SAVE_PHOTOS_TO_GALLERY, true);

        // [ResolveFromContainer(typeof(CameraHelper))]
        public static MediaHelper Instance { get; set; }

        /// <summary>
        /// Indicates that a photo was successfully taken or selected. 
        /// False indicates user cancel or other issue
        /// </summary>
        public bool ActionSucceeded { get; set; }

        public List<MediaResult> MediaResults { get; set; }

        public bool IncludeThumbnail { get; set; } = false;

        public string OperationInfo { get; set; }
        public string FilenamePrefix { get; set; } = "App_";


        public MediaHelper()
        {
        }

        public async Task<bool> TakeOrSelectPhoto(string title)
        {
            try
            {
                bool result = false;
                string takePhoto = "Take Photo";
                string selectPhoto = "Select Photo";
                string cancel = "Cancel";
                bool take = true;

                var action = await Application.Current.Windows[0].Page.DisplayActionSheet(null, cancel, null, new[] { takePhoto, selectPhoto });
                if (action != null)
                {
                    if (action.Equals(cancel))
                        return false;
                    else if (action.Equals(takePhoto))
                        take = true;
                    else
                        take = false;

                    try
                    {
                        bool actionResult = false;

                        if (take)
                            actionResult = await TakePhoto(title);
                        else
                            actionResult = await SelectPhotoFromGallery(title, 0);

                        ActionSucceeded = actionResult;
                        result = actionResult;
                    }
                    catch (Exception ex)
                    {
                        //Global.AppLogger.Instance.LogError(ex);
                        throw;
                    }
                }
                return result;

            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError(ex);
                throw;
            }
        }

        public async Task<bool> SelectPhotoFromGallery(string title, int selectionLimit)
        {
            bool result = false;
            MediaResults = new List<MediaResult>();
            if (string.IsNullOrEmpty(title))
                title = "Select Image";

            try
            {
                var cts = new CancellationTokenSource();

                try
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(5));
                    var options = GetDefaultMediaPickerOptions(title);
                    var fileResult = await MediaPicker.PickPhotosAsync(options);

                    foreach (var file in fileResult)
                    {
                        if (file != null)
                        {
                            MediaResult camResult = await LoadPhotoAsync(file, false);
                            MediaResults.Add(camResult);
                            result = true;
                        }
                        else
                        {
                            await ShowToastMessage("TextResource.ImageCameraUnknownError");
                            return false;
                        }
                    }

                    if (!result)
                        await ShowToastMessage("TextResource.ImageUnknownError");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError(ex);
                await ShowToastMessage("TextResource.ImageSelectError");
            }
            return result;
        }

        public async Task<bool> TakePhoto(string title)
        {
            bool result = false;
            MediaResults = new List<MediaResult>();
            if (string.IsNullOrEmpty(title))
                title = "Take Photo";

            try
            {
                if (MediaGallery.CheckCapturePhotoSupport())
                {
                    var status = await Permissions.RequestAsync<Permissions.Camera>();

                    if (status != PermissionStatus.Granted)
                        return false;

                    var options = GetDefaultMediaPickerOptions(title);
                    FileResult file = await MediaPicker.Default.CapturePhotoAsync(options);

                    if (file != null)
                    {
                        MediaResult camResult = await LoadPhotoAsync(file, _savePhotoToDeviceGallery);
                        MediaResults.Add(camResult);
                        result = true;
                    }
                    else
                    {
                        await ShowToastMessage("TextResource.ImageCameraUnknownError");
                        return false;
                    }
                }
            }
            catch (FeatureNotSupportedException)
            {
                await ShowToastMessage("Feature is not supported on the device");
            }
            catch (FeatureNotEnabledException)
            {
                await ShowToastMessage("Feature is not enabled on the device");
            }
            catch (PermissionException)
            {
                await ShowToastMessage("Permission denied");
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError(ex);
                await ShowToastMessage("TextResource.ImagePhotoError");
            }

            return result;
        }

        private MediaPickerOptions GetDefaultMediaPickerOptions(string title)
        {
            return new MediaPickerOptions
            {
                SelectionLimit = 0,
                PreserveMetaData = true,
                Title = title,
                RotateImage = false,
                CompressionQuality = 50,
                MaximumWidth = 700,
                MaximumHeight = 700
            };
        }

        private async Task<MediaResult> LoadPhotoAsync(FileResult photo, bool saveToGallery)
        {
            if (photo is null)
                return null;

            ImageSource PhotoSource;

            using (var stream = await photo.OpenReadAsync())
            {
                int rotation = await GenerateRotation(stream);
                PhotoSource = ImageSource.FromStream(() => stream);

                using (var image = PlatformImage.FromStream(stream))
                {
                    IImage rotImage;
                    byte[] imageData;//= rotImage.AsBytes();
                    (rotImage, imageData) = CreateOrientedImage(image, rotation);

                    int h = (int)rotImage.Height;
                    int w = (int)rotImage.Width;

                    string fileNameWithExtension = photo.FileName;
                    string fileNameTobeSavedToDevice = $"{FilenamePrefix}{photo.FileName}";
                    string[] split = fileNameWithExtension.Split('.');
                    string thumbnailFileNameWithExtension = split.Length > 1 ? $"{split[0]}_thumbnail.{split[1]}" : "thumbnail_" + photo.FileName;
                    string contentType = "jpg";

                    if (split.Length > 1)
                        contentType = split[1];

                    string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                    string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                    // Save to cache
                    bool saveToCache = false; // May be required at some point in time
                    if (saveToCache)
                        File.WriteAllBytes(newFn, imageData);


                    MediaResult camResult = new()
                    {
                        PhotoImageBytes = imageData,
                        PhotoImageData = Convert.ToBase64String(imageData),
                        PhotoImageSource = PhotoSource,
                        //ThumbBytes = thumbnailBytes,
                        PhotoFileName = fileNameWithExtension,
                        PhotoFileSize = imageData.Length,
                        PhotoFileType = contentType,
                        PhotoHeight = h,
                        PhotoWidth = w,
                        Rotation = rotation,
                        PhotoFullPath = saveToCache ? Path.GetDirectoryName(newFn) : fileNameWithExtension,
                        OriginalPhotoDateTime = DateTime.Now
                    };


                    if (image != null && saveToGallery)
                    {
                        try
                        {
                            await SavePhotoToGallery(image.AsBytes(), fileNameTobeSavedToDevice);
                        }
                        catch (Exception ex)
                        {
                            //Global.AppLogger.Instance.LogError(ex);
                            // soft fail
                        }
                    }

                    return camResult;
                }
            }
        }

        internal static (IImage Image, byte[] Bytes) CreateOrientedImage(IImage source, int rotation)
        {
            using var managedStream = new SKManagedStream(source.AsStream());
            using var codec = SKCodec.Create(managedStream);
            using var bitmap = SKBitmap.Decode(codec);
            SKBitmap oriented = MediaHelper.AutoOrient(bitmap, rotation);

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


        #region private methods

        /// <summary>
        /// When using cam, also saving photo to device
        /// </summary>
        /// <param name="imageBytes"> Image data </param>
        /// <param name="fileName"> Filename </param>
        private async Task SavePhotoToGallery(byte[] imageBytes, string fileName)
        {
            try
            {
                var status = await Permissions.RequestAsync<SaveMediaPermission>();

                if (status != PermissionStatus.Granted)
                {
                    //Global.AppLogger.Instance.LogWarning("Photos permission not granted, cannot save to gallery");
                    return;
                }

                string tempPath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(tempPath, imageBytes);

                using (var fileStream = File.OpenRead(tempPath))
                {
                    await MediaGallery.SaveAsync(MediaFileType.Image, fileStream, fileName);
                }

                // Global.AppLogger.Instance.LogInformation($"Photo saved to gallery: {fileName}");
                await ShowToastMessage("Photo saved to gallery");

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError($"Error saving photo to gallery: {ex.Message}");
                throw;
            }
        }

        internal static SKBitmap AutoOrient(SKBitmap bitmap, int rotation)
        {
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


        private async Task<int> GenerateRotation(Stream imageStream)
        {
            int rotation = 0;
            var meta = ImageMetadataReader.ReadMetadata(imageStream);

            var subIfd0Directory = meta.OfType<ExifIfd0Directory>().FirstOrDefault();
            string orientation = subIfd0Directory?.GetDescription(ExifDirectoryBase.TagOrientation);
            //Console.WriteLine($"orientation :{orientation}");

            if (orientation != null)
            {
                switch (orientation)
                {
                    case RotateOptions.ROTATE_90:
                        rotation = 90;
                        break;
                    case RotateOptions.ROTATE_180:
                        rotation = 180;
                        break;
                    case RotateOptions.LEFT_SIDE_TOP_ROTATE_270:
                    case RotateOptions.LEFT_SIDE_BOTTOM_ROTATE_270:
                        rotation = 270;
                        break;
                }
            }

            imageStream.Position = 0;
            return rotation;
        }

        private async Task ShowLongToast(string errorMesssage)
        {
            InfoPrompts.ShowLongToast(errorMesssage);
        }

        private async Task ShowToastMessage(string errorMessage)
        {
            InfoPrompts.ShowLongToast(errorMessage);
        }

        private async Task<DateTime?> ExtractPhotoTimestamp(IMediaFile file)
        {
            try
            {
                using (Stream imageStream = await file.OpenReadAsync())
                {
                    var meta = ImageMetadataReader.ReadMetadata(imageStream);
                    var detailedExif = meta.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                    if (detailedExif?.HasTagName(ExifDirectoryBase.TagDateTimeOriginal) == true)
                    {
                        if (detailedExif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime originalDateTime))
                        {
                            return originalDateTime;
                        }
                    }

                    var basicExif = meta.OfType<ExifIfd0Directory>().FirstOrDefault();

                    if (basicExif?.HasTagName(ExifDirectoryBase.TagDateTime) == true)
                    {
                        if (basicExif.TryGetDateTime(ExifDirectoryBase.TagDateTime, out DateTime dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError($"Error Extracting datetime info from attached photo named {file.NameWithoutExtension}: {ex}");
                throw;
            }

            return null;
        }

        private async Task<DateTime?> ExtractPhotoTimestamp(FileResult file)
        {
            try
            {
                using (Stream imageStream = await file.OpenReadAsync())
                {
                    var meta = ImageMetadataReader.ReadMetadata(imageStream);
                    var exifSubIfd = meta.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                    if (exifSubIfd?.HasTagName(ExifDirectoryBase.TagDateTimeOriginal) == true)
                    {
                        if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime originalDateTime))
                        {
                            return originalDateTime;
                        }
                    }

                    var exifIfd0 = meta.OfType<ExifIfd0Directory>().FirstOrDefault();

                    if (exifIfd0?.HasTagName(ExifDirectoryBase.TagDateTime) == true)
                    {
                        if (exifIfd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out DateTime dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError($"Error Extracting datetime info from attached photo named {file.FileName}: {ex}");
                throw;
            }

            return null;
        }

        #endregion
    }
}
