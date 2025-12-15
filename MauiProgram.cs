using CommunityToolkit.Maui;
using MauiCamera.ViewModels;
using MauiCamera.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace MauiCamera;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        RegisterViews(builder);
        return builder.Build();
    }
   
    public static void RegisterViews(MauiAppBuilder builder)
    {
    

        builder.Services.AddTransient<AttachmentsPageVm>(); 
        builder.Services.AddTransient<AttachmentsPage>();
        Routing.RegisterRoute(nameof(AttachmentsPage), typeof(AttachmentsPage));

        builder.Services.AddTransient<Class2>();
        builder.Services.AddTransient<NewPage2>();
        Routing.RegisterRoute(nameof(NewPage2), typeof(NewPage2));

        builder.Services.AddTransient<Class3>();
        builder.Services.AddTransient<NewPage3>();
        Routing.RegisterRoute(nameof(NewPage3), typeof(NewPage3));

      
    }
}
