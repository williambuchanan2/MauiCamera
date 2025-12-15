using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;

namespace MauiCamera.ViewModels;

public partial class AttachedImageViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    private AttachedImage model;

    [ObservableProperty]
    private bool processingDone;

    private ImageSource _cachedImageSource;

    public ImageSource ImageSource
    {
        get
        {
            if (_cachedImageSource == null && Model?.AttachmentInByteArray != null)
            {
                _cachedImageSource = ImageSource.FromStream(() =>
                    new MemoryStream(Model.AttachmentInByteArray));
            }
            return _cachedImageSource;
        }
    }

    public AttachedImageViewModel(AttachedImage model)
    {
        Model = model;
    }

    partial void OnModelChanged(AttachedImage value)
    {
        _cachedImageSource = null;

        if (value != null)
        {
            ProcessingDone = (value.AttachmentInByteArray?.Length ?? 0) > 0;
            UpdateButtonStates(value);
        }
        else
        {
            ProcessingDone = false;
        }
    }

    private void UpdateButtonStates(AttachedImage item)
    {
        if (item.Status == AttachedImageStatus.New)
        {
            item.IsActionButtonEnabled = ProcessingDone;
            item.IsEditButtonEnabled = ProcessingDone;
        }
        else
        {
            item.IsActionButtonEnabled = item.IsActionButtonEnabled && ProcessingDone;
            item.IsEditButtonEnabled = item.IsEditButtonEnabled && ProcessingDone;
            item.IsDeleteButtonEnabled = item.IsDeleteButtonEnabled && ProcessingDone;
        }
    }
}