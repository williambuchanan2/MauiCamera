using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
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
using PDS.App.Library.AppUtilities;
using PDS.App.Library.AppUtilities.Helpers;
using SkiaSharp;

namespace Global
{
    public sealed class CameraHelper
    {
        private const string LEFT_SIDE_BOTTOM_ROTATE_270 = "Left side, bottom (Rotate 270 CW)";

        private const string LEFT_SIDE_TOP_ROTATE_270 = "Left side, top (Mirror horizontal and rotate 270 CW)";

        private const string ROTATE_180 = "Bottom, right side (Rotate 180)";

        private const string ROTATE_90 = "Right side, top (Rotate 90 CW)";

        private const int THUMB_SIZE = 50;

        public const string KEYNAME_SAVE_PHOTOS_TO_GALLERY = "SavePhotosToGallery";

        private readonly CameraResultProcessingChannel _channel;

        private readonly ImageUtils _imageUtils;

        //private readonly InfoPrompts _infoPrompts;

        //private ImageEditPage _imageEditor;

        private bool ShouldSavePhotoToDeviceGallery => Preferences.Get(KEYNAME_SAVE_PHOTOS_TO_GALLERY, true);

        public CameraHelper()//CameraResultProcessingChannel channel, ImageUtils imageUtils)//, InfoPrompts infoPrompts)
        {
            _channel = new CameraResultProcessingChannel(new ImageUtils());
            _imageUtils = new ImageUtils();
            //_infoPrompts = infoPrompts;
        }

        public enum CameraHelperProcessingMode
        {
            /// <summary>
            /// Defer image conversion to a background process
            /// </summary>
            Deferred,
            /// <summary>
            /// Convert images immediately
            /// </summary>
            Immediate
        }

       // [ResolveFromContainer(typeof(CameraHelper))]
        public static CameraHelper Instance { get; set; }

        /// <summary>
        /// Indicates that a photo was successfully taken or selected. 
        /// False indicates user cancel or other issue
        /// </summary>
        public bool ActionSucceeded { get; set; }

        public List<CameraResult> CameraResults { get; set; }

        public bool IncludeThumbnail { get; set; } = false;

        public CameraHelperProcessingMode Mode { get; private set; } = CameraHelperProcessingMode.Deferred;

        public string OperationInfo { get; set; }

        public bool SwitchToMediaPicker { get; set; }

        public static void SaveByteArrayToFileWithBinaryWriter(byte[] data, string filePath)
        {
            using var writer = new BinaryWriter(File.OpenWrite(filePath));
            writer.Write(data);
        }

