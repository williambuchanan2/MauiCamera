using MauiCamera.ViewModels;
using Microsoft.Maui.Controls;

namespace MauiCamera.Views;

public partial class AttachmentsPage : ContentPage
{
    public ContentPageUtil<AttachmentsPageVm> PageUtil { get; set; }

    public AttachmentsPage(AttachmentsPageVm vm)
    {
        InitializeComponent();

        PageUtil = new ContentPageUtil<AttachmentsPageVm>(this, vm);
        BindingContext = PageUtil.PageBindingContext;
    }

}