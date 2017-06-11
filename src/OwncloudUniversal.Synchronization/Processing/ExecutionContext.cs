using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;

namespace OwncloudUniversal.Synchronization.Synchronisation
{
    public class ExecutionContext : INotifyPropertyChanged
    {
        private int _totalFileCount;
        private int _currentFileNumber;
        private string _currentFileName;
        private ExecutionStatus _status;
        private IBackgroundTransferOperation _backgroundTransferOperation;
        private static ExecutionContext _instance;

        public static ExecutionContext Instance => _instance ?? (_instance = new ExecutionContext());

        private ExecutionContext()
        {
            Status = ExecutionStatus.Ready;
            CurrentFileName = string.Empty;
            CurrentFileNumber = 0;
            TotalFileCount = 0;
        }
        
        public ExecutionStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged("StatusMessage");
                OnPropertyChanged("IsActive");
                OnPropertyChanged("ShowProgress");
            }
        }

        public string CurrentFileName
        {
            get { return WebUtility.UrlDecode(_currentFileName); }
            set
            {
                _currentFileName = value;
                OnPropertyChanged();
            }
        }

        public int CurrentFileNumber
        {
            get { return _currentFileNumber; }
            set
            {
                _currentFileNumber = value;
                OnPropertyChanged();
                OnPropertyChanged("FileText");
            }
        }

        public int TotalFileCount
        {
            get { return _totalFileCount; }
            set
            {
                _totalFileCount = value;
                OnPropertyChanged();
                OnPropertyChanged("FileText");
            }  
        }

        public IBackgroundTransferOperation BackgroundTransferOperation
        {
            get { return _backgroundTransferOperation; }
            set
            {
                _backgroundTransferOperation = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive => !(Status == ExecutionStatus.Finished || Status == ExecutionStatus.Ready || Status == ExecutionStatus.Stopped || Status == ExecutionStatus.Error);

        public bool ShowProgress => Status == ExecutionStatus.Sending || Status == ExecutionStatus.Receiving;

        public string FileText => $"{CurrentFileNumber} / {TotalFileCount}";

        public bool IsBackgroundTask { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
