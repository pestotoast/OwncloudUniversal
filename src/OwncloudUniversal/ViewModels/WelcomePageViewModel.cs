using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Services;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Views;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization.Configuration;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using HttpStatusCode = Windows.Web.Http.HttpStatusCode;

namespace OwncloudUniversal.ViewModels
{
    public class WelcomePageViewModel : ViewModelBase
    {
        private bool _serverFound;
        private string _responseCode;
        private string _serverUrl;
        private string _password;
        private string _userName;
        private ServerStatus _status;
        public string ServerUrl
        {
            get
            {
                _serverUrl = Configuration.ServerUrl;
                return _serverUrl;
            }
            set
            {
                _serverUrl = value;
                CheckServerStatus();
            }
        }

        public string UserName
        {
            get
            {
                _userName = Configuration.UserName;
                return _userName;
            }
            set { _userName = value; }
        }

        public string Password
        {
            get
            {
                _password = Configuration.Password;
                return _password;
            }
            set { _password = value; }
        }

        public string ResponseCode
        {
            get { return _responseCode; }
            set
            {
                _responseCode = value;
                RaisePropertyChanged();
            }
        }

        public ICommand ConnectCommand { get; private set; }

        public WelcomePageViewModel()
        {
            ConnectCommand = new DelegateCommand(async () => await Connect());
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Shell.HamburgerMenu.IsFullScreen = true;
            return Task.CompletedTask;
        }

        private async Task CheckServerStatus()
        {
            try
            {
                _status = await OcsClient.GetServerStatusAsync(_serverUrl);
                ResponseCode = _status?.ResponseCode ?? App.ResourceLoader.GetString("ServerNotFound");
                if (_status?.ResponseCode?.ToLower() == "ok")
                    ResponseCode = _serverUrl.ToLower().Contains("https://") ? App.ResourceLoader.GetString("SecureConnectionSuccessful") : App.ResourceLoader.GetString("InSecureConnectionSuccessful");
            }
            catch (Exception e)
            {
                if ((uint)e.HResult == 0x80072EE7)
                    ResponseCode = App.ResourceLoader.GetString("ServerNotFound");
                else throw;
            }
            if (_status != null && _status.Installed && !_status.Maintenance)
                _serverFound = true;
            else
                _serverFound = false;
            if (_status != null && (_status.Maintenance || !_status.Installed))
            {
                ResponseCode = App.ResourceLoader.GetString("MaintenanceIsEnabled");
            }
        }

        private async Task Connect()
        {
            IndicatorService.GetDefault().ShowBar();
            try
            {
                await CheckServerStatus();
                if (_serverFound)
                {
                    OcsClient client = new OcsClient(new Uri(_serverUrl), new NetworkCredential(_userName, _password));
                    var status = await client.CheckUserLoginAsync();
                    if (status == HttpStatusCode.Ok)
                    {
                        Configuration.ServerUrl = OcsClient.GetWebDavUrl(_serverUrl);
                        Configuration.Password = _password;
                        Configuration.UserName = _userName;
                        Configuration.IsFirstRun = false;
                        Shell.HamburgerMenu.IsFullScreen = false;
                        await NavigationService.NavigateAsync(typeof(FilesPage));
                        NavigationService.ClearHistory();
                    }
                    else
                    {
                        string message = status.ToString();
                        if (status == HttpStatusCode.Unauthorized)
                            message = App.ResourceLoader.GetString("LoginFailed");

                        MessageDialog dialog = new MessageDialog(message);
                        dialog.Title = "ownCloud";
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    ResponseCode = App.ResourceLoader.GetString("ServerNotFound");
                    MessageDialog dialog = new MessageDialog(ResponseCode);
                    await dialog.ShowAsync();
                }
            }
            catch (Exception e)
            {
                if ((uint) e.HResult == 0x80072EE7)
                    ResponseCode = App.ResourceLoader.GetString("ServerNotFound");
                else
                {
                    MessageDialog dialog = new MessageDialog(e.Message);
                    dialog.Title = "ownCloud";
                    await dialog.ShowAsync();
                }
            }
            IndicatorService.GetDefault().HideBar();
        }
    }
}