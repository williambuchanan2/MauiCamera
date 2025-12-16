using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Global;
using MauiCamera.Helpers;
using MauiCamera.Model;
using MauiCamera.Views;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MauiCamera.ViewModels
{
    public partial class AttachmentsPageVm : BaseViewModel
    {


        public const string EntityType = nameof(EntityType);
        public const string EntityTypeName = nameof(EntityTypeName);
        public const string ParentEntityId = nameof(ParentEntityId);
        public const string ParentMobileLocalId = nameof(ParentMobileLocalId);
        public const string ReadOnly = "IsReadOnly";
        public const string ReturnToAuditPage = nameof(ReturnToAuditPage);
        private const int MaxImagePixelSize = 700;

        private List<AttachedImage> _attachedImagesList = new List<AttachedImage>();
        private List<AttachedImage> _selectedLinkAttachment = new List<AttachedImage>();
        private int _initialAttachedImagesCount;

        private string _entityType;

        //private readonly AttachmentButtonStateHelper attachmentButtonStateHelper;
        //private readonly AttachmentService attachmentService;
        //private LocalizedStringsRepository _localizedStringsRepo;

        private int _parentEntityId;

        private Guid _parentMobileLocalId;

        private bool _returnToAuditPage;

        //public ObservableRangeCollection<AttachedImageViewModel> AttachedImages { get; set; } = [];
        public ObservableCollection<AttachedImageViewModel> AttachedImages { get; set; } = [];

        [ObservableProperty]
        private bool isAuditPage;

        [ObservableProperty]
        private bool isBackButtonVisible;

        [ObservableProperty]
        private bool isReadOnly;

        [ObservableProperty]
       // [NotifyCanExecuteChangedFor(nameof(SaveAttachmentsCommand))]
        private bool isSaveToolbarItemEnabled;

        [ObservableProperty]
        private bool loadingImg;

        public string OperationInfo { get; set; }
        [ObservableProperty]
        private bool noAttachments;

        private bool CanSaveAttachments() => isSaveToolbarItemEnabled;


        [ObservableProperty]
        private bool isReferenceButtonEnabled;

        public bool showLinkAttachmentButton;

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

        // Constructors

        public AttachmentsPageVm()
        {
            if (!WeakReferenceMessenger.Default.IsRegistered<CameraResultProcessingMessage>(this))
            {
                WeakReferenceMessenger.Default.Register<CameraResultProcessingMessage>(this, (_, message) =>
                {
                    OnCameraResultProcessingMessage(message);
                });
            }
        }
        public string PhotoPath { get; set; }

        private AttachedImage SelectedForEdit { get; set; }

        public enum ImageSamplingQuality
        {
            Low,
            Medium,
            Mitchell,
            CatMullRom
        }

        public static SKSamplingOptions GetSamplingOptions(ImageSamplingQuality quality)
        {
            return quality switch
            {
                ImageSamplingQuality.Low => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.Nearest),
                ImageSamplingQuality.Medium => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                ImageSamplingQuality.Mitchell => new SKSamplingOptions(SKCubicResampler.Mitchell),
                ImageSamplingQuality.CatMullRom => new SKSamplingOptions(SKCubicResampler.CatmullRom),
                _ => SKSamplingOptions.Default
            };
        }

        // Inherited Methods

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            object parentEntityTypeIdValue = string.Empty;
            object entityTypeValue = string.Empty;
            object parentMobileLocalIdValue = string.Empty;
            object fromImageRotateValue = string.Empty;
            object returnToRootPage = string.Empty;
            string title = null;

            if (query.TryGetValue(ParentEntityId, out parentEntityTypeIdValue))
            {
                int.TryParse((string)parentEntityTypeIdValue, out _parentEntityId);

                if (query.TryGetValue(EntityType, out entityTypeValue))
                {
                    _entityType = (string)entityTypeValue;
                }

                title = $"Select {_entityType} Attachment";
            }

            object entityTypeName;

            if (query.TryGetValue(EntityTypeName, out entityTypeName))
            {
                entityTypeName = Uri.UnescapeDataString(entityTypeName.ToString());
                title = $"{entityTypeName} Attachments";
            }

            if (query.TryGetValue(ParentMobileLocalId, out parentMobileLocalIdValue))
            {
                Guid.TryParse((string)parentMobileLocalIdValue, out _parentMobileLocalId);

                if (query.TryGetValue(EntityType, out entityTypeValue))
                {
                    _entityType = (string)entityTypeValue;

                    //if (_entityType == AttachmentEntityType.IdlerSchematic || _entityType == AttachmentEntityType.Schematic)
                    //{
                    //    title = _localizedStringsRepo.CommonSchematics;
                    //    IsReadOnly = true;
                    //}
                    //else if (string.IsNullOrWhiteSpace(title))
                    {
                        title = "_localizedStringsRepo.CommonAttachments";
                        isReadOnly = false;
                    }
                }
            }

            Title = title;

            if (query.TryGetValue(nameof(isReadOnly), out object? value))
            {
                if (bool.TryParse(value?.ToString() ?? string.Empty, out bool isReadOnly))
                {
                   isReadOnly = isReadOnly;
                }
            }

            if (query.TryGetValue(ReturnToAuditPage, out returnToRootPage))
            {
                bool.TryParse((string)returnToRootPage, out _returnToAuditPage);

                isAuditPage = true;
                isBackButtonVisible = false;
            }
            else
            {
                if (currentSelectionMode == SelectionMode.Multiple)
                {
                    isBackButtonVisible = false;
                }
                else
                {
                    isBackButtonVisible = true;
                }

                _ = Task.Delay(1000);
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
            {
                noAttachments = true;
            }
            else
            {
                noAttachments = false;
            }
        }

        private void CountInitialAttachedImages()
        {
            _initialAttachedImagesCount = _attachedImagesList.Count();
        }

        private bool CanNavigateAway()
        {
            var hasUnsavedChanges = _attachedImagesList.Any(attachment =>
                attachment.Status == AttachedImageStatus.New ||
                attachment.Status == AttachedImageStatus.Edited ||
                attachment.Status == AttachedImageStatus.Deleted);

            // Fixe Save > delete flow
            var noChangesInCount = _initialAttachedImagesCount == _attachedImagesList.Count();

            return !hasUnsavedChanges && noChangesInCount;
        }

        private static byte[] SubSampleImageToByteArray(byte[] originalBytes, int maxWidth)
        {
            using var inputStream = new MemoryStream(originalBytes);
            using var codec = SKCodec.Create(inputStream);
            if (codec == null)
                return originalBytes;

            var orientation = codec.EncodedOrigin;
            using var original = SKBitmap.Decode(codec);
            if (original == null)
                return originalBytes;

            int originalWidth = original.Width;
            int originalHeight = original.Height;

            float ratio = (float)maxWidth / originalWidth;
            int newWidth = maxWidth;
            int newHeight = (int)(originalHeight * ratio);

            var samplingOptions = GetSamplingOptions(ImageSamplingQuality.Medium);
            using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions);
            if (resized == null)
                return originalBytes;

            SKBitmap rotated;

            switch (orientation)
            {
                case SKEncodedOrigin.RightTop: // 90° CW
                    rotated = new SKBitmap(resized.Height, resized.Width);
                    using (var canvas = new SKCanvas(rotated))
                    {
                        canvas.Translate(rotated.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(resized, 0, 0);
                    }
                    break;

                case SKEncodedOrigin.BottomRight: // 180°
                    rotated = new SKBitmap(resized.Width, resized.Height);
                    using (var canvas = new SKCanvas(rotated))
                    {
                        canvas.Translate(rotated.Width, rotated.Height);
                        canvas.RotateDegrees(180);
                        canvas.DrawBitmap(resized, 0, 0);
                    }
                    break;

                case SKEncodedOrigin.LeftBottom: // 270° CW (or 90° CCW)
                    rotated = new SKBitmap(resized.Height, resized.Width);
                    using (var canvas = new SKCanvas(rotated))
                    {
                        canvas.Translate(0, rotated.Height);
                        canvas.RotateDegrees(270);
                        canvas.DrawBitmap(resized, 0, 0);
                    }
                    break;

                default: // Unknown
                    rotated = resized;
                    break;
            }

            using var image = SKImage.FromBitmap(rotated);
            using var output = new MemoryStream();
            // 70 = compression quality, lower = smaller file size
            // if < 70 pls use CubicResampler via SamplingQuality.Mitchell or SamplingQuality.CatmullRom
            image.Encode(SKEncodedImageFormat.Jpeg, 70).SaveTo(output);

            return output.ToArray();
        }

        private async Task RestoreDeletedItems()
        {
            await Task.Run(() =>
            {
                foreach (var o in _attachedImagesList.Where(x => x.Status == AttachedImageStatus.Deleted))
                {
                    // Restore
                    o.Status = o.AttachmentId.HasValue ? AttachedImageStatus.Saved : AttachedImageStatus.New;
                }
            });

            RefreshAttachedImages();
            ShowOrHideNoAttachmentsIndicator();
        }

        // ICommands
        [RelayCommand]
        private async Task BackButton()
        {
            if (!CanNavigateAway())
            {
                bool answer = true;//  await Application.Current.Windows[0].Page.DisplayAlert("You have unsaved changes", "Do you wish to save changes first?", _localizedStringsRepo.PromptYes, _localizedStringsRepo.PromptNo);
                if (answer)
                {
                    //await SaveAttachments();
                }
                else
                {
                    await RestoreDeletedItems();
                    NavigationUtil.GoBack("IsFromAttachmentsPage=true");
                }
            }
            else
            {
                NavigationUtil.GoBack("IsFromAttachmentsPage=true");
            }
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

            string action = await Application.Current.Windows[0].Page.DisplayActionSheet(
                null, "Cancel", null, options);

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
            else if (action == stepInfoPhoto)
            {
                //AppSettingsManager.EventLogger().AddButtonPressedEvent(stepInfoPhoto);
                await LinkAttachment();
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

        [RelayCommand]
        private async Task DeleteAttachment(AttachedImageViewModel attachedImage)
        {
            // AppSettingsManager.EventLogger().AddButtonPressedEvent("DeleteAttachment");
            LogAttachmentHistory("Delete", attachedImage.Model);
            attachedImage.Model.Status = AttachedImageStatus.Deleted;

            // defer this until save #12163
            // await attachmentService.DeleteAttachmentsAndEals(attachedImage.Model);

            // Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachmentsDeleted);
            RefreshAttachedImages();
            ShowOrHideNoAttachmentsIndicator();
        }

        //private async Task<int> DeleteAttachmentFromRepo(AttachedImage attachedImage)
        //{
        //    if (attachedImage.LocalPrimaryKeyId <= 0 && attachedImage.ParentEntityId > 0)
        //    {

        //        //add on the table
        //        var id = await _attachmentsRepo.InsertAttachedImage(attachedImage);
        //        attachedImage.LocalPrimaryKeyId = id;
        //        return 1;
        //    }
        //    else if (attachedImage.LocalPrimaryKeyId <= 0 && attachedImage.ParentEntityId <= 0)
        //    {
        //        return await _attachmentsRepo.DeleteAttachedImage(attachedImage);

        //    }

        //    return await _attachmentsRepo.SoftDeleteAttachedImage(attachedImage);
        //}

        [RelayCommand]
        private async Task ImageTapped(AttachedImageViewModel attachedImage)
        {
            await HandleImageInteraction(attachedImage, isEditMode: false);
        }

        [RelayCommand]
        private async Task EditAttachment(AttachedImageViewModel attachedImage)
        {
            await HandleImageInteraction(attachedImage, isEditMode: true);
        }

        [RelayCommand]
        private void DismissInfoBanner()
        {
            ShowDragDropInfo = false;
        }



        // Methods
        [RelayCommand]
        private async Task LinkAttachment()
        {
            try
            {
                if (currentSelectionMode == SelectionMode.None)
                {
                    currentSelectionMode = SelectionMode.Multiple;
                    isBackButtonVisible = false;
                    showDragDropInfo = true;
                    showLinkAttachmentButton = true;
                }
                else
                {
                    if (currentSelectedItems != null)
                    {
                        currentSelectedItems
                            .OfType<AttachedImageViewModel>()
                            .ToList()
                            .ForEach(item =>
                            {
                                item.Model.IsReferenceDocument = false;
                                item.Model.Status = AttachedImageStatus.New;
                                item.Model.IsReadOnly = false;
                                item.Model.EntityMobileLocalId = _parentMobileLocalId;
                                item.Model.PreviousAttachmentID = item.Model.AttachmentId;
                                item.Model.AttachmentLocalPrimaryKeyId = null;
                                item.Model.EALLocalPrimaryKeyId = null;
                            });

                        _selectedLinkAttachment.AddRange(
                            currentSelectedItems
                                .OfType<AttachedImageViewModel>()
                                .Select(item => item.Model)
                        );
                    }

                    currentSelectionMode = SelectionMode.None;
                    isBackButtonVisible = true;
                    showDragDropInfo = false;
                    showLinkAttachmentButton = false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                currentSelectedItems.Clear();
                //await GenerateAttachedImagesFromEALs();
                ShowOrHideNoAttachmentsIndicator();
            }
        }

        [RelayCommand]
        private async Task CancelLinkAttachment()
        {
            currentSelectedItems.Clear();
            currentSelectionMode = SelectionMode.None;
            isBackButtonVisible = true;
            showDragDropInfo = false;
            showLinkAttachmentButton = false;
            // await GenerateAttachedImagesFromEALs();
            ShowOrHideNoAttachmentsIndicator();
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

        private List<AttachedImage> savedNewAttachments = new List<AttachedImage>();

        //private async Task GenerateAttachedImagesFromEALs()
        //{
        //    if (CurrentSelectionMode == SelectionMode.Multiple && _selectedLinkAttachment.Count == 0)
        //    {
        //        savedNewAttachments = _attachedImagesList.Where(x => x.Status == AttachedImageStatus.New || x.Status == AttachedImageStatus.Edited).ToList();
        //    }

        //    _attachedImagesList.Clear();

        //    var isAttachmentPageCreateMode = false; ;// Preferences.Get(PDSXPreferences.IsAttachmentPageCreateMode, true);

        //    // Load EALs
        //    var links = await GetFilteredEALs();
        //    var processedAttachmentIds = new List<int>();

        //    foreach (var eal in links)
        //    {
        //        var attachment = null;// await _attachmentsRepo.GetAttachment(eal.AttachmentId, eal.AttachmentMobileLocalId);
        //        if (attachment == null) continue;

        //        var imageData = GetValidImageData(attachment);
        //        if (imageData == null) continue;

        //        var attachedImage = await CreateAttachedImage(attachment, eal, imageData);
        //        await ProcessAttachment(attachedImage, processedAttachmentIds, isAttachmentPageCreateMode);
        //    }

        //    RestoreSelections();
        //    AddSelectedLinkAttachment();
        //    RefreshAttachedImages();
        //}

        /// <summary>
        /// Restore previously saved new attachments when not in multiple selection mode
        /// </summary>
        private void RestoreSelections()
        {
            //Do not restore in multiple selection mode
            if (currentSelectionMode == SelectionMode.Multiple)
                return;

            var existingIds = _attachedImagesList.Select(x => x.AttachmentLocalPrimaryKeyId).ToHashSet();

            foreach (var saved in savedNewAttachments)
            {
                // Check if this item already exists in the list
                //when saved.AttachmentLocalPrimaryKeyId is null., it means it's a new attachment added in this session
                if (!existingIds.Contains(saved.AttachmentLocalPrimaryKeyId) || !saved.AttachmentLocalPrimaryKeyId.HasValue)
                {
                    _attachedImagesList.Add(saved);
                    existingIds.Add(saved.AttachmentLocalPrimaryKeyId); // Track that we added it
                }
            }
        }

        /// <summary>
        /// Add selected link attachments to the main list that is not yet added
        /// </summary>
        private void AddSelectedLinkAttachment()
        {
            if (currentSelectionMode != SelectionMode.None)
                return;

            _selectedLinkAttachment.ForEach(c => c.IsDeleteButtonEnabled = true);

            var newAttachments = _selectedLinkAttachment
                .Where(newItem => !_attachedImagesList.Any(existing => existing.PreviousAttachmentID == newItem.AttachmentId))
                .ToList();

            _attachedImagesList.AddRange(newAttachments);

            _selectedLinkAttachment.Clear();
        }

        //private async Task<List<EALEntity>> GetFilteredEALs()
        //{
        //    var links = await _attachmentsRepo.GetLinks(_parentEntityId, _parentMobileLocalId, _entityType);

        //    return _entityType switch
        //    {
        //        nameof(AuditStep) when CurrentSelectionMode == SelectionMode.None =>
        //            links.Where(c => c.IsReferenceDocument == false || !c.IsReferenceDocument.HasValue).ToList(),
        //        nameof(AuditStep) when CurrentSelectionMode == SelectionMode.Multiple =>
        //            links.Where(c => c.IsReferenceDocument == true).ToList(),
        //        nameof(AuditStepDefinition) =>
        //            links.ToList(), //Where(c => c.IsReferenceDocument == true)
        //        _ => links
        //    };
        //}

        private static byte[] GetValidImageData(AttachmentEntity attachment)
        {
            return attachment.Data?.Length > 0 ? attachment.Data :
                   attachment.LargeThumbnail?.Length > 0 ? attachment.LargeThumbnail :
                   attachment.Thumbnail?.Length > 0 ? attachment.Thumbnail :
                   null;
        }

        private async Task<AttachedImage> CreateAttachedImage(AttachmentEntity attachment, EALEntity eal, byte[] imageData)
        {
            var hasAttachmentsLinkedToParent = await CheckIfAttachmentsLinkedToParent(eal.AttachmentId);
            var isPDF = false;// attachment.FileType == FileTypes.Pdf;
            var imageSource = CreateImageSource(imageData, isPDF);

            return new AttachedImage
            {
                AttachmentId = attachment.AttachmentId,
                AttachmentInByteArray = imageData,
                //AttachmentMobileLocalId = attachment.MobileLocalId,
                CreatedDateTime = attachment.CreatedDateTime ?? DateTime.Now,
                EntityMobileLocalId = eal.EntityMobileLocalId,
                EntityType = _entityType,
                FileName = attachment.FileName,
                FileSize = attachment.FileSize,
                FileType = attachment.FileType,
                ImageSource = imageSource,
                IsEditButtonEnabled = false,// attachmentButtonStateHelper.IsEditButtonEnabled(hasAttachmentsLinkedToParent: true, isPDF: isPDF),
                IsDeleteButtonEnabled = false,// attachmentButtonStateHelper.IsDeleteButtonEnabled(hasAttachmentsLinkedToParent: hasAttachmentsLinkedToParent, isReadOnly: IsReadOnly),
                ParentEntityId = _parentEntityId,
                Status = AttachedImageStatus.Saved,
                ThumbnailInByteArray = attachment.Thumbnail,
                IsPDF = isPDF,
                AttachmentLocalPrimaryKeyId = attachment.LocalPrimaryKeyId,
                IsReadOnly = hasAttachmentsLinkedToParent || IsReadOnly,
                IsReferenceDocument = eal.IsReferenceDocument ?? false,
                PreviousAttachmentID = attachment.PreviousAttachmentId,
                EALLocalPrimaryKeyId = eal.LocalPrimaryKeyId
            };
        }

        private async Task<bool> CheckIfAttachmentsLinkedToParent(int attachmentId)
        {
            return false;
            //return _entityType switch
            //{
            //    nameof(AuditStep) => await _attachmentsRepo.IsAttachmentsFromAuditStepDefinition(_parentEntityId, attachmentId),
            //    nameof(AuditStepDefinition) => true,
            //    _ => false
            //};
        }

        private static ImageSource CreateImageSource(byte[] imageData, bool isPDF)
        {
            if (isPDF) return null;

            var converter = new ByteArrayToImageSourceConverter();
            return converter.ConvertFrom(imageData);
        }

        private async Task ProcessAttachment(AttachedImage attachedImage, List<int> processedAttachmentIds, bool isAttachmentPageCreateMode)
        {
            //var existingAttachment = await _attachmentsRepo.GetAttachedImageByAttachmentIdOrMobileLocalId(
            //    attachedImage.AttachmentId.Value,
            //    attachedImage.AttachmentMobileLocalId);

            //if (existingAttachment != null && _entityType != nameof(AuditStepDefinition)) //we should only add the incoming AttachedImage only when entityType is not auditstepdefinition
            //{
            //    processedAttachmentIds.Add(existingAttachment.LocalPrimaryKeyId);
            //    SetButtonStates(existingAttachment, isAttachmentPageCreateMode);
            //    _attachedImagesList.Add(existingAttachment);

            //    if (existingAttachment.Status == AttachedImageStatus.New)
            //    {
            //        _attachedImagesList.Add(attachedImage);
            //    }
            //}
            //else
            //{
            //SetButtonStates(attachedImage, isAttachmentPageCreateMode);
            _attachedImagesList.Add(attachedImage);
            //}
        }

        private void SetButtonStates(AttachedImage attachedImage, bool isAttachmentPageCreateMode)
        {
            attachedImage.IsEditButtonEnabled = false;// attachmentButtonStateHelper.IsEditButtonEnabled(isCreateNew: isAttachmentPageCreateMode);
            attachedImage.IsDeleteButtonEnabled = false;// attachmentButtonStateHelper.IsDeleteButtonEnabled(isCreateNew: isAttachmentPageCreateMode);
        }

        private async Task AddUnprocessedAttachedImages(List<int> processedAttachmentIds, bool isAttachmentPageCreateMode)
        {
            // if (_entityType == nameof(AuditStepDefinition) || CurrentSelectionMode == SelectionMode.Multiple)
            //     return;
            //TODO
            //var unprocessedAttachedImages = await _attachmentsRepo.GetAttachedImages(
            //    _parentEntityId,
            //    _parentMobileLocalId,
            //    _entityType,
            //    processedAttachmentIds);

            //foreach (var attachedImage in unprocessedAttachedImages)
            //{
            //    attachedImage.IsActionButtonEnabled = isAttachmentPageCreateMode && !IsReadOnly;
            //    attachedImage.IsEditButtonEnabled = attachmentButtonStateHelper.IsEditButtonEnabled(
            //        isCreateNew: isAttachmentPageCreateMode,
            //        isReadOnly: IsReadOnly);
            //}

            //_attachedImagesList.AddRange(unprocessedAttachedImages);
        }

        private async Task HandleImageInteraction(AttachedImageViewModel attachedImage, bool isEditMode)
        {
            try
            {
                if (attachedImage == null || attachedImage.Model.AttachmentInByteArray == null)
                    return;

                string eventName = isEditMode ? "Edit Attachment" : "Image Tapped";
                // AppSettingsManager.EventLogger().AddButtonPressedEvent(eventName);

                var ch = Global.CameraHelper.Instance;
                SelectedForEdit = attachedImage.Model;

                int screenWidth = (int)(DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
                int screenHeight = (int)(DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density);

                //if (isEditMode)
                //{
                //    //var navigationParameter = new Dictionary<string, object>
                //    //{
                //    //    { AttachmentEditPageVm.AttachedImage, SelectedForEdit },
                //    //    { AttachmentEditPageVm.EntityMobileLocalId, _parentMobileLocalId }
                //    //};

                //    WeakReferenceMessenger.Default.Unregister<AnnotationProcessCompletedMessage>(this);

                //    WeakReferenceMessenger.Default.Register<AnnotationProcessCompletedMessage>(this, async void (recipient, messages) =>
                //    {
                //        var result = messages.Value as List<AttachedImage>;

                //        if (result != null)
                //        {
                //            result.ForEach(item =>
                //            {
                //                var matched = _attachedImagesList.FirstOrDefault(listItem => listItem.AnnotationGuid == item.AnnotationGuid);

                //                if (matched != null)
                //                {
                //                    matched.AttachmentInByteArray = matched.AttachmentInByteArray;
                //                    matched.ThumbnailInByteArray = matched.ThumbnailInByteArray;
                //                    if ((matched.AttachmentLocalPrimaryKeyId ?? 0) == 0)
                //                    {
                //                        // Keep current status when AttachmentLocalPrimaryKeyId is null or 0
                //                        matched.Status = matched.Status;
                //                    }
                //                    else
                //                    {
                //                        // Only change to Edited if current status is not New
                //                        matched.Status = matched.Status != AttachedImageStatus.New
                //                            ? AttachedImageStatus.Edited
                //                            : matched.Status;
                //                    }
                //                }

                //                if (matched == null)
                //                {
                //                    _attachedImagesList.Add(item);

                //                    //AttachedImageSavedMessage message = new()
                //                    //{
                //                    //    Image = matched
                //                    //};

                //                    //WeakReferenceMessenger.Default.Send(message);
                //                }
                //            });
                //        }
                //        LogAttachmentHistory("Edit", attachedImage.Model);
                //        RefreshAttachedImages();
                //    });

                //    await NavigationUtil.Navigate<AttachmentEditPage>(navigationParameter);

                //    //bool edited = await ch.ShowImageEditor(attachedImage.Model.AttachmentInByteArray, screenWidth, screenHeight);
                //    //if (edited && ch.CameraResults.Count > 0)
                //    //{
                //    //    CameraResult cr = ch.CameraResults[0];
                //    //    attachedImage.Model.AttachmentInByteArray = cr.PhotoImageBytes;

                //    //    if (attachedImage.Model.Status == AttachedImageStatus.Saved && attachedImage.Model.LocalPrimaryKeyId > 0)
                //    //    {
                //    //        attachedImage.Model.Status = AttachedImageStatus.Edited;
                //    //    }

                //    //    LogAttachmentHistory("Edit", attachedImage.Model);
                //    //    RefreshAttachedImages();
                //    //}
                //}
                //else
                //{
                //    await ch.ShowImageEditor(attachedImage.Model.AttachmentInByteArray, screenWidth, screenHeight, isReadOnly: true);
                //}
            }
            catch (Exception ex)
            {
                //Global.MauiAppExceptionHandler.Instance.HandleException(ex);
            }
        }

        private static void LogAttachmentHistory(string eventStr, AttachedImage attachedImage)
        {
            if (attachedImage!.AttachmentId != null)
            {
                //AppSettingsManager.EventLogger().AddEvent($"{eventStr} attachment id:{attachedImage.AttachmentId}");
            }
            else if (attachedImage!.AttachmentMobileLocalId != null)
            {
                //AppSettingsManager.EventLogger().AddEvent($"{eventStr} attachment mobile local id:{attachedImage.AttachmentMobileLocalId}");
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
                    isSaveToolbarItemEnabled = !isReadOnly && _attachedImagesList.Where(c => c.Status != AttachedImageStatus.Deleted).All(c => c.AttachmentInByteArray != null) && _attachedImagesList.Count() > 0;
                    isReferenceButtonEnabled = false;// await attachmentButtonStateHelper.IsReferenceDocumentButtonEnabled(_parentEntityId, _parentMobileLocalId, _entityType);

                    WeakReferenceMessenger.Default.Send(new ReRenderMessage());
                });

                var list = _attachedImagesList
                         .Where(x => x.Status != AttachedImageStatus.Deleted)
                         .OrderByDescending(x => x.CreatedDateTime)
                       .Select(o =>
                       {
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
                AttachedImages.Add(list[list.Count-1]);
            }
            catch (Exception ex)
            {
                // Global.MauiAppExceptionHandler.Instance.HandleException(ex);
            }
        }
        
        [RelayCommand]
        private async Task ReturnToAuditModule()
        {
            //await NavigateToAuditSafely();
        }


        //[RelayCommand(CanExecute = nameof(CanSaveAttachments))]
        //private async Task SaveAttachments()
        //{
        //   // AppSettingsManager.EventLogger().AddButtonPressedEvent("SaveAttachments");
        //    List<int> primaryKeys = [];

        //    foreach (AttachedImage attachedImage in _attachedImagesList)
        //    {
        //        int saved = 0;
        //        int deleted = 0;

        //        switch (attachedImage.Status)
        //        {
        //            case AttachedImageStatus.New:
        //                {
        //                    //check if its duplicated
        //                    var existing = await _attachmentsRepo.GetAttachedImageByLocalPrimaryKeyId(attachedImage.LocalPrimaryKeyId);

        //                    if (existing != null)
        //                    {
        //                        primaryKeys.Add(existing.LocalPrimaryKeyId);
        //                        break;
        //                    }

        //                    // Save attached image to repo
        //                    saved = await _attachmentsRepo.InsertAttachedImage(attachedImage);

        //                    // Check if save failed
        //                    if (saved < 1)
        //                    {
        //                        LogAttachmentHistory("Failed - Save new", attachedImage);
        //                        attachedImage.Status = AttachedImageStatus.New;
        //                        Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachedImageSaveFailed);
        //                        break;
        //                    }
        //                    else
        //                    {
        //                        LogAttachmentHistory("Success - Save new", attachedImage);
        //                        primaryKeys.Add(saved);
        //                    }
        //                }
        //                break;

        //            case AttachedImageStatus.Edited:
        //                {
        //                    //check if its duplicated
        //                    var existing = await _attachmentsRepo.GetAttachedImageByLocalPrimaryKeyId(attachedImage.LocalPrimaryKeyId);

        //                    if (existing == null)
        //                    {
        //                        //if not found, check whether the record exists on AttachmentEntity
        //                        existing = await _attachmentsRepo.GetAttachedImageByAttachmentIdOrMobileLocalId(attachedImage.AttachmentId.Value, attachedImage.AttachmentMobileLocalId);

        //                        if (existing == null)
        //                        {
        //                            // Save attached image to repo
        //                            saved = await _attachmentsRepo.InsertAttachedImage(attachedImage);

        //                            // Check if save failed
        //                            if (saved < 1)
        //                            {
        //                                LogAttachmentHistory("Failed - Save Edited", attachedImage);
        //                                attachedImage.Status = AttachedImageStatus.Edited;
        //                                Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachedImageSaveFailed);
        //                                break;
        //                            }
        //                            else
        //                            {
        //                                LogAttachmentHistory("Success - Save Edited", attachedImage);
        //                                primaryKeys.Add(saved);
        //                            }

        //                            break;
        //                        }
        //                        else
        //                        {
        //                            //This should never happen, attachment does not exist on AttachedImage and AttachmentEntity
        //                            Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachedImageSaveFailed);
        //                        }
        //                    }

        //                    // Save attached image to repo
        //                    saved = await _attachmentsRepo.UpdateAttachedImage(attachedImage);

        //                    // Check if save failed
        //                    if (saved < 1)
        //                    {
        //                        LogAttachmentHistory("Failed - Save Edited", attachedImage);
        //                        attachedImage.Status = AttachedImageStatus.Edited;
        //                        Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachedImageSaveFailed);
        //                        break;
        //                    }
        //                    else
        //                    {
        //                        LogAttachmentHistory("Success - Save Edited", attachedImage);
        //                        primaryKeys.Add(saved);
        //                    }
        //                }
        //                break;

        //            case AttachedImageStatus.Saved:
        //                break;

        //            case AttachedImageStatus.Deleted:
        //                {
        //                    await attachmentService.DeleteAttachmentsAndEals(attachedImage);
        //                    deleted = await DeleteAttachmentFromRepo(attachedImage);

        //                    if (attachedImage.LocalPrimaryKeyId > 0)
        //                    {
        //                        LogAttachmentHistory("Success - saving deleted", attachedImage);
        //                        primaryKeys.Add(attachedImage.LocalPrimaryKeyId);
        //                    }
        //                }
        //                break;
        //        }

        //        if (saved > 0)
        //        {
        //            AttachedImageSavedMessage message = new()
        //            {
        //                Image = attachedImage
        //            };

        //            WeakReferenceMessenger.Default.Send(message);
        //        }

        //        if (saved > 0 || deleted > 0)
        //        {
        //            WeakReferenceMessenger.Default.Send(new DirtyStateChangedMessage(true)); // Notify dirty state change for attachment)
        //        }
        //    }

        //    if (primaryKeys.Count > 0)
        //    {
        //        Global.InfoPrompts.Instance.ShowQuickToast(_localizedStringsRepo.AttachmentsSaved);
        //        AppSettingsManager.EventLogger().AddEvent("Exiting Attachment after saving changes");

        //        if (_returnToAuditPage)
        //        {
        //            await NavigateToAuditSafely();
        //        }
        //        else
        //            await NavigationUtil.GoBack("HasAttachments=true&IsFromAttachmentsPage=true");
        //    }
        //    else
        //    {
        //        AppSettingsManager.EventLogger().AddEvent("Exiting Attachment without changes");

        //        if (_returnToAuditPage)
        //        {
        //            await NavigateToAuditSafely();
        //            //await Shell.Current.GoToAsync($"//AuditsPage", animate: false);
        //        }
        //        else
        //            await NavigationUtil.GoBack("IsFromAttachmentsPage=true");
        //    }

        //}

        //private async Task NavigateToAuditSafely()
        //{
        //    try
        //    {
        //        await Task.Delay(50);

        //        await Shell.Current.GoToAsync("//Module/Audit", animate: false);
        //    }
        //    catch (Exception ex)
        //    {
        //        MauiAppExceptionHandler.Instance.HandleException(ex);
        //    }
        //}

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

        [RelayCommand]
        private async Task ViewPDFTapped(AttachedImageViewModel model)
        {
            // AppSettingsManager.EventLogger().AddButtonPressedEvent("ViewPDFTapped");

            // await NavigationUtil.Navigate<PDFViewerPage>($"{PDFViewerPageVm.AttachmentQueryId}={model.Model.AttachmentLocalPrimaryKeyId}");
        }

        public class ReRenderMessage { };

        [ObservableProperty]
        private bool _isLinkButtonEnabled = false;

        partial void OnCurrentSelectedItemsChanged(ObservableCollection<object> value)
        {
            UpdateLinkButtonState();
        }

        private void UpdateLinkButtonState()
        {
            _isLinkButtonEnabled = !isReadOnly &&
                                 currentSelectedItems != null &&
                                 currentSelectedItems.Count > 0;
        }

        [RelayCommand]
        private void SelectionChanged()
        {
            UpdateLinkButtonState();
        }
    }
}