using AdminPanel.ViewModels;
using System.Collections.ObjectModel;

namespace AdminPanel.Entity
{
    public class Server : NotificationObject
    {
        public string ServerName { get; set; }

        private ServerState _state;

        public ServerState State
        {
            get => _state;
            set { SetProperty(ref _state, value, () => State); }
        }
        
        private ObservableCollection<LogMessage> _log = new ObservableCollection<LogMessage>();

        public ObservableCollection<LogMessage> Log
        {
            get { return _log; }
            set { SetProperty(ref _log, value, () => Log); }
        }
    }
}
