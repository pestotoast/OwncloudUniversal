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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Microsoft.Toolkit.Uwp.UI;
using OwncloudUniversal.Converters;
using OwncloudUniversal.Services;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Synchronization.LocalFileSystem;
using OwncloudUniversal.Synchronization.Model;
using OwncloudUniversal.Views;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization.Configuration;
using OwncloudUniversal.Utils;
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
        private CancellationTokenSource _thumbnailTokenSource;
        private CancellationTokenSource _tokenSource;
        private ExtendedExecutionSession _executionSession;

        public FilesPageViewModel()
        {
            _syncedFolderService = new SyncedFoldersService();
            UploadItemCommand = new DelegateCommand(async () => await UploadFilesAsync() );   
            RefreshCommand = new DelegateCommand(async () => await WebDavNavigationService.ReloadAsync());
            AddToSyncCommand = new DelegateCommand<object>(async parameter => await RegisterFolderForSync(parameter));
            DownloadCommand = new DelegateCommand<DavItem>(async item => await DownloadFilesAsync(FilesPage.GetSelectedItems(item)));
            DeleteCommand = new DelegateCommand<DavItem>(async item => await DeleteItems(FilesPage.GetSelectedItems(item)));
            SwitchSelectionModeCommand = new DelegateCommand(() => SelectionMode = SelectionMode == ListViewSelectionMode.Multiple ? ListViewSelectionMode.Single : ListViewSelectionMode.Multiple);
            ShowPropertiesCommand = new DelegateCommand<DavItem>(async item => await NavigationService.NavigateAsync(typeof(DetailsPage), item, new SuppressNavigationTransitionInfo()));
            AddFolderCommand = new DelegateCommand(async () => await CreateFolderAsync());
            HomeCommand = new DelegateCommand(async () => await WebDavNavigationService.NavigateAsync(new DavItem { EntityId = Configuration.ServerUrl }));
            MoveCommand = new DelegateCommand<DavItem>(async item => await NavigationService.NavigateAsync(typeof(SelectFolderPage), FilesPage.GetSelectedItems(item), new SuppressNavigationTransitionInfo()));
            RenameCommand = new DelegateCommand<DavItem>(async item => await Rename(item));
            OpenCommand = new DelegateCommand<DavItem>(async item => await OpenFileAsync(item));
            ToogleViewCommand = new DelegateCommand(() => ShowGridView = !ShowGridView);
        }

        private void WebDavNavigationServiceOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Items")
            {
                //stop loading the old thumbnails after changing the webdav folder
                if(_thumbnailTokenSource == null)
                    _thumbnailTokenSource = new CancellationTokenSource();
                else
                {
                    _thumbnailTokenSource.Cancel();
                    _thumbnailTokenSource = new CancellationTokenSource();
                }
                Task.Run(() => LoadThumbnails(), _thumbnailTokenSource.Token);
            }
        }

        public ICommand UploadItemCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddToSyncCommand { get; }
        public ICommand ShowPropertiesCommand { get;}
        public ICommand DownloadCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand SwitchSelectionModeCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand MoveCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand ToogleViewCommand { get; }

        public bool ShowGridView
        {
            get
            {
                return Configuration.ShowGridView;
            }
            private set
            {
                Configuration.ShowGridView = value;
                RaisePropertyChanged();
            }
        }

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
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;
            WebDavNavigationService = await WebDavNavigationService.InintializeAsync();
            WebDavNavigationService.PropertyChanged += WebDavNavigationServiceOnPropertyChanged;
            await Task.Run(() => LoadThumbnails());
            await ShowCameraUploadInfo();
        }

        private async Task ShowCameraUploadInfo()
        {
            if (Configuration.ShowCamerUploadInfo)
            {
                ContentDialog dialog = new ContentDialog();
                dialog.VerticalAlignment = VerticalAlignment.Center;
                dialog.Content = App.ResourceLoader.GetString("CameraUploadInfoText");
                dialog.PrimaryButtonText = App.ResourceLoader.GetString("yes");
                dialog.SecondaryButtonText = App.ResourceLoader.GetString("no");
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Secondary)
                {
                    Configuration.ShowCamerUploadInfo = false;
                    return;
                }
                if (result == ContentDialogResult.Primary)
                {
                    Configuration.ShowCamerUploadInfo = false;
                    await NavigationService.NavigateAsync(typeof(SelectFolderPage), new DavItem { EntityId = Configuration.ServerUrl }, new SuppressNavigationTransitionInfo());
                }
            }
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
                    if (value.IsCollection)
                    {
                        var t = WebDavNavigationService.NavigateAsync(value);
                        //IndicatorService.GetDefault().ShowBar();
                        //NavigationService.Navigate(typeof(FilesPage), value, new SuppressNavigationTransitionInfo());
                    }
                    else if(value.ContentType.StartsWith("image") || value.ContentType.StartsWith("video"))
                    {
                        NavigationService.Navigate(typeof(PhotoPage), value, new SuppressNavigationTransitionInfo());
                    }
                    else
                    {
                        var task = OpenFileAsync(value);
                    }
                    RaisePropertyChanged();
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

        public int SelectedIndex
        {
            get
            {
                return WebDavNavigationService?.Items?.IndexOf(WebDavNavigationService.CurrentItem) ?? -1;
            }
        }

        private async Task RegisterFolderForSync(object parameter)
        {
            await DesktopClientHelper.ShowDekstopClientInfo();
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
                foreach (var assocaition in _syncedFolderService.GetAllSyncedFolders())
                {
                    if(String.Equals(folder.Path, assocaition.LocalFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(App.ResourceLoader.GetString("SelectedFolderAlreadyInUseForSync"), folder.Path));
                        await dialog.ShowAsync();
                        return;
                    }
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
                IndicatorService.GetDefault().ShowBar();
                await WebDavItemService.GetDefault().DeleteItemAsync(items.Cast<DavItem>().ToList());
                SelectionMode = ListViewSelectionMode.Single;
                await WebDavNavigationService.ReloadAsync();
                IndicatorService.GetDefault().HideBar();
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
                IndicatorService.GetDefault().ShowBar();
                await WebDavItemService.GetDefault().CreateFolder(WebDavNavigationService.CurrentItem, box.Text);
                await WebDavNavigationService.ReloadAsync();
                IndicatorService.GetDefault().HideBar();
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
                    var url = serverUrl + "index.php/apps/files/api/v1/thumbnail/" + 120 + "/" + 120 + itemPath;
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
                IndicatorService.GetDefault().ShowBar();
                await WebDavItemService.GetDefault().Rename(item, box.Text);
                await WebDavNavigationService.ReloadAsync();
                IndicatorService.GetDefault().HideBar();
            }
        }

        private async Task OpenFileAsync(DavItem item)
        {
            try
            {
                var tokenSource = new CancellationTokenSource();
                var button = new Button();
                button.Content = App.ResourceLoader.GetString("Cancel");
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.Command = new DelegateCommand(() =>
                {
                    tokenSource.Cancel(false);
                    IndicatorService.GetDefault().HideBar();
                });
                var progress = new Progress<HttpProgress>(async httpProgress =>
                {
                    await Dispatcher.DispatchAsync(() =>
                    {
                        var text = string.Format(App.ResourceLoader.GetString("DownloadingFile"), item.DisplayName);
                        text += " - " + new ProgressToPercentConverter().Convert(httpProgress, null, null, null);
                        IndicatorService.GetDefault().ShowBar(text, button);
                    });
                });
                var file = await WebDavItemService.GetDefault().DownloadAsync(item, ApplicationData.Current.TemporaryFolder, tokenSource.Token, progress);
                if(!tokenSource.IsCancellationRequested)
                    await Launcher.LaunchFileAsync(file);
            }
            finally
            {
                IndicatorService.GetDefault().HideBar();
            }
        }

        private async Task DownloadFilesAsync(List<DavItem> files)
        {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add(".");
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            var folder = await picker.PickSingleFolderAsync();

            _tokenSource = new CancellationTokenSource();
            var button = new Button();
            button.Content = App.ResourceLoader.GetString("Cancel");
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Command = new DelegateCommand(() =>
            {
                _tokenSource.Cancel(false);
                IndicatorService.GetDefault().HideBar();
            });
            _executionSession = await RequestExtendedExecutionAsync();
            try
            {
                foreach (var fileToDownload in files)
                {
                    if (_tokenSource.IsCancellationRequested)
                    {
                        IndicatorService.GetDefault().HideBar();
                        SelectionMode = ListViewSelectionMode.Single;
                        return;
                    }
                    var progress = new Progress<HttpProgress>(async httpProgress =>
                    {
                        if(_tokenSource.IsCancellationRequested)
                            return;
                        await Dispatcher.DispatchAsync(() =>
                        {
                            var text = string.Format(App.ResourceLoader.GetString("DownloadingFile"), fileToDownload.DisplayName);
                            text += Environment.NewLine + new BytesToSuffixConverter().Convert(httpProgress.BytesReceived, null, null, null) + " - " + new ProgressToPercentConverter().Convert(httpProgress, null, null, null);
                            IndicatorService.GetDefault().ShowBar(text, button);
                        });
                        if (httpProgress.BytesReceived == httpProgress.TotalBytesToReceive && files.Count - 1 == files.IndexOf(fileToDownload))
                        {
                            IndicatorService.GetDefault().HideBar();
                            SelectionMode = ListViewSelectionMode.Single;
                        }
                    });
                    await WebDavItemService.GetDefault().DownloadAsync(fileToDownload, folder, _tokenSource.Token, progress);
                }
                _tokenSource.Cancel();
            }
            finally
            {
                ClearExecutionSession(_executionSession);
                IndicatorService.GetDefault().HideBar();
            }
            
        }

        private async Task UploadFilesAsync()
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            var files = await picker.PickMultipleFilesAsync();

            _tokenSource = new CancellationTokenSource();
            var button = new Button();
            button.Content = App.ResourceLoader.GetString("Cancel");
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Command = new DelegateCommand(() =>
            {
                _tokenSource.Cancel(false);
                IndicatorService.GetDefault().HideBar();
            });
            _executionSession = await RequestExtendedExecutionAsync();

            try
            {
                foreach (var file in files)
                {
                    if (!_tokenSource.IsCancellationRequested)
                    {
                        var progress = new Progress<HttpProgress>(async httpProgress =>
                        {
                            if (_tokenSource.IsCancellationRequested)
                                return;
                            await Dispatcher.DispatchAsync(() =>
                            {
                                var text = string.Format(App.ResourceLoader.GetString("UploadingFile"), file.DisplayName);
                                text += Environment.NewLine + new BytesToSuffixConverter().Convert(httpProgress.BytesSent, null, null, null) + " - " + new ProgressToPercentConverter().Convert(httpProgress, null, null, null);
                                IndicatorService.GetDefault().ShowBar(text, button);

                                if (httpProgress.BytesSent == httpProgress.TotalBytesToSend && files.Count - 1 == files.ToList().IndexOf(file))
                                {
                                    IndicatorService.GetDefault().HideBar();
                                    SelectionMode = ListViewSelectionMode.Single;
                                }
                            });
                        });
                        await WebDavItemService.GetDefault().UploadAsync(WebDavNavigationService.CurrentItem, file, _tokenSource.Token, progress);
                    }
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                ClearExecutionSession(_executionSession);
                IndicatorService.GetDefault().HideBar();
                await WebDavNavigationService.ReloadAsync();
            }
        }

        private async Task<ExtendedExecutionSession> RequestExtendedExecutionAsync()
        {
            var session = new ExtendedExecutionSession();
            session.Reason = ExtendedExecutionReason.Unspecified;
            session.Revoked += SessionRevoked;
            ExtendedExecutionResult result = await session.RequestExtensionAsync();
            if (result == ExtendedExecutionResult.Allowed)
                return session;
            return null;
        }

        private void ClearExecutionSession(ExtendedExecutionSession session)
        {
            if (session != null)
            {
                session.Revoked -= SessionRevoked;
                session.Dispose();
            }
        }

        private void SessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Debug.WriteLine($"Extended execution session was revoked reaseon: {args.Reason}");
            _tokenSource.Cancel(false);
            ToastHelper.SendToast(App.ResourceLoader.GetString("TransferCancelled"));
            ClearExecutionSession(_executionSession);
        }
    }
}
