using AdminPanel.Language;
using AdminPanel.Utils;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;

namespace AdminPanel.ViewModels
{
    public class ApplicationViewModel : NotificationObject
    {
        public TelegramApi TlClient = new TelegramApi();
        private readonly MainWindow _mainWindow;
        public ApplicationViewModel(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Clients = new ObservableCollection<ClientViewModel>();
        }

        private ObservableCollection<ClientViewModel> _clients;

        public ObservableCollection<ClientViewModel> Clients
        {
            get => _clients;
            set { SetProperty(ref _clients, value, () => Clients); }
        }

        private ClientViewModel _selectedClient;

        public ClientViewModel SelectedClient
        {
            get => _selectedClient;
            set { SetProperty(ref _selectedClient, value, () => SelectedClient); }
        }

        public void AddClient(ClientViewModel client)
        {
            client.Changed += Save;
            Clients.Add(client);
            SelectedClient = client;
        }
        
        public void RemoveSelectedClient()
        {
            SelectedClient.Kill();
            SelectedClient.Changed -= Save;
            Clients.Remove(SelectedClient);

            if (Clients.Count != 0)
            {
                SelectedClient = Clients[0];
            }
            else
            {
                SelectedClient = null;
            }
        }

        public void SetSelectedClient(string name)
        {
            SelectedClient = Clients.FirstOrDefault(c => c.Name == name);
        }

        public void ChangeLocal()
        {
            ChangeClientLocal();
            foreach (var client in Clients)
            {
                client.ChangeLocal();
            }
        }

        public void SendAlert(string msg, Status status)
        {
            if (UseDanger && status == Status.Danger ||
                UseErrors && status == Status.Error)
            {
                if (UseTelegram)
                {
                    TlClient.SendMessage(msg, Receiver);
                }

                if (UseSound)
                {
                    string path = "";
                    if (status == Status.Danger)
                    {
                        path = "danger.wav";
                    }
                    else if (status == Status.Error)
                    {
                        path = "error.wav";
                    }

                    System.Windows.Resources.StreamResourceInfo res =
                        Application.GetResourceStream(new Uri(path, UriKind.Relative));

                    if (res == null)
                    {
                        return;
                    }
                    SoundPlayer sp = new SoundPlayer(res.Stream);
                    sp.Play();
                }
            }
        }

        private bool _needSave = true;
        public void Save()
        {
            if (!_needSave)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\Clients.txt", false))
                {
                    foreach (var client in Clients)
                    {
                        writer.WriteLine(client.GetStringForSave());
                    }
                    
                    writer.Close();
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\Prime.txt", false))
                {
                    writer.WriteLine(_useTelegram);
                    writer.WriteLine(_key);
                    writer.WriteLine(_token);
                    writer.WriteLine(_phone);
                    writer.WriteLine(_receiver);
                    writer.WriteLine(_useSound);
                    writer.WriteLine(_useDanger);
                    writer.WriteLine(_useErrors);

                    writer.Close();
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public void Load()
        {
            if (!File.Exists(@"Engine\Clients.txt"))
            {
                return;
            }
            try
            {
                _needSave = false;

                if (Clients == null)
                {
                    Clients = new ObservableCollection<ClientViewModel>();
                }
                using (StreamReader reader = new StreamReader(@"Engine\Clients.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        var client = new ClientViewModel(_mainWindow, this);
                        client.LoadFromString(reader.ReadLine());
                        AddClient(client);
                    }

                    reader.Close();
                }

                if (!File.Exists(@"Engine\Prime.txt"))
                {
                    return;
                }
                using (StreamReader reader = new StreamReader(@"Engine\Prime.txt"))
                {
                    UseTelegram =  Convert.ToBoolean(reader.ReadLine());
                    Key = reader.ReadLine();
                    Token = reader.ReadLine();
                    Phone = reader.ReadLine();
                    Receiver = reader.ReadLine();
                    UseSound = Convert.ToBoolean(reader.ReadLine());
                    UseDanger = Convert.ToBoolean(reader.ReadLine());
                    UseErrors = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }

                _needSave = true;
            }
            catch (Exception e)
            {
                //throw;
            }
        }

        #region Settings

        private bool _useTelegram;
        public bool UseTelegram
        {
            get { return _useTelegram; }
            set
            {
                SetProperty(ref _useTelegram, value, () => UseTelegram);
                Save();
            }
        }

        private string _key;
        public string Key
        {
            get { return _key; }
            set
            {
                SetProperty(ref _key, value, () => Key);
                Save();
            }
        }

        private string _token;
        public string Token
        {
            get { return _token; }
            set
            {
                SetProperty(ref _token, value, () => Token);
                Save();
            }
        }

        private string _phone;
        public string Phone
        {
            get { return _phone; }
            set
            {
                SetProperty(ref _phone, value, () => Phone);
                Save();
            }
        }

        private string _receiver;
        public string Receiver
        {
            get { return _receiver; }
            set
            {
                SetProperty(ref _receiver, value, () => Receiver);
                Save();
            }
        }

        private bool _useSound;
        public bool UseSound
        {
            get { return _useSound; }
            set
            {
                SetProperty(ref _useSound, value, () => UseSound);
                Save();
            }
        }

        private bool _useDanger;
        public bool UseDanger
        {
            get { return _useDanger; }
            set
            {
                SetProperty(ref _useDanger, value, () => UseDanger);
                Save();
            }
        }

        private bool _useErrors;
        public bool UseErrors
        {
            get { return _useErrors; }
            set
            {
                SetProperty(ref _useErrors, value, () => UseErrors);
                Save();
            }
        }

        #endregion
        
        public void Close()
        {
            foreach (var client in Clients)
            {
                client.Close();
            }
        }

        #region Local

        private string _nameHeader;
        public string NameHeader
        {
            get { return _nameHeader; }
            set
            {
                SetProperty(ref _nameHeader, value, () => NameHeader);
            }
        }

        private string _systemHeader;
        public string SystemHeader
        {
            get { return _systemHeader; }
            set
            {
                SetProperty(ref _systemHeader, value, () => SystemHeader);
            }
        }

        private string _statusHeader;
        public string StatusHeader
        {
            get { return _statusHeader; }
            set
            {
                SetProperty(ref _statusHeader, value, () => StatusHeader);
            }
        }

        private string _transferHeader;
        public string TransferHeader
        {
            get { return _transferHeader; }
            set
            {
                SetProperty(ref _transferHeader, value, () => TransferHeader);
            }
        }

        private void ChangeClientLocal()
        {
            NameHeader = OsLocalization.Entity.RobotNameHeader;
            SystemHeader = OsLocalization.Entity.SystemHeader;
            StatusHeader = OsLocalization.Entity.StatusHeader;
            TransferHeader = OsLocalization.Entity.TransferHeader;
        }
        #endregion
    }
}
