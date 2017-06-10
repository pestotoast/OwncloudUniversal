using System;
using Windows.UI.Xaml;
using System.Threading.Tasks;
using OwncloudUniversal.Services.SettingsServices;
using Windows.ApplicationModel.Activation;
using Template10.Controls;
using Template10.Common;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Data;
using Windows.Web.Http;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.UI;
using OwncloudUniversal.Services;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Synchronization.SQLite;
using OwncloudUniversal.Utils;
using OwncloudUniversal.ViewModels;
using OwncloudUniversal.Views;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization.Configuration;

namespace OwncloudUniversal
{
    /// Documentation on APIs used in this page:
    /// https://github.com/Windows-XAML/Template10/wiki

    [Bindable]
    sealed partial class App : BootStrapper
    {
        public App()
        {
            InitializeComponent();
            SplashFactory = (e) => new Views.Splash(e);

            this.UnhandledException += ExceptionHandlerService.OnUnhandledException;

            #region app settings

            // some settings must be set in app.constructor
            var settings = SettingsService.Instance;
            RequestedTheme = settings.AppTheme;
            CacheMaxDuration = settings.CacheMaxDuration;
            AutoSuspendAllFrames = true;
            AutoRestoreAfterTerminated = true;
            AutoExtendExecutionSession = true;

            #endregion
        }

        public static readonly ResourceLoader ResourceLoader = new ResourceLoader();

        public override UIElement CreateRootElement(IActivatedEventArgs e)
        {
            ThemeHelper.UpdateTitleBar();
            var view = ApplicationView.GetForCurrentView();
            view.ShowStandardSystemOverlays();
            var service = NavigationServiceFactory(BackButton.Attach, ExistingContent.Include);
            return new ModalDialog
            {
                DisableBackButtonWhenModal = true,
                Content = new Views.Shell(service),
            };
        }

        public override Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            // TODO: add your long-running task here
            SQLiteClient.Init();
            ImageCache.Instance.MaxMemoryCacheCount = Int32.MaxValue;
            if (startKind == StartKind.Launch)
            {
                if (Configuration.IsFirstRun)
                {
                    Configuration.RemoveCredentials();
                    Configuration.IsBackgroundTaskEnabled = false;
                    NavigationService.Navigate(typeof(WelcomePage));
                }
                else
                {
                    NavigationService.Navigate(typeof(FilesPage));
                }

                if (Configuration.IsBackgroundTaskEnabled)
                {
                    var settings = new SettingsPageViewModel();
                    settings.SettingsPartViewModel.BackgroundTaskEnabled = true;
                }
            }
            return Task.CompletedTask;
        }
    }
}

