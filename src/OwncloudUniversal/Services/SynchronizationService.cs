using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Devices.Power;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.System.Display;
using Windows.System.Power;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using OwncloudUniversal.Synchronization;
using OwncloudUniversal.Synchronization.LocalFileSystem;
using OwncloudUniversal.Synchronization.Synchronisation;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.Synchronization.Configuration;
using Template10.Mvvm;

namespace OwncloudUniversal.Services
{
    class SynchronizationService
    {
        private BackgroundSyncProcess _worker;

        private static SynchronizationService _instance;

        public static SynchronizationService GetInstance()
        {
            if (_instance == null)
                _instance = new SynchronizationService();
            return _instance;
        }

        private SynchronizationService()
        {
            _Initialize();
        }

        private void _Initialize()
        {
            var fileSystem = new FileSystemAdapter(false, null);
            var webDav = new WebDavAdapter(false, Configuration.ServerUrl, Configuration.Credential, fileSystem);
            fileSystem.LinkedAdapter = webDav;
            _worker = new BackgroundSyncProcess(fileSystem, webDav, false);
        }

        public async Task StartSyncProcess()
        {
            var run = true;
            if (!ChargerAndNetworkAvailable())
            {
                var dialog = new ContentDialog
                {
                    Content = App.ResourceLoader.GetString("SyncWarning"),
                    PrimaryButtonText = App.ResourceLoader.GetString("yes"),
                    SecondaryButtonText = App.ResourceLoader.GetString("no")
                };
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    run = false;
                }
            }
            if (run)
            {
                await Task.Factory.StartNew(async () =>
                {
                    DisplayRequest displayRequest = null;
                    try
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                displayRequest = new DisplayRequest();
                                displayRequest.RequestActive();
                            });
                        _Initialize();
                        await _worker.Run();
                    }
                    catch (Exception e)
                    {
                        if (Windows.ApplicationModel.Core.CoreApplication.Views.Count > 0)
                        {
                            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ExecutionContext.Instance.Status = ExecutionStatus.Error;
                            });
                        }
                        await LogHelper.Write(e.Message);
                        await LogHelper.Write(e.StackTrace);
                    }
                    finally
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                displayRequest?.RequestRelease();
                            });
                    }
                });
            }
        }

        private bool ChargerAndNetworkAvailable()
        {
            var result = true;
            var battery = Battery.AggregateBattery.GetReport();
            if (battery.Status == BatteryStatus.Discharging)
                result = false;
            var connectionCost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
            if (!(connectionCost?.NetworkCostType == NetworkCostType.Unknown || connectionCost?.NetworkCostType == NetworkCostType.Unrestricted))
            {
                result = false;
            }
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                result = false;
            }
            return result;
        }
    }
}
