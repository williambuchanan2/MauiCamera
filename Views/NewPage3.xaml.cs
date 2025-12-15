using MauiCamera.ViewModels;
using Microsoft.Maui.Controls;

namespace MauiCamera.Views;

public partial class NewPage3 : ContentPage
{
    public ContentPageUtil<Class3> PageUtil { get; set; }

    public NewPage3(Class3 vm)
    {
        InitializeComponent();

        PageUtil = new ContentPageUtil<Class3>(this, vm);
        BindingContext = PageUtil.PageBindingContext;
    }

}