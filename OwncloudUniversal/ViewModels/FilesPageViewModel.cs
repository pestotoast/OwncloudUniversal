using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Microsoft.Toolkit.Uwp.UI;
using OwncloudUniversal.Services;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Shared.LocalFileSystem;
using OwncloudUniversal.Shared.Model;
using OwncloudUniversal.Views;
using OwncloudUniversal.WebDav;
using OwncloudUniversal.WebDav.Model;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using Template10.Utils;

namespace OwncloudUniversal.ViewModels
{
    public class FilesPageViewModel : ViewModelBase
    {
        private DavItem _selectedItem;
        private readonly SyncedFoldersService _syncedFolderService;
        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Single;
        private WebDavNavigationService _webDavNavigationService;

        public FilesPageViewModel()
        {
            _syncedFolderService = new SyncedFoldersService();
            UploadItemCommand = new DelegateCommand(async () => await NavigationService.NavigateAsync(typeof(FileTransferPage), WebDavNavigationService.CurrentItem, new SuppressNavigationTransitionInfo()) );   
            RefreshCommand = new DelegateCommand(async () => await WebDavNavigationService.ReloadAsync());
            AddToSyncCommand = new DelegateCommand<object>(async parameter => await RegisterFolderForSync(parameter));
            DownloadCommand = new DelegateCommand<DavItem>(async item => await NavigationService.NavigateAsync(typeof(FileTransferPage), FilesPage.GetSelectedItems(item), new SuppressNavigationTransitionInfo()));
            DeleteCommand = new DelegateCommand<DavItem>(async item => await DeleteItems(FilesPage.GetSelectedItems(item)));
            SwitchSelectionModeCommand = new DelegateCommand(() => SelectionMode = SelectionMode == ListViewSelectionMode.Multiple ? ListViewSelectionMode.Single : ListViewSelectionMode.Multiple);
            ShowPropertiesCommand = new DelegateCommand<DavItem>(async item => await NavigationService.NavigateAsync(typeof(DetailsPage), item, new SuppressNavigationTransitionInfo()));
            AddFolderCommand = new DelegateCommand(async () => await CreateFolderAsync());
            HomeCommand = new DelegateCommand(() => NavigationService.Navigate(typeof(FilesPage), new DavItem { EntityId = Configuration.ServerUrl}, new SuppressNavigationTransitionInfo()));
            MoveCommand = new DelegateCommand<DavItem>(async item => await NavigationService.NavigateAsync(typeof(SelectFolderPage), FilesPage.GetSelectedItems(item), new SuppressNavigationTransitionInfo()));
            RenameCommand = new DelegateCommand<DavItem>(async item => await Rename(item));
        }

        private void WebDavNavigationServiceOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Items")
            {
                Task.Run(() => LoadThumbnails());
            }
        }

        public ICommand UploadItemCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand AddToSyncCommand { get; private set; }
        public ICommand ShowPropertiesCommand { get; private set; }
        public ICommand DownloadCommand { get; private set; }
        public ICommand RenameCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand AddFolderCommand { get; private set; }
        public ICommand SwitchSelectionModeCommand { get; private set; }
        public ICommand HomeCommand { get; private set; }
        public ICommand MoveCommand { get; private set; }

        public WebDavNavigationService WebDavNavigationService
        {
            get { return _webDavNavigationService; }
            private set
            {
                _webDavNavigationService = value;
                RaisePropertyChanged();
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            WebDavNavigationService = await WebDavNavigationService.InintializeAsync();
            WebDavNavigationService.PropertyChanged += WebDavNavigationServiceOnPropertyChanged;
            WebDavNavigationService.SetNavigationService(NavigationService);
            await Task.Run(() => LoadThumbnails());
        }

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            if (args.NavigationMode == NavigationMode.Back && args.TargetPageType == typeof(SelectFolderPage))
            {
                args.TargetPageType = typeof(FilesPage);
            }
            return base.OnNavigatingFromAsync(args);
        }

