using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Services;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Views;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization.Configuration;
using OwncloudUniversal.Synchronization.Model;
using Template10.Mvvm;
using Template10.Services.NavigationService;

namespace OwncloudUniversal.ViewModels
{
    public class SelectFolderPageViewModel : ViewModelBase
    {
        private DavItem _selectedItem;
        private List<DavItem> _itemsToMove;
        private WebDavNavigationService _webDavNavigationService;
        private Page _callingPage;

        public WebDavNavigationService WebDavNavigationService
        {
            get { return _webDavNavigationService; }
            private set
            {
                _webDavNavigationService = value;
                RaisePropertyChanged();
            }
        }

        public ICommand HomeCommand { get; private set; }
        public ICommand AcceptCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand CreateFolderCommand { get; private set; }

        public SelectFolderPageViewModel()
        {
            HomeCommand = new DelegateCommand(async () => await WebDavNavigationService.NavigateAsync(new DavItem {EntityId = Configuration.ServerUrl}));
            AcceptCommand = new DelegateCommand(async () => await AcceptSelection());
            CancelCommand = new DelegateCommand(async () =>
            {

                await WebDavNavigationService.ReloadAsync();
                await NavigationService.NavigateAsync(typeof(FilesPage), SelectedItem, new SuppressNavigationTransitionInfo());
            });
            CreateFolderCommand = new DelegateCommand(async () => await CreateFolderAsync());
        }
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;
            WebDavNavigationService = await WebDavNavigationService.InintializeAsync();
            WebDavNavigationService.PropertyChanged += WebDavNavigationServiceOnPropertyChanged;
            //items should be moved
            if (parameter is List<DavItem>)
            {
                _itemsToMove = parameter as List<DavItem>;
                HideItemsToMove();
            }
            //just a folder has to be selected (nothing to move), the parameter is the default folder
            if (parameter is DavItem item)
            {
                await WebDavNavigationService.NavigateAsync(item);
            }
            await base.OnNavigatedToAsync(parameter, mode, state);
        }

        private void WebDavNavigationServiceOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if(propertyChangedEventArgs.PropertyName=="Items")
                HideItemsToMove();
        }

        private void HideItemsToMove()
        {
            if(_itemsToMove == null)
                return;
            foreach (var davItem in _itemsToMove)
            {
                var item = WebDavNavigationService.Items?.FirstOrDefault(x => x.EntityId == davItem.EntityId);
                if(item != null)
                    WebDavNavigationService.Items.Remove(item);
            }
        }

        private async Task Move()
        {
            try
            {
                IndicatorService.GetDefault().ShowBar();
                foreach (var davItem in _itemsToMove)
                {
                    await WebDavItemService.GetDefault().MoveToFolder(davItem, WebDavNavigationService.CurrentItem);
                }
            }
            finally
            {
                IndicatorService.GetDefault().HideBar();
                await WebDavNavigationService.ReloadAsync();
                await NavigationService.NavigateAsync(typeof(FilesPage), WebDavNavigationService.CurrentItem, new SuppressNavigationTransitionInfo());
            }
            for (int i = 0; i < NavigationService.Frame.BackStackDepth; i++)
            {
                if(NavigationService.Frame.BackStack[i].SourcePageType == typeof(SelectFolderPage))
                    NavigationService.Frame.BackStack.RemoveAt(i);
            }
        }

        public DavItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (_selectedItem == value)
                    return;
                _selectedItem = value;
                RaisePropertyChanged();
                if(value == null)
                    return;
                if (value.IsCollection)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    WebDavNavigationService.NavigateAsync(value);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
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
                IndicatorService.GetDefault().ShowBar();
                await WebDavItemService.GetDefault().CreateFolder(WebDavNavigationService.CurrentItem, box.Text);
                await WebDavNavigationService.ReloadAsync();
                IndicatorService.GetDefault().HideBar();
            }
        }

        private async Task AcceptSelection()
        {
            if(_itemsToMove != null)
                await Move();
            else
            {
                SyncedFoldersService service = new SyncedFoldersService();
                var library = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
                foreach (var assocaition in service.GetAllSyncedFolders())
                {
                    if (WebDavNavigationService.CurrentItem.EntityId == assocaition.RemoteFolderFolderPath)
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(App.ResourceLoader.GetString("SelectedFolderAlreadyInUseForSync"), WebDavNavigationService.CurrentItem.EntityId));
                        await dialog.ShowAsync();
                        return;
                    }
                    if (String.Equals(assocaition.LocalFolderPath, library.SaveFolder.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageDialog dialog = new MessageDialog(string.Format(App.ResourceLoader.GetString("SelectedFolderAlreadyInUseForSync"), library.SaveFolder.Path));
                        await dialog.ShowAsync();
                        return;
                    }
                }
                Configuration.IsBackgroundTaskEnabled = true;
                new BackgroundTaskConfiguration().Enabled = true;
                await service.AddFolderToSyncAsync(library.SaveFolder, WebDavNavigationService.CurrentItem, SyncDirection.UploadOnly);
                await NavigationService.NavigateAsync(typeof(SyncedFoldersPageView),
                    WebDavNavigationService.CurrentItem, new SuppressNavigationTransitionInfo());
            }
        }
    }
}
