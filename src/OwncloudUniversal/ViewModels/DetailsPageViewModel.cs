using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Synchronization.Model;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization.Configuration;

namespace OwncloudUniversal.ViewModels
{
    public class DetailsPageViewModel : ViewModelBase
    {
        private DavItem _item;

        public DavItem Item
        {
            get { return _item; }
            private set
            {
                _item = value;
                RaisePropertyChanged();
            }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;
            var item = parameter as BaseItem;
            if (item != null)
                Item = (DavItem)item;
            var serverUrl = Configuration.ServerUrl.Substring(0, Configuration.ServerUrl.IndexOf("remote.php", StringComparison.OrdinalIgnoreCase));
            if (!Item.IsCollection && Item.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                var itemPath = Item.EntityId.Substring(Item.EntityId.IndexOf("remote.php/webdav", StringComparison.OrdinalIgnoreCase) + 17);
                var url = serverUrl + "index.php/apps/files/api/v1/thumbnail/" + 128 + "/" + 128 + itemPath;
                Item.ThumbnailUrl = url;
            }
            RaisePropertyChanged("Item");
            return base.OnNavigatedToAsync(parameter, mode, state);
        }
    }
}

