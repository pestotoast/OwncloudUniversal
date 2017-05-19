using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Services;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Views;
using OwncloudUniversal.WebDav;
using OwncloudUniversal.WebDav.Model;
using Template10.Mvvm;
using Template10.Services.NavigationService;

namespace OwncloudUniversal.ViewModels
{
    public class SelectFolderPageViewModel : ViewModelBase
    {
        private DavItem _selectedItem;
        private static List<DavItem> _itemsToMove;
        private WebDavNavigationService _webDavNavigationService;

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
            HomeCommand = new DelegateCommand(async () => await NavigationService.NavigateAsync(typeof(SelectFolderPage), new DavItem{EntityId = Configuration.ServerUrl}, new SuppressNavigationTransitionInfo()));
            AcceptCommand = new DelegateCommand(async () => await Move());
            CancelCommand = new DelegateCommand(async () =>
            {

                await WebDavNavigationService.ReloadAsync();
                await NavigationService.NavigateAsync(typeof(FilesPage), SelectedItem, new SuppressNavigationTransitionInfo());
            });
            CreateFolderCommand = new DelegateCommand(async () => await CreateFolderAsync());
        }
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            WebDavNavigationService = await WebDavNavigationService.InintializeAsync();
            WebDavNavigationService.SetNavigationService(NavigationService);
            WebDavNavigationService.PropertyChanged += WebDavNavigationServiceOnPropertyChanged;
            if (parameter is List<DavItem>)
            {
                _itemsToMove = parameter as List<DavItem>;
            }
            HideItemsToMove();
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
            var service = new WebDavItemService();
            try
            {
                foreach (var davItem in _itemsToMove)
                {
                    await service.MoveToFolder(davItem, WebDavNavigationService.CurrentItem);
                }
            }
            finally
            {
                await NavigationService.NavigateAsync(typeof(FilesPage), WebDavNavigationService.CurrentItem, new SuppressNavigationTransitionInfo());
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
                    NavigationService.Navigate(typeof(SelectFolderPage), value, new SuppressNavigationTransitionInfo());
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
                var indicator = new IndicatorService();
                indicator.ShowBar();
                var itemService = new WebDavItemService();
                await itemService.CreateFolder(WebDavNavigationService.CurrentItem, box.Text);
                await WebDavNavigationService.ReloadAsync();
                indicator.HideBar();
            }
        }
    }
}
