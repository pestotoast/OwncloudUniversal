using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Template10.Mvvm;
using Template10.Services.SettingsService;
using Windows.UI.Xaml;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Synchronization.Configuration;
using OwncloudUniversal.Synchronization.SQLite;
using OwncloudUniversal.Utils;
using Template10.Common;
using SettingsService = OwncloudUniversal.Services.SettingsServices.SettingsService;

namespace OwncloudUniversal.ViewModels
{
    public class SettingsPageViewModel : ViewModelBase
    {
        public SettingsPartViewModel SettingsPartViewModel { get; } = new SettingsPartViewModel();
        public AboutPartViewModel AboutPartViewModel { get; } = new AboutPartViewModel();
    }

    public class SettingsPartViewModel : ViewModelBase
    {
        Services.SettingsServices.SettingsService _settings;
        BackgroundTaskConfiguration _taskConfig = new BackgroundTaskConfiguration();

        public ICommand ResetCommand { get; }

        public SettingsPartViewModel()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // designtime
            }
            else
            {
                _settings = Services.SettingsServices.SettingsService.Instance;
            }

            ResetCommand = new DelegateCommand(async () => await ResetDataBaseAsync());
        }

        public bool ShowHamburgerButton
        {
            get { return _settings.ShowHamburgerButton; }
            set { _settings.ShowHamburgerButton = value; base.RaisePropertyChanged(); }
        }

        public bool IsFullScreen
        {
            get { return _settings.IsFullScreen; }
            set
            {
                _settings.IsFullScreen = value;
                base.RaisePropertyChanged();
                if (value)
                {
                    ShowHamburgerButton = false;
                }
                else
                {
                    ShowHamburgerButton = true;
                }
            }
        }

        public bool UseLightThemeButton
        {
            get { return _settings.AppTheme.Equals(ApplicationTheme.Light); }
            set
            {
                _settings.AppTheme = value ? ApplicationTheme.Light : ApplicationTheme.Dark; base.RaisePropertyChanged();
                ThemeHelper.UpdateTitleBar();
            }
        }

        public string ServerUrl
        {
            get { return Configuration.ServerUrl; }
            set { Configuration.ServerUrl = value; }
        }

        public string UserName
        {
            get { return Configuration.UserName; }
            set { Configuration.UserName = value; }
        }

        public string Password
        {
            get { return Configuration.Password; }
            set { Configuration.Password = value; }
        }

        private async Task ResetDataBaseAsync()
        {
            MessageDialog areYouSure = new MessageDialog(App.ResourceLoader.GetString("ResetQuestion"));
            areYouSure.Commands.Add(new UICommand(App.ResourceLoader.GetString("yes"), null, "YES"));
            areYouSure.Commands.Add(new UICommand(App.ResourceLoader.GetString("no"), null, "NO"));
            var result = await areYouSure.ShowAsync();
            if (result.Id.ToString() == "YES")
            {
                SQLiteClient.Reset();
                LogHelper.ResetLog();
                Configuration.Reset();
                MessageDialog dialog = new MessageDialog(App.ResourceLoader.GetString("RestartMessage"));
                await dialog.ShowAsync();
                BootStrapper.Current.Exit();
            }
        }

        public bool BackgroundTaskEnabled
        {
            get { return _taskConfig.Enabled; }
            set
            {
                _taskConfig.Enabled = value;
                Configuration.IsBackgroundTaskEnabled = value;
                RaisePropertyChanged();
            }
        }

        public long MaximumDownloadSize
        {
            get { return Configuration.MaxDownloadSize; }
            set { Configuration.MaxDownloadSize = value; }
        }
    }

    public class AboutPartViewModel : ViewModelBase
    {
        public Uri Logo => Windows.ApplicationModel.Package.Current.Logo;

        public string DisplayName => Windows.ApplicationModel.Package.Current.DisplayName;

        public string Publisher => Windows.ApplicationModel.Package.Current.PublisherDisplayName;

        public string Version
        {
            get
            {
                var v = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }
        
        public ICommand GenerateReportCommand => new DelegateCommand(async ()=> await DiagnosticReportHelper.GenerateReport());
    }
}

