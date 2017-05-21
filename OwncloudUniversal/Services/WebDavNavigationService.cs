using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Shared.Model;
using OwncloudUniversal.Views;
using OwncloudUniversal.WebDav.Model;
using Template10.Services.NavigationService;
using Template10.Utils;

namespace OwncloudUniversal.Services
{
    public class WebDavNavigationService : INotifyPropertyChanged
    {
        private static WebDavNavigationService _instance;
        private static INavigationService _navigationService;
        private readonly WebDavItemService _itemService;
        private DavItem _currentItem;
        private ObservableCollection<DavItem> _items;
        private ObservableCollection<DavItem> _backStack;
        private ObservableCollection<DavItem> _forwardStack;

        private WebDavNavigationService()
        {
            _itemService = new WebDavItemService();
            BackStack = new ObservableCollection<DavItem>();
            ForwardStack = new ObservableCollection<DavItem>();
            Items = new ObservableCollection<DavItem>();
        }

        public static async Task<WebDavNavigationService> InintializeAsync()
        {
            if (_instance != null) return _instance;
            _instance = new WebDavNavigationService();
            var item = new DavItem { Href = Configuration.ServerUrl, IsCollection = true };
            await _instance.NavigateAsync(item);
            return _instance;
        }

        public static async void Reset()
        {
            if (_instance != null)
            {
                _instance.Items = new ObservableCollection<DavItem>();
                _instance.BackStack = new ObservableCollection<DavItem>();
                _instance.ForwardStack = new ObservableCollection<DavItem>();
                _instance.CurrentItem = null;
                await _instance.NavigateAsync(new DavItem { Href = Configuration.ServerUrl, IsCollection = true });
                _navigationService.ClearHistory();
            }
        }

        public void SetNavigationService(INavigationService service)
        {
            _navigationService = service;
            _navigationService.FrameFacade.Navigated += FrameFacadeOnNavigating;
        }

        private async void FrameFacadeOnNavigating(object sender, NavigatedEventArgs args)
        {
            var mode = args.NavigationMode;
            var sourcePageEntry = args.Page.Frame.BackStack.LastOrDefault();
            if (args.NavigationMode == NavigationMode.Back)
                sourcePageEntry = args.Page.Frame.ForwardStack.LastOrDefault();
            var targetPage = args.Page;

            Debug.WriteLine($"Navigating: {args.NavigationMode}, To: {targetPage.GetType().Name}, From {sourcePageEntry?.SourcePageType.Name}");
            
            //ignore all naviagtion between other pagetypes
            if (!(targetPage is FilesPage || targetPage is SelectFolderPage))
                return;
            if (!(sourcePageEntry?.SourcePageType == typeof(FilesPage) || sourcePageEntry?.SourcePageType == typeof(SelectFolderPage)))
                return;

            if (mode == NavigationMode.New)
            {
                DavItem parameter = null;
                if (args.Parameter is string)
                    parameter = Template10.Services.SerializationService.SerializationService.Json.Deserialize((string)args.Parameter) as DavItem;
                await NavigateAsync(parameter);
            }

            if (mode == NavigationMode.Back)
            {
                await GoBackAsync();
            }
            if (mode == NavigationMode.Forward)
            {
                await GoForwardAsync();
            }
        }
        
        public ObservableCollection<DavItem> Items
        {
            get { return _items; }
            private set
            {
                _items = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DavItem> BackStack
        {
            get { return _backStack; }
            private set
            {
                _backStack = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DavItem> ForwardStack
        {
            get { return _forwardStack; }
            private set
            {
                _forwardStack = value;
                OnPropertyChanged();
            }
        }

        public DavItem CurrentItem
        {
            get { return _currentItem; }
            private set
            {
                _currentItem = value;
                OnPropertyChanged();
            }
        }

        private async Task NavigateAsync(DavItem item)
        {
            if(item == null)
                return;
            if (BackStack.FirstOrDefault(x=>x.EntityId == item.EntityId) != null)
            {
                var entry = _navigationService?.Frame.BackStack.FirstOrDefault(x =>
                {
                    var s = x.Parameter as string;
                    if (s == null) return false;
                    if (!s.Contains(item.GetType().FullName)) return false;
                    var parameterItem = Template10.Services.SerializationService.SerializationService.Json.Deserialize(s);
                    if(parameterItem is DavItem)
                        return ((DavItem)parameterItem).EntityId == item.EntityId;
                    return false;
                });
                if (entry != default(PageStackEntry))
                {
                    int i = _navigationService.Frame.BackStack.IndexOf(entry);
                    while (_navigationService.Frame.BackStackDepth > i)
                    {
                        _navigationService.Frame.BackStack.RemoveAt(_navigationService.Frame.BackStackDepth-1);
                    }
                }
                else _navigationService?.Frame.BackStack.Clear();
                var backStackItem = BackStack.FirstOrDefault(x => x.EntityId == item.EntityId);
                int index = BackStack.IndexOf(backStackItem);
                var list = BackStack.ToList();
                list.RemoveRange(index+1, BackStack.Count - index-1);
                BackStack = list.ToObservableCollection();
                ForwardStack.Clear();
                CurrentItem = backStackItem;
            }
            else
            {
                BackStack.Add(item);
                ForwardStack.Remove(item);
                CurrentItem = item;
            }
            await ReloadAsync();
        }

        private async Task GoForwardAsync()
        {
           if(ForwardStack.Count > 0)
                CurrentItem = ForwardStack.Last();
            ForwardStack.Remove(CurrentItem);
            BackStack.Add(CurrentItem);
            await ReloadAsync();
        }

        private async Task GoBackAsync()
        {
            BackStack.Remove(CurrentItem);
            ForwardStack.Add(CurrentItem);
            if (BackStack.Count > 0)
                CurrentItem = BackStack.Last();
            await ReloadAsync();
        }

        public async Task ReloadAsync()
        {
             var service = new IndicatorService();
            service.ShowBar();
            Items.Clear();
            Items.AddRange(await _itemService.GetItemsAsync(new Uri(CurrentItem.EntityId, UriKind.RelativeOrAbsolute)));
            OnPropertyChanged("Items");
            service.HideBar();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
