using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Services;
using OwncloudUniversal.Synchronization.Configuration;
using OwncloudUniversal.Synchronization.Model;
using OwncloudUniversal.Views;
using Template10.Common;
using Template10.Mvvm;

namespace OwncloudUniversal.ViewModels
{
    class CameraUploadPageViewModel : ViewModelBase
    {
        private readonly InstantUploadRegistration _registration;
        private FolderAssociation _association;
        private SyncedFoldersService _foldersService;

        public CameraUploadPageViewModel()
        {
            _foldersService = new SyncedFoldersService();
            _registration = new InstantUploadRegistration();
            _association = FolderAssociationTableModel.GetDefault().GetAllItems()
                .FirstOrDefault(x => x.SupportsInstantUpload = true);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is DavItem item)
                _foldersService.CreateCameraUploadAssociation(item);
            return Task.CompletedTask;
        }

        public string Folder
        {
            get { return _association?.RemoteFolderFolderPath; }
            set { }
        }

        private async Task SelectFolder()
        {
            await BootStrapper.Current.NavigationService.NavigateAsync(typeof(SelectFolderPageViewModel), typeof(SettingsPage), new SuppressNavigationTransitionInfo());
        }

        public bool Enabled
        {
            get => _registration.Enabled;
            set
            {
                _registration.Enabled = value;
                RaisePropertyChanged();
            }
        }
    }
}
