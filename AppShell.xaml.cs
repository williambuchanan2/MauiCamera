using Microsoft.Maui.Controls;

namespace MauiCamera;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}

    public static void SwitchToMainShellItem()
    {
        //NavigationUtil.NavigateShell(nameof(MainShellItem));
    }

    public static void SwitchToLoginShellItem()
    {
        //NavigationUtil.NavigateShell(nameof(LogoutShellItem));
    }

    public static void SwitchToResetShellItem()
    {
        //NavigationUtil.NavigateShell(nameof(ResetShellItem));
    }

}