        public DavItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if(_selectedItem == value)
                    return;
                if (value != null && SelectionMode == ListViewSelectionMode.Single)
                {
                    _selectedItem = value;
                    RaisePropertyChanged();
                    if (value.IsCollection)
                    {
                        NavigationService.Navigate(typeof(FilesPage), value, new SuppressNavigationTransitionInfo());
                    }
                    else if(value.ContentType.StartsWith("image") || value.ContentType.StartsWith("video"))
                    {
                        NavigationService.Navigate(typeof(PhotoPage), value, new SuppressNavigationTransitionInfo());
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(DetailsPage), value, new SuppressNavigationTransitionInfo());
                    }
                }
            } 
        }

        public ListViewSelectionMode SelectionMode
        {
            get { return _selectionMode; }
            private set
            {
                _selectionMode = value;
                RaisePropertyChanged();
            }
        }

        private async Task RegisterFolderForSync(object parameter)
        {
            if (parameter is DavItem)
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add(".");
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null)
                    return;
                var state = await folder.GetIndexedStateAsync();
                if (state == IndexedState.NotIndexed || state == IndexedState.PartiallyIndexed)
                {
                    ContentDialog dialog = new ContentDialog();
                    dialog.Content = state == IndexedState.NotIndexed ? App.ResourceLoader.GetString("NotIndexedError") : App.ResourceLoader.GetString("PartiallyIndexedError");
                    dialog.PrimaryButtonText = App.ResourceLoader.GetString("yes");
                    dialog.SecondaryButtonText = App.ResourceLoader.GetString("no");
                    var result = await dialog.ShowAsync();
                    if(result == ContentDialogResult.Secondary)
                        return;
                }
                var fa = await _syncedFolderService.AddFolderToSyncAsync(folder, (DavItem) parameter);
                NavigationService.Navigate(typeof(SyncedFolderConfigurationPage), fa, new SuppressNavigationTransitionInfo());
            }
        }

        private async Task DeleteItems(List<DavItem> items)
        {
            ContentDialog dialog = new ContentDialog();
            if (items.Count == 1)
            {
                var item = items.First();
                dialog.Title = App.ResourceLoader.GetString("deleteFileConfirmation");
                if (item.IsCollection)
                    dialog.Title = App.ResourceLoader.GetString("deleteFolderConfirmation");
                dialog.Content = item.DisplayName;
            }
            else
            {
                dialog.Title = App.ResourceLoader.GetString("deleteMultipleConfirmation");
                int i = 0;
                foreach (var item in items)
                {
                    if (i < 3)
                    {
                        dialog.Content += item.DisplayName + Environment.NewLine;
                        i++;
                    }
                    else
                    {
                        dialog.Content += "...";
                        break;
                    }
                }
            }
            dialog.PrimaryButtonText = App.ResourceLoader.GetString("yes");
            dialog.SecondaryButtonText = App.ResourceLoader.GetString("no");
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var indicator = new IndicatorService();
                indicator.ShowBar();
                var itemService = new WebDavItemService();
                await itemService.DeleteItemAsync(items.Cast<DavItem>().ToList());
                SelectionMode = ListViewSelectionMode.Single;
                await WebDavNavigationService.ReloadAsync();
                indicator.HideBar();
            }
            
        }

        private async Task CreateFolderAsync()
        {
            var dialog = new ContentDialog();
            dialog.Title = App.ResourceLoader.GetString("CreateNewFolder");
            var box = new TextBox()
            {
                Header = App.ResourceLoader.GetString("FolderName"),
                AcceptsReturn = false,
                SelectedText = App.ResourceLoader.GetString("NewFolderName")
            };
            dialog.Content = box;
            dialog.PrimaryButtonText = App.ResourceLoader.GetString("OK");
            dialog.SecondaryButtonText = App.ResourceLoader.GetString("Cancel");
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                    return;
                var indicator = new IndicatorService();
                indicator.ShowBar();
                var itemService = new WebDavItemService();
                await itemService.CreateFolder(WebDavNavigationService.CurrentItem, box.Text);
                await WebDavNavigationService.ReloadAsync();
                indicator.HideBar();
            }
        }

        private void LoadThumbnails()
        {
            if(WebDavNavigationService.Items == null)
                return;

            var serverUrl = Configuration.ServerUrl.Substring(0, Configuration.ServerUrl.IndexOf("remote.php", StringComparison.OrdinalIgnoreCase));
            foreach (var davItem in WebDavNavigationService.Items)
            {
                if (!davItem.IsCollection && davItem.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    var itemPath = davItem.EntityId.Substring(davItem.EntityId.IndexOf("remote.php/webdav", StringComparison.OrdinalIgnoreCase) + 17);
                    var url = serverUrl + "index.php/apps/files/api/v1/thumbnail/" + 40 + "/" + 40 + itemPath;
                    davItem.ThumbnailUrl = url;
                }
            }
        }

        private async Task Rename(DavItem item)
        {
            var dialog = new ContentDialog();
            dialog.Title = App.ResourceLoader.GetString("RenameTitle");
            var box = new TextBox();
            box.Header = App.ResourceLoader.GetString("NewName");
            box.AcceptsReturn = false;
            try
            {
                box.Text = Path.GetExtension(item.DisplayName);
                box.SelectedText = Path.GetFileNameWithoutExtension(item.DisplayName);

            }
            catch (ArgumentException)
            {
                box.Text = item.DisplayName;
            }

            dialog.Content = box;
            dialog.PrimaryButtonText = App.ResourceLoader.GetString("OK");
            dialog.SecondaryButtonText = App.ResourceLoader.GetString("Cancel");
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if(string.IsNullOrWhiteSpace(box.Text))
                    return;
                var indicator = new IndicatorService();
                indicator.ShowBar();
                var itemService = new WebDavItemService();
                await itemService.Rename(item, box.Text);
                await WebDavNavigationService.ReloadAsync();
                indicator.HideBar();
            }
        }
    }
}
