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
using OwncloudUniversal.WebDav;
using OwncloudUniversal.WebDav.Model;

namespace OwncloudUniversal.ViewModels
{
    class ShellPageViewModel : ViewModelBase
    {
        private Account _selectedAccount;
        private List<Account> _accounts;
       
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

        public Account SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                Configuration.SelectedServer = _selectedAccount.Credentials;
                WebDavNavigationService.Reset();
            }
        }
    }
}
