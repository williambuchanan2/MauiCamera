using CommunityToolkit.Maui.Alerts;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiCamera.Helpers
{

    public static class InfoPrompts
    {
        //public static InfoPrompts Instance { get; set; }
        const double toastMessageSize = 14;

        public static void ShowQuickToast(string message)
        {
            var toast = Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Short, toastMessageSize);
            toast.Show();
        }

        public static void ShowLongToast(string message)
        {
            var toast = Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Long, toastMessageSize);
            toast.Show();
        }

        public static void ShowSnackbar(Action snackbarAction, string message, string actionButtonText)
        {
            var snackbar = Snackbar.Make(message, snackbarAction, actionButtonText, TimeSpan.FromSeconds(3));
            snackbar.Show();
        }
    }

}
