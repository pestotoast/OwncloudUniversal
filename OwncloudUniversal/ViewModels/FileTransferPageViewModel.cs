using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Services;
using OwncloudUniversal.Shared.Model;
using OwncloudUniversal.WebDav.Model;
using Template10.Mvvm;
using Template10.Utils;

namespace OwncloudUniversal.ViewModels
{
    public class FileTransferPageViewModel : ViewModelBase
    {
        private CancellationTokenSource _token;
        private ObservableCollection<IBackgroundTransferOperation> _operations;
        public ICommand OpenFileCommand { get; private set; }

        public FileTransferPageViewModel()
        {
            OpenFileCommand = new DelegateCommand<DownloadOperation>(async download => await Launcher.LaunchFileAsync(download.ResultFile));
        }

        public ObservableCollection<IBackgroundTransferOperation> OperationsList
        {
            get { return _operations; }
            private set
            {
                _operations = value;
                RaisePropertyChanged();
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode,
            IDictionary<string, object> state)
        {
            _token = new CancellationTokenSource();
            if (parameter is DavItem)
            {
                var files = await PickOpenFile();
                if (files.Count > 0)
                    StartUpload(files, (DavItem)parameter);
                else
                {
                    NavigationService.GoBack();
                }
            }
            else if(parameter is List<DavItem>)
            {
                var items = (List<DavItem>) parameter;
                var folder = await PickFolder();
                if(folder!=null)
                    await StartDownload(folder, items.Cast<BaseItem>().ToList());
                else
                {
                    NavigationService.GoBack();
                }
            }
            await base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override async Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            var service = await WebDavNavigationService.InintializeAsync();
            service.SetNavigationService(NavigationService);
            await service.ReloadAsync();
            await base.OnNavigatedFromAsync(pageState, suspending);
        }

        #region Upload

        private async Task<List<StorageFile>> PickOpenFile()
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            var files = await picker.PickMultipleFilesAsync();
            return files.ToList();
        }

        private void StartUpload(List<StorageFile> files, DavItem uploadFolder)
        {
            var service = new WebDavItemService();

            OperationsList = service.CreateUpload(uploadFolder, files).Cast<IBackgroundTransferOperation>().ToObservableCollection();
            if (OperationsList.Count > 0)
            {
                Progress<UploadOperation> progressCallback = new Progress<UploadOperation>(OnUploadProgressChanged);
                var task = ((UploadOperation)OperationsList.First()).StartAsync().AsTask(_token.Token, progressCallback);
                task.ContinueWith(OnUploadCompleted);
            }
        }

        private void OnUploadProgressChanged(UploadOperation upload)
        {
            this.Dispatcher.DispatchAsync(() =>
            {
                var index = OperationsList.IndexOf(upload);
                OperationsList[index] = upload;
            });
        }

        private async Task OnUploadCompleted(Task<UploadOperation> task)
        {
            var upload = await task;
            OnUploadProgressChanged(upload);
            int index = OperationsList.IndexOf(upload);
            if (OperationsList.Count > index+1)
            {
                Progress<UploadOperation> progressCallback = new Progress<UploadOperation>(OnUploadProgressChanged);
                var uploadTask = ((UploadOperation)OperationsList[index + 1]).StartAsync().AsTask(_token.Token, progressCallback);
                await uploadTask.ContinueWith(OnUploadCompleted);
            }
        }

        #endregion

        #region Download

        private async Task StartDownload(StorageFolder folder, List<BaseItem> filesToUpload)
        {
            var service = new WebDavItemService();
            OperationsList = (await service.CreateDownloadAsync(filesToUpload, folder)).Cast<IBackgroundTransferOperation>().ToObservableCollection();

            Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(OnDownloadProgressChanged);
            var task = ((DownloadOperation)OperationsList.First()).StartAsync().AsTask(_token.Token, progressCallback);
            await task.ContinueWith(OnDownloadCompleted);
        }

        private void OnDownloadProgressChanged(DownloadOperation download)
        {
            Dispatcher.DispatchAsync(() =>
            {
                var index = OperationsList.IndexOf(download);
                OperationsList[index] = download;
                RaisePropertyChanged("Progress");
            });
        }

        private async  Task OnDownloadCompleted(Task<DownloadOperation> task)
        {
            var download = await task;
            OnDownloadProgressChanged(download);
            int index = OperationsList.IndexOf(download);
            Debug.WriteLine(download.Progress.Status);
            if (OperationsList.Count > index + 1)
            {
                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(OnDownloadProgressChanged);
                var uploadTask = ((DownloadOperation)OperationsList[index + 1]).StartAsync().AsTask(_token.Token, progressCallback);
                await uploadTask.ContinueWith(OnDownloadCompleted);
            }

        }

        private async Task<StorageFolder> PickFolder()
        {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add(".");
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            return await picker.PickSingleFolderAsync();
        }

        #endregion
    }
}
