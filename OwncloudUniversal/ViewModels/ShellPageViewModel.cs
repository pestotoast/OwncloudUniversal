using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp;
using OwncloudUniversal.Services;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Views;
using Template10.Mvvm;

namespace OwncloudUniversal.ViewModels
{
    class ShellPageViewModel : ViewModelBase
    {
        public List<PasswordCredential> Credentials
        {
            get
            {
                var manager = new UserAccountManager();
                return manager.GetAllAccounts();
            }
        }

        public PasswordCredential SelectedServer
        {
            get { return Configuration.SelectedServer; }
            set
            {
                if(value != null)
                    Configuration.SelectedServer = value;
                WebDavNavigationService.Reset();
            }
        }
    }
}