        public async Task<bool> ChoosePhotoFromMediaGallery(string title, int selectionLimit, int? downsize)
        {
            bool result = false;
            CameraResults = new List<CameraResult>();

            try
            {
                var cts = new CancellationTokenSource();

                try
                {
                    var request = new MediaPickRequest(selectionLimit, MediaFileType.Image, MediaFileType.Video)
                    {
                        PresentationSourceBounds = System.Drawing.Rectangle.Empty,
                        UseCreateChooser = true,
                        Title = title
                    };

                    cts.CancelAfter(TimeSpan.FromMinutes(5));


                    var fileResult = await MediaGallery.PickAsync(request, cts.Token);

                    if (fileResult != null)
                    {
                        List<IMediaFile> selFiles = fileResult.Files.ToList();
                        if (selFiles.Count > 0)
                        {
                            int i = 0;
                            foreach (IMediaFile selFile in selFiles)
                            {
                                Stream imageStream = await selFile.OpenReadAsync();
                                IImage newImage = PlatformImage.FromStream(imageStream);

                                string contentType = selFile.Extension;
                                string fileNameWithExtension = $"{selFile.NameWithoutExtension}.{selFile.Extension}";
                                string thumbnailFileNameWithExtension = $"{selFile.NameWithoutExtension}_thumbnail.{selFile.Extension}";

                                string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                                string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                                int rotation = await GenerateRotation(selFile);
                                DateTime? originalPhotoDateTime = await ExtractPhotoTimestamp(selFile);

                                if (newImage != null)
                                {
                                    CameraResult cameraResult = await GenerateCameraResultAsync(newImage, rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension, originalPhotoDateTime);
                                    if (cameraResult != null)
                                    {
                                        CameraResults.Add(cameraResult);
                                    }
                                    result = true;
                                    i++;
                                }
                            }
                            result = i >= selFiles.Count;
                        }
                        else
                            return false;
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

        public async Task<bool> ChoosePhotoFromMediaPicker(string title, int? downsize)
        {
            bool result = false;
            CameraResults = new List<CameraResult>();

            try
            {
                var cts = new CancellationTokenSource();

                try
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(5));

                    MediaPickerOptions options = new MediaPickerOptions();
                    options.Title = title;

                    var fileResult = await MediaPicker.PickPhotoAsync(options);

                    if (fileResult != null)
                    {

                        Stream imageStream = await fileResult.OpenReadAsync();
                        IImage newImage = PlatformImage.FromStream(imageStream);
                        string fileNameWithExtension = fileResult.FileName;
                        string contentType = "jpg";
                        string[] split = fileNameWithExtension.Split('.');

                        if (split.Length > 1)
                        {
                            contentType = split[1];
                        }

                        fileNameWithExtension = fileResult.FileName;
                        string thumbnailFileNameWithExtension = split.Length > 1 ? $"{split[0]}_thumbnail.{split[1]}" : "thumbnail_" + fileResult.FileName;

                        string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                        string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                        int rotation = await GenerateRotation(fileResult);
                        DateTime? originalPhotoDateTime = await ExtractPhotoTimestamp(fileResult);

                        if (newImage != null)
                        {
                            CameraResult cameraResult = await GenerateCameraResultAsync(newImage, rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension, originalPhotoDateTime);
                            if (cameraResult != null)
                            {
                                CameraResults.Add(cameraResult);
                            }
                            result = true;
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

        public void RotateImage(byte[] imageData, int Rotation)
        {
            Stream stream = new MemoryStream(imageData);
            using (var inputStream = new SKManagedStream(stream))
            {
                using (var codec = SKCodec.Create(inputStream))
                {
                    using (var bitmap = SKBitmap.Decode(codec))
                    {
                        SKBitmap newBitMap = AutoOrient(bitmap, Rotation);
                        SKImage image = SKImage.FromBitmap(newBitMap);
                        string imagePath = Path.Combine(FileSystem.CacheDirectory, "image.png");
                        SKData encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                        var bitmapImageStream = File.Open(imagePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        encodedData.SaveTo(bitmapImageStream);
                        bitmapImageStream.Flush(true);
                        bitmapImageStream.Dispose();
                        IImage newImage = PlatformImage.FromStream(encodedData.AsStream());
                        byte[] resultBytes = newImage.AsBytes();

                        CameraResults = new List<CameraResult>();
                        //CameraResult cameraResult =  GenerateCameraResult(newImage, Rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension);

                        CameraResult camResult = new CameraResult()
                        {
                            PhotoImageBytes = resultBytes,
                            PhotoImageData = Convert.ToBase64String(resultBytes),
                            //PhotoImageSource = ImageSource.FromFile(newFn),
                            //ThumbBytes = thumbnailBytes,
                            //PhotoFileName = fileNameWithExtension,
                            PhotoFileSize = resultBytes.Length,
                            //PhotoFileType = contentType,
                            PhotoHeight = (int)newImage.Height,
                            PhotoWidth = (int)newImage.Width,
                            Rotation = Rotation,
                            //PhotoFullPath = Path.GetDirectoryName(newFn)
                        };

                        if (camResult != null)
                            CameraResults.Add(camResult);
                    }
                }
            }
        }

        /// <summary>
        /// Select a photo from the image gallery
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SelectPhotoFromGallery()
        {
            if (SwitchToMediaPicker)
                return await ChoosePhotoFromMediaPicker(string.Empty, null);
            return await ChoosePhotoFromMediaGallery(string.Empty, 1, null);
        }

        public async Task<bool> SelectPhotoFromGallery(string title)
        {
            if (SwitchToMediaPicker)
                return await ChoosePhotoFromMediaPicker(title, null);
            return await ChoosePhotoFromMediaGallery(title, 1, null);
        }

        public async Task<bool> SelectPhotoFromGallery(int downsize)
        {
            if (SwitchToMediaPicker)
                return await ChoosePhotoFromMediaPicker(string.Empty, downsize);
            return await ChoosePhotoFromMediaGallery(string.Empty, 1, downsize);
        }

        public Task<bool> SelectPhotoFromGallery(string title, int selectionLimit, int? downsize)
        {
            if (SwitchToMediaPicker)
            {
                return ChoosePhotoFromMediaPicker(title, downsize);
            }

            return ChoosePhotoFromMediaGallery(title, selectionLimit, downsize);
        }

        public void SetProcessingMode(CameraHelperProcessingMode mode)
        {
            Mode = mode;
        }
        //public async Task<bool> ShowImageEditor(byte[] imageData, int width, int height, bool isReadOnly = false)
        //{
        //    _imageEditor = new ImageEditPage(this, imageData, width, height, isReadOnly);
        //    var x = await Application.Current?.Windows[0].Page.ShowPopupAsync(_imageEditor,
        //        new PopupOptions() { CanBeDismissedByTappingOutsideOfPopup = true });
        //    if (x == null) { }
        //    return _imageEditor.IsImageEdited;
        //}
        /// <summary>
        /// Gives the user the option to take or select a photo
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TakeOrSelectPhoto()
        {
            return await TakeOrSelectPhoto(string.Empty, string.Empty, null);
        }

        public async Task<bool> TakeOrSelectPhoto(string fileName)
        {
            return await TakeOrSelectPhoto(string.Empty, fileName, null);
        }

        public async Task<bool> TakeOrSelectPhoto(int downSize)
        {
            return await TakeOrSelectPhoto(string.Empty, string.Empty, downSize);
        }

        public async Task<bool> TakeOrSelectPhoto(string title, int downSize)
        {
            return await TakeOrSelectPhoto(title, string.Empty, downSize);
        }

        public async Task<bool> TakeOrSelectPhoto(string title, string fileName, int? downSize)
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
                            actionResult = await TakePhoto(fileName, downSize);
                        else
                            actionResult = await SelectPhotoFromGallery(title, 1, downSize);

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

        /// <summary>
        /// Take a photo with the camera
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TakePhoto()
        {
            if (SwitchToMediaPicker)
                return await TakePhotoFromMediaPicker(string.Empty, null);
            return await TakePhotoFromMediaGallery(string.Empty, null);
        }

        public async Task<bool> TakePhoto(string fileName)
        {
            if (SwitchToMediaPicker)
                return await TakePhotoFromMediaPicker(fileName, null);
            return await TakePhotoFromMediaGallery(fileName, null);
        }

        public async Task<bool> TakePhoto(int downsize)
        {
            if (SwitchToMediaPicker)
                return await TakePhotoFromMediaPicker(null, downsize);
            return await TakePhotoFromMediaGallery(null, downsize);
        }

        public async Task<bool> TakePhoto(string fileName, int? downsize = null)
        {
            if (SwitchToMediaPicker)
                return await TakePhotoFromMediaPicker(fileName, downsize);
            return await TakePhotoFromMediaGallery(fileName, downsize);
        }

        public async Task<bool> TakePhotoFromMediaGallery(string fileName, int? downsize = null)
        {
            bool result = false;
            CameraResults = new List<CameraResult>();

            try
            {
                if (MediaGallery.CheckCapturePhotoSupport())
                {
                    var status = await Permissions.RequestAsync<Permissions.Camera>();

                    if (status != PermissionStatus.Granted)
                        return false;


                    IImage newImage = null;

                    using (IMediaFile file = await MediaGallery.CapturePhotoAsync())
                    {
                        if (file != null)
                        {
#if IOS
                            await ShowToastMessage("TextResource.ImageWait");
#endif
#if ANDROID
                            await ShowLongToast("Image loading...");
#endif

                            string contentType = file.Extension;
                            string fileNameWithExtension = $"{file.NameWithoutExtension}.{file.Extension}";
                            string fileNameTobeSavedToDevice = $"{file.NameWithoutExtension}_Taken From PDSX.{file.Extension}";
                            string thumbnailFileNameWithExtension = $"{file.NameWithoutExtension}_thumbnail.{file.Extension}";

                            string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                            string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileNameWithExtension = $"{fileName}.{file.Extension}";
                                newFn = Path.Combine(FileSystem.Current.CacheDirectory, $"{fileName}.{file.Extension}");
                            }

                            int rotation = await GenerateRotation(file);

                            using (Stream imageStream = await file.OpenReadAsync())
                            {
                                newImage = PlatformImage.FromStream(imageStream);
                                using (FileStream localFileStream = File.OpenWrite(newFn))
                                {
                                    await imageStream.CopyToAsync(localFileStream);
                                }
                            }

                            // Save to device
                            if (newImage != null && ShouldSavePhotoToDeviceGallery)
                            {
                                try
                                {
                                    await SavePhotoToGallery(newImage.AsBytes(), fileNameTobeSavedToDevice);
                                }
                                catch (Exception ex)
                                {
                                   // Global.AppLogger.Instance.LogError(ex);
                                    // soft fail
                                }
                            }

                            if (newImage != null)
                            {
                                CameraResult cameraResult = await GenerateCameraResultAsync(newImage, rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension);

                                if (cameraResult != null)
                                {
                                    CameraResults.Add(cameraResult);
                                }
                                result = true;

                            }
                        }
                        else // Assume the user cancelled and not the result of an exception.
                            return false;
                    }
                }

                if (!result)
                    await ShowToastMessage("TextResource.ImageCameraUnknownError");
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
                await ShowToastMessage("TextResource.ImageCameraUnknownError");
            }

            return result;
        }

        public async Task<bool> TakePhotoFromMediaPicker(string fileName, int? downsize = null)
        {
            bool result = false;
            CameraResults = new List<CameraResult>();

            try
            {
                if (MediaGallery.CheckCapturePhotoSupport())
                {
                    var status = await Permissions.RequestAsync<Permissions.Camera>();

                    if (status != PermissionStatus.Granted)
                        return false;


                    IImage image = null;

                    //this else will be removed once MediaGallery will be fix for lower than android os 33
                    FileResult file = await MediaPicker.CapturePhotoAsync();
                    string fileNameWithExtension = file.FileName;
                    string fileNameTobeSavedToDevice = file.FileName + "_Taken From PDSX";
                    string[] split = fileNameWithExtension.Split('.');
                    string thumbnailFileNameWithExtension = split.Length > 1 ? $"{split[0]}_thumbnail.{split[1]}" : "thumbnail_" + file.FileName;
                    string contentType = "jpg";

                    if (split.Length > 1)
                    {
                        contentType = split[1];
                    }

                    string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                    string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);
                    ImageSource imageSource;

                    int rotation = await GenerateRotation(file);

                    using (Stream imageStream = await file.OpenReadAsync())
                    {
                        imageSource = ImageSource.FromStream(() => imageStream);
                        image = PlatformImage.FromStream(imageStream);

                        using (FileStream localFileStream = File.OpenWrite(newFn))
                        {
                            await imageStream.CopyToAsync(localFileStream);
                        }
                    }

                    // Save device
                    if (image != null && ShouldSavePhotoToDeviceGallery)
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

                    if (image != null)
                    {
                        CameraResult cameraResult = await GenerateCameraResultAsync(image, rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension);

                        if (cameraResult != null)
                        {
                            CameraResults.Add(cameraResult);
                        }

                        result = true;

                    }
                    else
                    {
                        await ShowToastMessage("TextResource.ImageCameraUnknownError");
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
               // Global.AppLogger.Instance.LogError(ex);
                await ShowToastMessage("TextResource.ImagePhotoError");
            }

            return result;
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
                var status = await Permissions.RequestAsync<Permissions.Photos>();

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

                //Global.AppLogger.Instance.LogInformation($"Photo saved to gallery: {fileName}");
                await ShowToastMessage("Photo saved to gallery");

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                //Global.AppLogger.Instance.LogError($"Error saving photo to gallery: {ex.Message}");
                throw;
            }
        }

        private static SKBitmap AutoOrient(SKBitmap bitmap, int rotation)
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

        private Task<CameraResult> GenerateCameraResultAsync(IImage newImage, int rotation, int? downsize, string newFn, string thumbnailFn, string photoFileType, string fileNameWithExtension, DateTime? originalPhotoDateTime = null)
        {
            if (Mode == CameraHelperProcessingMode.Deferred)
            {
                return GenerateCameraResultDeferred(newImage, rotation, downsize, newFn, thumbnailFn, photoFileType, fileNameWithExtension, originalPhotoDateTime);
            }

            return GenerateCameraResultImmediate(newImage, rotation, downsize, newFn, thumbnailFn, photoFileType, fileNameWithExtension, originalPhotoDateTime);
        }

        private async Task<CameraResult> GenerateCameraResultDeferred(IImage newImage, int rotation, int? downsize, string newFn, string thumbnailFn, string photoFileType, string fileNameWithExtension, DateTime? originalPhotoDateTime)
        {
            CameraResultProcessingOptions options = new()
            {
                Downsize = downsize,
                Path = newFn,
                PathWithExtension = fileNameWithExtension,
                PhotoFileType = photoFileType,
                Rotation = rotation,
                SourceImage = newImage,
                ThumbnailPath = thumbnailFn,
                OriginalPhotoDateTime = originalPhotoDateTime
            };

            await _channel.WriteAsync(options);

            // return an interim result to the client
            CameraResult value = new()
            {
                PhotoFileName = options.PathWithExtension,
                PhotoFileSize = 0,
                PhotoFileType = options.PhotoFileType,
                PhotoFullPath = Path.GetDirectoryName(options.Path),
                PhotoHeight = (int)options.SourceImage.Height,
                //#if IOS
                //                PhotoImageBytes = await newImage.AsBytesAsync(),

                //#elif Android
                //                PhotoImageBytes = null,
                //#endif
                PhotoImageBytes = null,
                PhotoImageData = null,
                PhotoImageSource = ImageSource.FromFile(options.Path),
                PhotoWidth = (int)options.SourceImage.Width,
                ProcessingId = options.ProcessingId,
                Rotation = rotation,
                ThumbBytes = null,
                OriginalPhotoDateTime = originalPhotoDateTime
            };

            return value;
        }

        private async Task<CameraResult> GenerateCameraResultImmediate(IImage newImage, int rotation, int? downsize, string newFn, string thumbnailFn, string contentType, string fileNameWithExtension, DateTime? originalPhotoDateTime = null)
        {
#if IOS
            await ShowToastMessage("TextResource.ImageWait");
#endif
#if ANDROID
            await ShowLongToast("Image loading...");
#endif
            await Task.Delay(250);

            int height = (int)newImage.Height;
            int width = (int)newImage.Width;

            using (var inputStream = new SKManagedStream(newImage.AsStream()))
            {
                using (var codec = SKCodec.Create(inputStream))
                {
                    using (var bitmap = SKBitmap.Decode(codec))
                    {
                        SKBitmap newBitMap = AutoOrient(bitmap, rotation);
                        SKImage image = SKImage.FromBitmap(newBitMap);
                        string imagePath = Path.Combine(FileSystem.CacheDirectory, "image.png");
                        SKData encodedData = image.Encode(SKEncodedImageFormat.Png, 80);
                        var bitmapImageStream = File.Open(imagePath,
                                                      FileMode.Create,
                                                      FileAccess.Write,
                                                      FileShare.None);
                        encodedData.SaveTo(bitmapImageStream);
                        bitmapImageStream.Flush(true);
                        bitmapImageStream.Dispose();
                        newImage = PlatformImage.FromStream(encodedData.AsStream());
                    }
                }
            }

            if (downsize != null)
                newImage = _imageUtils.DownsizeImage(newImage.AsBytes(), downsize.Value, newFn);

            IImage thumbnail;
            byte[] thumbnailBytes = null;

            if (IncludeThumbnail)
            {
                thumbnail = _imageUtils.DownsizeImage(newImage.AsBytes(), THUMB_SIZE, thumbnailFn);
                thumbnailBytes = thumbnail.AsBytes();
            }

            height = (int)newImage.Height;
            width = (int)newImage.Width;

            bool isLandScape = width > height;

            int heightPercentage = (int)(height * (80f / 100f));
            int widthPercentage = (int)(width * (80f / 100f));

            byte[] resultBytes = newImage.AsBytes();

            while (resultBytes.Length > 5000000 && height > 1 && width > 1)
            {
                height -= heightPercentage;
                width -= widthPercentage;

                if ((isLandScape && (rotation == 0 || rotation == 180)) || (rotation == 90 || rotation == 270))
                {
                    int tempHeight = height;
                    int tempWidth = width;
                    height = tempWidth;
                    width = tempHeight;
                }

                newImage = _imageUtils.DownsizeImage(newImage.AsBytes(), height, width, newFn);
                resultBytes = newImage.AsBytes();
            }

            CameraResult camResult = new CameraResult()
            {
                PhotoImageBytes = resultBytes,
                PhotoImageData = Convert.ToBase64String(resultBytes),
                PhotoImageSource = ImageSource.FromFile(newFn),
                ThumbBytes = thumbnailBytes,
                PhotoFileName = fileNameWithExtension,
                PhotoFileSize = resultBytes.Length,
                PhotoFileType = contentType,
                PhotoHeight = height,
                PhotoWidth = width,
                Rotation = rotation,
                PhotoFullPath = Path.GetDirectoryName(newFn),
                OriginalPhotoDateTime = originalPhotoDateTime
            };

            return camResult;
        }
        private async Task<int> GenerateRotation(IMediaFile file)
        {
            int rotation = 0;

            using (Stream imageStream = await file.OpenReadAsync())
            {
                var meta = ImageMetadataReader.ReadMetadata(imageStream);
                var subIfd0Directory = meta.OfType<ExifIfd0Directory>().FirstOrDefault();
                string orientation = subIfd0Directory?.GetDescription(ExifDirectoryBase.TagOrientation);
                //Console.WriteLine($"orientation :{orientation}");

                if (orientation != null)
                {
                    switch (orientation)
                    {
                        case ROTATE_90:
                            rotation = 90;
                            break;
                        case ROTATE_180:
                            rotation = 180;
                            break;
                        case LEFT_SIDE_TOP_ROTATE_270:
                        case LEFT_SIDE_BOTTOM_ROTATE_270:
                            rotation = 270;
                            break;
                    }
                }
            }
            return rotation;
        }

        private async Task<int> GenerateRotation(FileResult file)
        {
            int rotation = 0;

            using (Stream imageStream = await file.OpenReadAsync())
            {
                var meta = ImageMetadataReader.ReadMetadata(imageStream);

                var subIfd0Directory = meta.OfType<ExifIfd0Directory>().FirstOrDefault();
                string orientation = subIfd0Directory?.GetDescription(ExifDirectoryBase.TagOrientation);
                //Console.WriteLine($"orientation :{orientation}");

                if (orientation != null)
                {
                    switch (orientation)
                    {
                        case ROTATE_90:
                            rotation = 90;
                            break;
                        case ROTATE_180:
                            rotation = 180;
                            break;
                        case LEFT_SIDE_TOP_ROTATE_270:
                        case LEFT_SIDE_BOTTOM_ROTATE_270:
                            rotation = 270;
                            break;

                    }

                }
            }
            return rotation;
        }

        private async Task ShowLongToast(string errorMesssage)
        {
            //await Global.PDSToast.Instance.ShowLongToast(errorMesssage);
        }

        private async Task ShowToastMessage(string errorMessage)
        {
            //await Global.PDSToast.Instance.ShowToast(errorMessage);
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
               // Global.AppLogger.Instance.LogError($"Error Extracting datetime info from attached photo named {file.NameWithoutExtension}: {ex}");
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
            }

            return null;
        }
        #endregion
    }
}