using Java.Util.Streams;
using MauiCamera.Helpers;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Maui;
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
using static MetadataExtractor.Formats.Bmp.BmpHeaderDirectory;
using IImage = Microsoft.Maui.Graphics.IImage;

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
            //_channel = new CameraResultProcessingChannel(new ImageUtils());
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


        public static void SaveByteArrayToFileWithBinaryWriter(byte[] data, string filePath)
        {
            using var writer = new BinaryWriter(File.OpenWrite(filePath));
            writer.Write(data);
        }

       

        public async Task<bool> ChoosePhotoFromMediaPicker(string title, int selectionLimit, int? downsize)
        {
            bool result = false;
            CameraResults = new List<CameraResult>();

            try
            {
                var cts = new CancellationTokenSource();

                try
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(5));

                    var options = new MediaPickerOptions
                    {
                        Title = title,
                        SelectionLimit = selectionLimit,
                        PreserveMetaData = false,
                        RotateImage = false,
                    };

                    var fileResult = await MediaPicker.PickPhotosAsync(options);

                    foreach (var file in fileResult)
                    {

                        Stream imageStream = await file.OpenReadAsync();
                        IImage newImage = PlatformImage.FromStream(imageStream);
                        string fileNameWithExtension = file.FileName;
                        string contentType = "jpg";
                        string[] split = fileNameWithExtension.Split('.');

                        if (split.Length > 1)
                        {
                            contentType = split[1];
                        }

                        fileNameWithExtension = file.FileName;
                        string thumbnailFileNameWithExtension = split.Length > 1 ? $"{split[0]}_thumbnail.{split[1]}" : "thumbnail_" + file.FileName;

                        string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                        string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                        int rotation = 0;// await GenerateRotation(file);
                        DateTime? originalPhotoDateTime = await ExtractPhotoTimestamp(file);

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
                return await ChoosePhotoFromMediaPicker(string.Empty, 1, null);
        }

        public async Task<bool> SelectPhotoFromGallery(string title)
        {
                return await ChoosePhotoFromMediaPicker(title, 1, null);
        }

        public async Task<bool> SelectPhotoFromGallery(int downsize)
        {
                return await ChoosePhotoFromMediaPicker(string.Empty, 1, downsize);
        }

        public Task<bool> SelectPhotoFromGallery(string title, int selectionLimit, int? downsize)
        {
                return ChoosePhotoFromMediaPicker(title, selectionLimit, downsize);
        }

        public void SetProcessingMode(CameraHelperProcessingMode mode)
        {
            Mode = mode;
        }

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
                return await TakePhotoFromMediaPicker(string.Empty, null);
        }

        public async Task<bool> TakePhoto(string fileName)
        {
                return await TakePhotoFromMediaPicker(fileName, null);
        }

        public async Task<bool> TakePhoto(int downsize)
        {
                return await TakePhotoFromMediaPicker(null, downsize);
        }

        public async Task<bool> TakePhoto(string fileName, int? downsize = null)
        {
                return await TakePhotoFromMediaPicker(fileName, downsize);
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
                    var options = new MediaPickerOptions
                    {
                        SelectionLimit = 0,
                        PreserveMetaData = true, Title="Take a photo",
                        RotateImage = true,
                        CompressionQuality=100,
                        MaximumWidth=1000,
                        MaximumHeight=1000
                    };
                    FileResult file = await MediaPicker.Default.CapturePhotoAsync(options);
                    if (file != null)
                    {
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

                        //int rotation = await GenerateRotation(file);
                        byte[] imageData;

                       CameraResult camResult= await LoadPhotoAsync(file);

                        //using (Stream imageStream = await file.OpenReadAsync())
                        //{
                        //    imageSource = ImageSource.FromStream(() => imageStream);
                        //    image = PlatformImage.FromStream(imageStream);
                        //    imageData = image.AsBytes();
                        //    //using (FileStream localFileStream = File.OpenWrite(newFn))
                        //    //{
                        //    //    await imageStream.CopyToAsync(localFileStream);
                        //    //}
                        //}

                        //CameraResult camResult = new()
                        //{
                        //    PhotoImageBytes = imageData,
                        //    PhotoImageData = Convert.ToBase64String(imageData),
                        //    PhotoImageSource = imageSource,
                        //    //ThumbBytes = thumbnailBytes,
                        //    PhotoFileName = fileNameWithExtension,
                        //    PhotoFileSize = imageData.Length,
                        //    PhotoFileType = contentType,
                        //    PhotoHeight = (int)float.Round(image.Height),// image.Height,
                        //    PhotoWidth = (int)float.Round(image.Width),// compression.Width,
                        //    //Rotation = rotation,
                        //    PhotoFullPath = Path.GetDirectoryName(newFn),
                        //    OriginalPhotoDateTime = DateTime.Now
                        //};
                        CameraResults.Add(camResult);
                        result = true;

                        // Save device
                        //if (image != null && ShouldSavePhotoToDeviceGallery)
                        //{
                        //    try
                        //    {
                        //        await SavePhotoToGallery(image.AsBytes(), fileNameTobeSavedToDevice);
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        //Global.AppLogger.Instance.LogError(ex);
                        //        // soft fail
                        //    }
                        //}

                        //if (image != null)
                        //{
                        //    CameraResult cameraResult = await GenerateCameraResultAsync(image, rotation, downsize, newFn, thumbnailFn, contentType, fileNameWithExtension);

                        //    if (cameraResult != null)
                        //    {
                        //        CameraResults.Add(cameraResult);
                        //    }

                        //    result = true;

                        //}
                        //else
                        //{
                        //    await ShowToastMessage("TextResource.ImageCameraUnknownError");
                        //}
                    }
                    else
                        return false;
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

        async Task<CameraResult> LoadPhotoAsync(FileResult photo)
        {
            string imageDimensions = "";
            ImageSource PhotoSource;

            if (photo is null)
            {
                PhotoSource = null;
                imageDimensions = "";
                return null;
            }

            var stream = await photo.OpenReadAsync();
            {
               int rotation=await  GenerateRotation(stream);
                //int h = 0;
                //int w = 0;

                // Get image dimensions
                //try
                //{
                //    var imageInfo = GetImageDimensions(stream);
                //    h=imageInfo.Height;
                //    w=imageInfo.Width;
                //    imageDimensions = $"{imageInfo.Width} × {imageInfo.Height} • {stream.Length:N0} bytes";
                //    stream.Position = 0; // Reset stream position
                //}
                //catch
                //{
                //    imageDimensions = $"Unknown dimensions • {stream.Length:N0} bytes";
                //}

                PhotoSource = ImageSource.FromStream(() => stream);
                //long ImageByteLength = stream.Length;

                //ShowMultiplePhotos = false;
                //ShowPhoto = true;

                //using (Stream imageStream = await file.OpenReadAsync())
                //{
                //    imageSource = ImageSource.FromStream(() => imageStream);
                //    image = PlatformImage.FromStream(imageStream);
                //    imageData = image.AsBytes();
                //    //using (FileStream localFileStream = File.OpenWrite(newFn))
                //    //{
                //    //    await imageStream.CopyToAsync(localFileStream);
                //    //}
                //}


                using (var image = PlatformImage.FromStream(stream))
                {
                    var rotImage=CreateOrientedImage(image, rotation).Image;
                    var imageData = rotImage.AsBytes();

                    //var imageInfo = GetImageDimensions(stream);
                    int h = (int)rotImage.Height;
                    int w = (int)rotImage.Width;

                    string fileNameWithExtension = photo.FileName;
                    string fileNameTobeSavedToDevice = photo.FileName + "_Taken From PDSX";
                    string[] split = fileNameWithExtension.Split('.');
                    string thumbnailFileNameWithExtension = split.Length > 1 ? $"{split[0]}_thumbnail.{split[1]}" : "thumbnail_" + photo.FileName;
                    string contentType = "jpg";

                    if (split.Length > 1)
                    {
                        contentType = split[1];
                    }
                    string newFn = Path.Combine(FileSystem.Current.CacheDirectory, fileNameWithExtension);
                    string thumbnailFn = Path.Combine(FileSystem.Current.CacheDirectory, thumbnailFileNameWithExtension);

                    CameraResult camResult = new()
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
                        //Rotation = rotation,
                        PhotoFullPath = Path.GetDirectoryName(newFn),
                        OriginalPhotoDateTime = DateTime.Now
                    };
                    return camResult;
                }
            }
        }
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

        (int Width, int Height) GetImageDimensions2(Stream imageStream)
        {
            try
            {
                // Reset position to beginning of stream
                imageStream.Position = 0;

                // Use MAUI.Graphics to load the image and get dimensions
                using var image = PlatformImage.FromStream(imageStream);
                return ((int)image.Width, (int)image.Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract image dimensions: {ex.Message}");
                return (0, 0);
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

               // Global.AppLogger.Instance.LogInformation($"Photo saved to gallery: {fileName}");
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

        private Task<CameraResult> GenerateCameraResultAsync(IImage newImage, int rotation, int? downsize, string newFn, string thumbnailFn, string photoFileType, string fileNameWithExtension, DateTime? originalPhotoDateTime = null)
        {
            //if (Mode == CameraHelperProcessingMode.Deferred)
            //{
            //    return GenerateCameraResultDeferred(newImage, rotation, downsize, newFn, thumbnailFn, photoFileType, fileNameWithExtension, originalPhotoDateTime);
            //}
            return GenerateResult(newImage, rotation, downsize, newFn, thumbnailFn, photoFileType, fileNameWithExtension, originalPhotoDateTime);

            //return GenerateCameraResultImmediate(newImage, rotation, downsize, newFn, thumbnailFn, photoFileType, fileNameWithExtension, originalPhotoDateTime);
        }

        private async Task<CameraResult> GenerateResult(IImage newImage, int rotation, int? downsize, string newFn, string thumbnailFn, string photoFileType, string fileNameWithExtension, DateTime? originalPhotoDateTime)
        {
            CameraResult camResult = new()
            {
                //PhotoImageBytes = newImage.AsBytes(),
                ////PhotoImageData = Convert.ToBase64String(compression.Bytes),
                //PhotoImageSource = ImageSource.FromFile(newFn),
                ////ThumbBytes = thumbnailBytes,
                //PhotoFileName = fileNameWithExtension,
                //PhotoFileSize = compression.Bytes.Length,
                //PhotoFileType = contentType,
                //PhotoHeight = compression.Height,
                //PhotoWidth = compression.Width,
                //Rotation = rotation,
                //PhotoFullPath = Path.GetDirectoryName(newFn),
                //OriginalPhotoDateTime = originalPhotoDateTime
            };

            return camResult;
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

            //await _channel.WriteAsync(options);

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

            (IImage orientedImage, byte[] orientedBytes) = CameraCompressionHelper.CreateOrientedImage(newImage, rotation);

            byte[] thumbnailBytes = null;
            //if (IncludeThumbnail)
            //{
            //    IImage thumbnailImage = _imageUtils.DownsizeImage(orientedBytes, THUMB_SIZE, thumbnailFn);
            //    thumbnailBytes = thumbnailImage?.AsBytes();
            //    thumbnailImage?.Dispose();
            //}

            IImage workingImage = orientedImage;

            //if (downsize.HasValue)
            //{
            //    workingImage = _imageUtils.DownsizeImage(orientedBytes, downsize.Value, string.Empty);
            //}

            try
            {
                CompressionOutcome compression = CameraCompressionHelper.CompressToTargetSize(workingImage, ImageFormat.Jpeg, _imageUtils);

                await File.WriteAllBytesAsync(newFn, compression.Bytes);

                CameraResult camResult = new()
                {
                    PhotoImageBytes = compression.Bytes,
                    PhotoImageData = Convert.ToBase64String(compression.Bytes),
                    PhotoImageSource = ImageSource.FromFile(newFn),
                    ThumbBytes = thumbnailBytes,
                    PhotoFileName = fileNameWithExtension,
                    PhotoFileSize = compression.Bytes.Length,
                    PhotoFileType = contentType,
                    PhotoHeight = compression.Height,
                    PhotoWidth = compression.Width,
                    Rotation = rotation,
                    PhotoFullPath = Path.GetDirectoryName(newFn),
                    OriginalPhotoDateTime = originalPhotoDateTime
                };

                return camResult;
            }
            finally
            {
                if (workingImage != null && !ReferenceEquals(workingImage, orientedImage))
                {
                    workingImage.Dispose();
                }

                orientedImage.Dispose();
            }
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

        private async Task<int> GenerateRotation(Stream imageStream)
        {
            int rotation = 0;

            //using (Stream imageStream = await file.OpenReadAsync())
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
