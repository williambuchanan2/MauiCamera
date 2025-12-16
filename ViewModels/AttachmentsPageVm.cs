using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Global;
using MauiCamera.Helpers;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MauiCamera.ViewModels
{
    public partial class AttachmentsPageVm : BaseViewModel
    {

        private const int MaxImagePixelSize = 700;

        private List<AttachedImage> _attachedImagesList = new List<AttachedImage>();
        private string _entityType;
        private int _parentEntityId;
        private Guid _parentMobileLocalId;
        public string OperationInfo { get; set; }
        public string PhotoPath { get; set; }
        private AttachedImage SelectedForEdit { get; set; }
        public class ReRenderMessage { };


        //public ObservableRangeCollection<AttachedImageViewModel> AttachedImages { get; set; } = [];
        [ObservableProperty]
        public ObservableCollection<AttachedImageViewModel> _attachedImages;// { get; set; } = [];

        [ObservableProperty]
        private bool isAuditPage;

        [ObservableProperty]
        private bool isBackButtonVisible;

        [ObservableProperty]
        private bool isReadOnly;

        [ObservableProperty]
        private bool loadingImg;

        [ObservableProperty]
        private bool noAttachments;

        [ObservableProperty]
        private bool isReferenceButtonEnabled;

        [ObservableProperty]
        private SelectionMode currentSelectionMode = SelectionMode.None;

        [ObservableProperty]
        private ObservableCollection<object> currentSelectedItems = new();

        [ObservableProperty]
        private bool showDragDropInfo;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private bool isPhotoSavedLocallyEnabled;


        public AttachmentsPageVm()
        {
            _attachedImages = new ObservableCollection<AttachedImageViewModel>();
            if (!WeakReferenceMessenger.Default.IsRegistered<CameraResultProcessingMessage>(this))
            {
                WeakReferenceMessenger.Default.Register<CameraResultProcessingMessage>(this, (_, message) =>
                {
                    OnCameraResultProcessingMessage(message);
                });
            }
        }

        [RelayCommand]
        private void Appearing()
        {
            try
            {
                if (_attachedImagesList?.Count == 0)
                {
                    Task.Run(async () =>
                    {
                        //await GenerateAttachedImagesFromEALs();
                        ShowOrHideNoAttachmentsIndicator();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        public override async void PageAppearing()
        {
            try
            {
                //AppSettingsManager.EventLogger().AddScreenEnteredEvent("AttachmentPageVm");
                //AppSettingsManager.EventLogger().AddEvent("Started - GenerateAttachedImagesFromEALs");
                isPhotoSavedLocallyEnabled = true;// AppSettingsManager.SharedSettings().PhotoSavedLocally();

                if (_attachedImagesList?.Count == 0)
                {
                    //await GenerateAttachedImagesFromEALs();
                    ShowOrHideNoAttachmentsIndicator();
                    CountInitialAttachedImages();
                }

                //AppSettingsManager.EventLogger().AddEvent("Finished - GenerateAttachedImagesFromEALs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void ShowOrHideNoAttachmentsIndicator()
        {
            if (_attachedImagesList.Count == 0)
                noAttachments = true;
            else
                noAttachments = false;
        }

        private void CountInitialAttachedImages()
        {
            //_initialAttachedImagesCount = _attachedImagesList.Count();
        }

        [RelayCommand]
        private async Task AddAttachment()
        {
            //AppSettingsManager.EventLogger().AddButtonPressedEvent("Add Attachment");

            string fromGallery = "Choose Photo";// _localizedStringsRepo.PromptChoosePhoto;
            string fromCamera = "Take Photo";// _localizedStringsRepo.PromptTakePhoto;
            string stepInfoPhoto = "Step";// PDSXResources.PromptStepInfoPhoto;

            bool isLinkAttachmentVisible = currentSelectionMode == SelectionMode.Multiple || isReferenceButtonEnabled;

            string[] options = isLinkAttachmentVisible
                ? new[] { fromCamera, fromGallery, stepInfoPhoto }
                : new[] { fromCamera, fromGallery };

            string action = await Application.Current.Windows[0].Page.DisplayActionSheet(null, "Cancel", null, options);

            if (action == fromCamera)
            {
                //AppSettingsManager.EventLogger().AddButtonPressedEvent(fromCamera);
                await ShowCamera();
            }
            else if (action == fromGallery)
            {
                //AppSettingsManager.EventLogger().AddButtonPressedEvent(fromGallery);
                await ShowPhotoGallery();
            }

        }

        public void OnCameraResultProcessingMessage(CameraResultProcessingMessage message)
        {
            var match = _attachedImagesList.FirstOrDefault(o => o.ProcessingId == message.Options.ProcessingId);

            if (match is not null)
            {
                if (message.ThumbnailBytes is not null)
                {
                    match.ThumbnailInByteArray = message.ThumbnailBytes;
                }

                if (message.AttachmentBytes is not null)
                {
                    match.AttachmentInByteArray = message.AttachmentBytes;
                    match.PhotoHeight = message.Height.Value;
                    match.PhotoWidth = message.Width.Value;

                    RefreshAttachedImages();
                }
            }
        }

        private void ProcessCameraResults(CameraHelper ch)
        {
            int count = ch.CameraResults.Count;

            if (count > 0)
            {
                string message = "TextResource.ImageConversionSingle";

                if (count > 1)
                {
                    message = string.Format("TextResource.ImageConversionMany", [count]);
                }

                // InfoPrompts.Instance.ShowLongToast(message);


                ch.CameraResults.ForEach(result =>
                {
                    var attachedImage = new AttachedImage
                    {
                        AttachmentInByteArray = result.PhotoImageBytes,
                        CreatedDateTime = result.OriginalPhotoDateTime ?? DateTime.Now,
                        EntityMobileLocalId = _parentMobileLocalId,
                        EntityType = _entityType,
                        FileName = result.PhotoFileName,
                        FileSize = result.PhotoFileSize,
                        FileType = result.PhotoFileType,
                        FullPath = result.PhotoFullPath,
                        ImageSource = result.PhotoImageSource,
                        //IsEditButtonEnabled = attachmentButtonStateHelper.IsEditButtonEnabled(isCreateNew: true),
                        // IsDeleteButtonEnabled = attachmentButtonStateHelper.IsDeleteButtonEnabled(isCreateNew: true),
                        ParentEntityId = _parentEntityId,
                        ProcessingId = result.ProcessingId,
                        Status = AttachedImageStatus.New,
                        ThumbnailInByteArray = result.ThumbBytes,
                        PhotoHeight = result.PhotoHeight,
                        PhotoWidth = result.PhotoWidth
                    };

                    _attachedImagesList.Add(attachedImage);
                });
                RefreshAttachedImages();
            }

            ShowOrHideNoAttachmentsIndicator();
        }

        private void RefreshAttachedImages()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    //isSaveToolbarItemEnabled = !isReadOnly && _attachedImagesList.Where(c => c.Status != AttachedImageStatus.Deleted).All(c => c.AttachmentInByteArray != null) && _attachedImagesList.Count() > 0;
                    isReferenceButtonEnabled = false;// await attachmentButtonStateHelper.IsReferenceDocumentButtonEnabled(_parentEntityId, _parentMobileLocalId, _entityType);

                    WeakReferenceMessenger.Default.Send(new ReRenderMessage());
                });

                var list = _attachedImagesList
                         .Where(x => x.Status != AttachedImageStatus.Deleted)
                         .OrderByDescending(x => x.CreatedDateTime)
                       .Select(o =>
                       {
                           //o.ImageSource = ImageSource.FromFile("edit.png");
                           var item = new AttachedImageViewModel(o);

                           if (isReadOnly)
                           {
                               //item.Model.IsActionButtonEnabled = false;
                               item.Model.IsEditButtonEnabled = false;//attachmentButtonStateHelper.IsEditButtonEnabled(isReadOnly: IsReadOnly);
                               item.Model.IsDeleteButtonEnabled = false;//attachmentButtonStateHelper.IsDeleteButtonEnabled(isReadOnly: IsReadOnly);
                           }

                           return item;
                       })
                    .ToList();

                //TODO
                //AttachedImages.ReplaceRange(list);
                Debug.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: ******* RefreshAttachedImages");
                foreach (var attachedImage in list)
                {
                    AttachedImages.Add(attachedImage);
                }
            }
            catch (Exception ex)
            {
                // Global.MauiAppExceptionHandler.Instance.HandleException(ex);
            }
        }


        private async Task ShowCamera()
        {
            Global.CameraHelper.Instance = new Global.CameraHelper();

            await CaptureImage(async (ch) => await ch.TakePhoto(string.Empty, MaxImagePixelSize));

        }

        private void ShowErrorMessage(string errorMessage)
        {
            MainPage.DisplayAlert("_localizedStringsRepo.CommonError", errorMessage, "OK");
        }

        private async Task ShowPhotoGallery()
        {
            await CaptureImage(async (ch) => await ch.SelectPhotoFromGallery(string.Empty, 1, MaxImagePixelSize));

        }

        private async Task CaptureImage(Func<CameraHelper, Task<bool>> captureAction)
        {
            try
            {
                LoadingImg = true;
                //App.DisableLoadState = true;

                var ch = CameraHelper.Instance;
                var success = await captureAction(ch);

                if (success)
                {
                    ProcessCameraResults(ch);
                }
                else
                {
                    if (!string.IsNullOrEmpty(ch.OperationInfo))
                    {
                        InfoPrompts.ShowLongToast(ch.OperationInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                //MauiAppExceptionHandler.Instance.HandleException(ex);
                ShowErrorMessage($"Camera error: {ex.Message}");
            }
            finally
            {
                // App.DisableLoadState = false;
                LoadingImg = false;
            }
        }

    

    }
}