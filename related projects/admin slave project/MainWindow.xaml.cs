using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using AdminSlave.Model;
using Newtonsoft.Json;
using DataGrid = System.Windows.Controls.DataGrid;
using MessageBox = System.Windows.MessageBox;

namespace AdminSlave
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string RestartBat = "restart_osengine_robots.bat";

        public State State = State.Off;
        private ProcessManager _manager;
        private ObservableCollection<OsEngine> _osEngines = new ObservableCollection<OsEngine>();

        public ObservableCollection<OsEngine> Engines
        {
            get { return _osEngines; }
            set { SetProperty(ref _osEngines, value, () => Engines); }
        }

        public MainWindow()
        {
            InitializeComponent();
            CreateDir();
            SetLabelState(State.Off);
            Load();
            DataGridEngines.ItemsSource = Engines;
            if (AutoStartChb.IsChecked != null && AutoStartChb.IsChecked.Value == true)
            {
                Start();
            }

            _manager = new ProcessManager();

            Task.Run(() => _manager.Start());
            Task.Run(Sender);
        }

        protected override void OnClosed(EventArgs e)
        {
            _messagesToSend.Enqueue("close");
            Thread.Sleep(2000);
            _tcpServer?.Disconnect();
            base.OnClosed(e);
        }

        private string _lastPathToFolder = "";

        private void BtnAddClient_Click(object sender, RoutedEventArgs e)
        {
            AddClient();
        }

        private void AddClient()
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = _lastPathToFolder;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _lastPathToFolder = fbd.SelectedPath;

                var info = Directory.GetFiles(_lastPathToFolder);

                var needFile = info.FirstOrDefault(p => p.EndsWith("OsEngine.exe"));

                if (needFile == null)
                {
                    MessageBox.Show("В выбранном каталоге не обнаружен OsEngine.exe");
                    return;
                }

                if (Engines.FirstOrDefault(o=>o.Path == needFile) != null)
                {
                    MessageBox.Show("OsEngine с таким путем уже добавлен");
                    return;
                }

                var os = new OsEngine(needFile);
                os.State = State.Off;
                os.ProcessId = _manager.GetProcessIdByPath(_lastPathToFolder);
                Engines.Add(os);
            }

            Save();
        }

        private void BtnDeleteClient_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void BtnGenerateToken_OnClick(object sender, RoutedEventArgs e)
        {
            string token = Guid.NewGuid().ToString();
            TextBoxToken.Text = token;
        }

        private void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            Start();
        }

        private TcpServer _tcpServer;

        private void Start()
        {
            Save();

            if (_tcpServer != null)
            {
                _tcpServer.Disconnect();
                _tcpServer = null;
            }

            var permittedIp = TextBoxIp.Text.Replace(" ", "").Split(',');
            var permittedToken = TextBoxToken.Text.Replace(" ", "");

            _tcpServer = new TcpServer();
            _tcpServer.Port = Convert.ToInt32(TextBoxPort.Text);
            
            _tcpServer.Started += TcpServerOnStarted;
            _tcpServer.ClientNeedRebootEvent += TcpServerOnClientNeedRebootEvent;
            _tcpServer.Connect(permittedIp, permittedToken);
        }

        private void TcpServerOnClientNeedRebootEvent(string id)
        {
            InitEnginesId();

            var needOs = Engines.FirstOrDefault(e => e.ProcessId == Convert.ToInt32(id));
            if (needOs!= null)
            {
                ReStartProcess(needOs.Path.Replace("\\OsEngine.exe",  ""));
            }
        }

        private void InitEnginesId()
        {
            foreach (var osEngine in Engines)
            {
                osEngine.ProcessId = _manager.GetProcessIdByPath(osEngine.Path);
            }
        }

        private void TcpServerOnStarted()
        {
            State = State.Active;
            SetLabelState(State.Active);
        }

        private void SetLabelState(State state)
        {
            string strState = "Off";
            Brush brush = Brushes.White;

            switch (state)
            {
                case State.Active:
                    strState = "Active";
                    brush = Brushes.Chartreuse;
                    break;
                case State.NotAsk:
                    strState = "Dont ask";
                    brush = Brushes.Red;
                    break;
            }

            this.Dispatcher.Invoke(() =>
            {
                LabelStateValue.Foreground = brush;
                LabelStateValue.Content = strState;
            });
        }

        private void CreateDir()
        {
            if (!Directory.Exists("Engine"))
            {
                Directory.CreateDirectory("Engine");
            }
        }

        private void Load()
        {
            if (!File.Exists(@"Engine\SettingsKeeper.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\SettingsKeeper.txt"))
                {
                    TextBoxIp.Text = reader.ReadLine();
                    TextBoxPort.Text = reader.ReadLine();
                    TextBoxToken.Text = reader.ReadLine();
                    AutoStartChb.IsChecked = Convert.ToBoolean(reader.ReadLine());
                    _lastPathToFolder = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\SettingsKeeper.txt", false))
                {
                    writer.WriteLine(TextBoxIp.Text);
                    writer.WriteLine(TextBoxPort.Text);
                    writer.WriteLine(TextBoxToken.Text);
                    writer.WriteLine(AutoStartChb.IsChecked);
                    writer.WriteLine(_lastPathToFolder);

                    writer.Close();
                }
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        #region Notification

        protected bool SetProperty<T>(ref T storage, T value, Expression<Func<T>> action)
        {
            if (Equals(storage, value))
                return false;
            storage = value;
            RaisePropertyChanged(action);
            return true;
        }

        protected void RaisePropertyChanged<T>(Expression<Func<T>> action)
        {
            var propertyName = GetPropertyName(action);
            RaisePropertyChanged(propertyName);
        }

        private static string GetPropertyName<T>(Expression<Func<T>> action)
        {
            var expression = (MemberExpression)action.Body;
            var propertyName = expression.Member.Name;
            return propertyName;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private readonly ConcurrentQueue<string> _messagesToSend = new ConcurrentQueue<string>();

        private void Sender()
        {
            while (true)
            {
                _manager.CheckStateEngines(Engines.ToList());

                if (State == State.Active)
                {
                    var counterMessage = GetCounterMessage();
                    _messagesToSend.Enqueue(counterMessage);

                    if (!_messagesToSend.IsEmpty)
                    {
                        if (_messagesToSend.TryDequeue(out var message))
                        {
                            _tcpServer.Send(message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }

                Thread.Sleep(10000);
            }
        }

        private string GetCounterMessage()
        {
            Counter counter = new Counter();

            counter.RamAll = _manager.RamAll;
            counter.RamFree = Math.Round((decimal) _manager.RamFree, 1);
            counter.CpuFree = Math.Round((decimal) (100 - _manager.Cpu));

            var message = JsonConvert.SerializeObject(counter);

            return "counter_" + message;
        }

        private void DataGrid_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                if (grid.SelectedCells.Count == 0)
                {
                    return;
                }
                var cell = grid.CurrentCell;
                grid.SelectedItem = null;
                if (cell.Column.Header?.ToString() == "Path")
                {
                    var path = ((OsEngine)cell.Item).Path;
                    var index = path.LastIndexOf('\\');
                    var newPath = path.Substring(0, index);
                    _lastPathToFolder = newPath;
                    AddClient();
                }

                if (cell.Column.Header == null)
                {
                    var path = ((OsEngine)cell.Item).Path;
                    var index = path.LastIndexOf('\\');
                    var newPath = path.Substring(0, index);
                    ReStartProcess(newPath);
                }
            }
        }

        private void ReStartProcess(string path)
        {
            Process proc = new Process();
            proc.EnableRaisingEvents = true;
            proc.Exited += ProcessOnExited;
            proc.StartInfo.FileName = path + "\\" + RestartBat;
            proc.StartInfo.WorkingDirectory = path;
            proc.Start();
        }

        private void ProcessOnExited(object sender, EventArgs e)
        {
            
        }
    }
}
