﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Shared.Model;
using OwncloudUniversal.ViewModels;
using OwncloudUniversal.WebDav.Model;
using Template10.Services.PopupService;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace OwncloudUniversal.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FilesPage : Page
    {
        public FilesPageViewModel FilesPageViewModel { get; } = new FilesPageViewModel();
        private static List<object> _selectedItems { get; set; }


        public static List<AbstractItem> GetSelectedItems()
        {
            //using this dirty workaround because if the selected Items are passed 
            //directly as commandparameter they are always null
            return _selectedItems.Cast<AbstractItem>().ToList();
        }

        public FilesPage()
        {
            this.InitializeComponent();
        }

        private void UIElement_OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            var uiSender = sender as UIElement;
            if (ListView.SelectionMode == ListViewSelectionMode.Single)
            {
                var flyout = (FlyoutBase)uiSender.GetValue(FlyoutBase.AttachedFlyoutProperty);
                flyout.ShowAt(uiSender as FrameworkElement);
            }
        }

        private void UIElement_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var uiSender = sender as UIElement;
            if (ListView.SelectionMode == ListViewSelectionMode.Single)
            {
                var flyout = (FlyoutBase)uiSender.GetValue(FlyoutBase.AttachedFlyoutProperty);
                flyout.ShowAt(uiSender as FrameworkElement);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItems = ListView.SelectedItems?.ToList();
        }
    }
}