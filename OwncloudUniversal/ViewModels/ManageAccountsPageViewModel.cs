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
using OwncloudUniversal.WebDav;
using OwncloudUniversal.WebDav.Model;

namespace OwncloudUniversal.ViewModels
{
    class ManageAccountsPageViewModel : ViewModelBase
    {
        private List<Account> _accounts;
        public ManageAccountsPageViewModel()
        {
            AddAccountCommand = new DelegateCommand(async ()=> await NavigationService.NavigateAsync(typeof(WelcomePage)));
            RemoveAccountCommand = new DelegateCommand(() => new UserAccountManager().DeleteAccount(SelectedAccount));
        }

        public ICommand AddAccountCommand { get; }
        public ICommand RemoveAccountCommand { get; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var userAccountManager = new UserAccountManager();
            Accounts = await userAccountManager.GetAllAccountsAsync();
            await base.OnNavigatedToAsync(parameter, mode, state);
        }


        public List<Account> Accounts
        {
            get => _accounts;
            set
            {
                _accounts = value;
                RaisePropertyChanged();
            }
        }

        public Account SelectedAccount { get; set; }
    }
}
