using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Security.Credentials;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Views;
using Template10.Mvvm;

namespace OwncloudUniversal.ViewModels
{
    class ManageAccountsPageViewModel : ViewModelBase
    {

        public ManageAccountsPageViewModel()
        {
            AddAccountCommand = new DelegateCommand(async ()=> await NavigationService.NavigateAsync(typeof(WelcomePage)));
        }

        public ICommand AddAccountCommand { get; private set; }

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
                Configuration.SelectedServer = value;
                RaisePropertyChanged();
            }
        }
    }
}
