using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MauiCamera.Views;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiCamera.ViewModels
{
    public partial class Class2: BaseViewModel
    {
        [ObservableProperty]
        public string _testText;


        [RelayCommand]
        private void NextButtonPressed()
        {
            NavigationUtil.Navigate<NewPage3>();
        }

    }
}
