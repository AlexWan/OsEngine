using AdminPanel.Entity;
using AdminPanel.Language;
using AdminPanel.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdminPanel.ViewModels
{
    public class ClientViewModel : ProxyClient
    {
        public bool IsAuth { get; private set; }
        private readonly MainWindow _mainWindow;
        private readonly ApplicationViewModel _app;

        public ClientViewModel(MainWindow mainWindow, ApplicationViewModel app)
        {
            _app = app;
            _mainWindow = mainWindow;
            Task.Run(() =>
            {
                while (!IsDisposed)
                {
                    Thread.Sleep(5000);
                    try
                    {
                        if (!Connected())
                        {
                            SetState(ServerState.Disconnect);
                            TryConnect(_ip, _port, _token);
                        }
                        else if (!IsAuth)
                        {
                            SetState(ServerState.Disconnect);
                            var message = $"{{\"Token\":\"{Token}\"}}";
                            Send(message);
                        }
                        else
                        {
                            Send("ping");
                        }
                        
                        CheckAllStatus();
                        CheckEnginesRam();
                    }
                    catch (Exception e)
                    {
                        SetState(ServerState.Disconnect);
                    }
                }
            });
        }

        private void CheckEnginesRam()
        {
            foreach (var engineViewModel in Engines)
            {
                if (engineViewModel.NeedRebootRam() && engineViewModel.LastTimeReboot.AddMinutes(1) < DateTime.Now)
                {
                    engineViewModel.LastTimeReboot = DateTime.Now;
                    Reboot(engineViewModel.ProcessId);
                }
            }
        }

        public void Reboot(string processId)
        {
            var message = $"reboot_{processId}";
            Send(message);
        }

        public void SetActiveTab(int index)
        {
            _mainWindow.SetActiveTab(Name);
            SelectedTabIndex = index;
        }

        public bool IsDisposed { get; private set; } = false;

        public void Kill()
        {
            IsDisposed = true;
            Close();
        }

        #region General

        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                SetProperty(ref _name, value, () => Name);
                OnChanged();
            }
        }

        private string _comment;
        public string Comment
        {
            get { return _comment; }
            set
            {
                SetProperty(ref _comment, value, () => Comment);
                OnChanged();
            }
        }

        private ServerState _slaveState = ServerState.Disconnect;

        public ServerState SlaveState
        {
            get { return _slaveState; }
            set { SetProperty(ref _slaveState, value, () => SlaveState); }
        }

        private Status _status = Status.Ok;

        public Status Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value, () => Status); }
        }

        #endregion

        #region Connection

        private string _ip;
        public string Ip
        {
            get { return _ip; }
            set
            {
                SetProperty(ref _ip, value, () => Ip);
                foreach (var engineViewModel in Engines)
                {
                    engineViewModel.Ip = value;
                }
                OnChanged();
            }
        }

        private string _port;
        public string Port
        {
            get { return _port; }
            set
            {
                SetProperty(ref _port, value, () => Port);
                OnChanged();
            }
        }

        private string _token;
        public string Token
        {
            get { return _token; }
            set
            {
                SetProperty(ref _token, value, () => Token);
                OnChanged();
            }
        }

        #endregion

        #region Cpu and Ram
        private string _cpu;

        public string Cpu
        {
            get { return _cpu; }
            set { SetProperty(ref _cpu, value, () => Cpu); }
        }

        private decimal _cpuFree;

        public decimal SetCpu
        {
            set
            {
                _cpuFree = value;
                Cpu = "free: " + _cpuFree + " %";
            }
        }

        private string _ram;

        public string Ram
        {
            get { return _ram; }
            set { SetProperty(ref _ram, value, () => Ram); }
        }

        private decimal _ramAll;

        public decimal SetRamAll
        {
            set
            {
                _ramAll = value;
                ConvertRamValue();
            }
        }

        private decimal _ramFree;

        public decimal SetRamFree
        {
            set
            {
                _ramFree = value;
                ConvertRamValue();
            }
        }

        private void ConvertRamValue()
        {
            Ram = "all: " + _ramAll + "  " + "free: " + _ramFree;
        }
        #endregion

        #region Localization

        private string _nameHeader;

        public string NameHeader
        {
            get { return _nameHeader; }
            set { SetProperty(ref _nameHeader, value, () => NameHeader); }
        }

        private string _stateHeader;

        public string StateHeader
        {
            get { return _stateHeader; }

            set { SetProperty(ref _stateHeader, value, () => StateHeader); }
        }

        private string _tokenHeader;

        public string TokenHeader
        {
            get { return _tokenHeader; }
            set { SetProperty(ref _tokenHeader, value, () => TokenHeader); }
        }

        private string _portHeader;

        public string PortHeader
        {
            get { return _portHeader; }

            set { SetProperty(ref _portHeader, value, () => PortHeader); }
        }

        private string _ramHeader;

        public string RamHeader
        {
            get { return _ramHeader; }

            set { SetProperty(ref _ramHeader, value, () => RamHeader); }
        }

        private string _rebootRamHeader;

        public string RebootRamHeader
        {
            get { return _rebootRamHeader; }

            set { SetProperty(ref _rebootRamHeader, value, () => RebootRamHeader); }
        }

        private string _cpuHeader;

        public string CpuHeader
        {
            get { return _cpuHeader; }

            set { SetProperty(ref _cpuHeader, value, () => CpuHeader); }
        }

        private string _tabMainHeader = OsLocalization.MainWindow.TabMain;

        public string TabMainHeader
        {
            get { return _tabMainHeader; }
            set { SetProperty(ref _tabMainHeader, value, () => TabMainHeader); }
        }

        private string _tabServersHeader = OsLocalization.MainWindow.TabServers;

        public string TabServersHeader
        {
            get { return _tabServersHeader; }
            set { SetProperty(ref _tabServersHeader, value, () => TabServersHeader); }
        }

        private string _tabRobotsHeader = OsLocalization.MainWindow.TabRobots;

        public string TabRobotsHeader
        {
            get { return _tabRobotsHeader; }
            set { SetProperty(ref _tabRobotsHeader, value, () => TabRobotsHeader); }
        }

        private string _tabAllPositionsHeader = OsLocalization.MainWindow.TabAllPositions;

        public string TabAllPositionsHeader
        {
            get { return _tabAllPositionsHeader; }
            set { SetProperty(ref _tabAllPositionsHeader, value, () => TabAllPositionsHeader); }
        }

        private string _tabPortfolioHeader = OsLocalization.MainWindow.TabPortfolio;

        public string TabPortfolioHeader
        {
            get { return _tabPortfolioHeader; }
            set { SetProperty(ref _tabPortfolioHeader, value, () => TabPortfolioHeader); }
        }

        private string _tabOrdersHeader = OsLocalization.MainWindow.TabOrders;

        public string TabOrdersHeader
        {
            get { return _tabOrdersHeader; }
            set { SetProperty(ref _tabOrdersHeader, value, () => TabOrdersHeader); }
        }
        private string _btnAddContent = OsLocalization.Entity.BtnAdd;
        public string BtnAddContent
        {
            get { return _btnAddContent; }
            set { SetProperty(ref _btnAddContent, value, () => BtnAddContent); }
        }

        private string _btnEditContent = OsLocalization.Entity.BtnEdit;
        public string BtnEditContent
        {
            get { return _btnEditContent; }
            set { SetProperty(ref _btnEditContent, value, () => BtnEditContent); }
        }

        private string _btnDeleteContent = OsLocalization.Entity.BtnDelete;
        public string BtnDeleteContent
        {
            get { return _btnDeleteContent; }
            set { SetProperty(ref _btnDeleteContent, value, () => BtnDeleteContent); }
        }


        private void ChangeClientLocal()
        {
            NameHeader = OsLocalization.Entity.SecuritiesColumn1;
            StateHeader = OsLocalization.Entity.ColumnServers2;
            TokenHeader = OsLocalization.Entity.TokenHeader;

            PortHeader = OsLocalization.Entity.PortHeader;
            RamHeader = OsLocalization.Entity.RamHeader;
            RebootRamHeader = OsLocalization.Entity.RebootRamHeader;
            CpuHeader = OsLocalization.Entity.CpuHeader;

            TabMainHeader = OsLocalization.MainWindow.TabMain;
            TabServersHeader = OsLocalization.MainWindow.TabServers;
            TabRobotsHeader = OsLocalization.MainWindow.TabRobots;
            TabAllPositionsHeader = OsLocalization.MainWindow.TabAllPositions;
            TabPortfolioHeader = OsLocalization.MainWindow.TabPortfolio;
            TabOrdersHeader = OsLocalization.MainWindow.TabOrders;

            BtnAddContent = OsLocalization.Entity.BtnAdd;
            BtnEditContent = OsLocalization.Entity.BtnEdit;
            BtnDeleteContent = OsLocalization.Entity.BtnDelete;

            OsEngineHeader = OsLocalization.Entity.OsEngineHeader;
            SlaveHeader = OsLocalization.Entity.SlaveHeader;
            ServersHeader = OsLocalization.Entity.ServersHeader;
            RobotsHeader = OsLocalization.Entity.RobotsHeader;
            AllPositionsHeader = OsLocalization.Entity.AllPositionsHeader;
            PortfoliosHeader = OsLocalization.Entity.PortfoliosHeader;
            OrdersHeader = OsLocalization.Entity.OrdersHeader;
        }

        public void ChangeLocal()
        {
            ChangeClientLocal();
            foreach (var engineViewModel in Engines)
            {
                engineViewModel.ChangeLocal();
            }
        }

        #endregion

        #region Preview

        private string _osEngineHeader = OsLocalization.Entity.OsEngineHeader;
        public string OsEngineHeader
        {
            get { return _osEngineHeader; }
            set{ SetProperty(ref _osEngineHeader, value, () => OsEngineHeader); }
        }

        private string _slaveHeader = OsLocalization.Entity.SlaveHeader;
        public string SlaveHeader
        {
            get { return _slaveHeader; }
            set { SetProperty(ref _slaveHeader, value, () => SlaveHeader); }
        }

        private string _serversHeader = OsLocalization.Entity.ServersHeader;
        public string ServersHeader
        {
            get { return _serversHeader; }
            set { SetProperty(ref _serversHeader, value, () => ServersHeader); }
        }

        private string _robotsHeader = OsLocalization.Entity.RobotsHeader;
        public string RobotsHeader
        {
            get { return _robotsHeader; }
            set { SetProperty(ref _robotsHeader, value, () => RobotsHeader); }
        }

        private string _allPositionsHeader = OsLocalization.Entity.AllPositionsHeader;
        public string AllPositionsHeader
        {
            get { return _allPositionsHeader; }
            set { SetProperty(ref _allPositionsHeader, value, () => AllPositionsHeader); }
        }

        private string _portfoliosHeader = OsLocalization.Entity.PortfoliosHeader;
        public string PortfoliosHeader
        {
            get { return _portfoliosHeader; }
            set { SetProperty(ref _portfoliosHeader, value, () => PortfoliosHeader); }
        }

        private string _ordersHeader = OsLocalization.Entity.OrdersHeader;
        public string OrdersHeader
        {
            get { return _ordersHeader; }
            set { SetProperty(ref _ordersHeader, value, () => OrdersHeader); }
        }

        #endregion

        private int _selectedTabIndex;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { SetProperty(ref _selectedTabIndex, value, () => SelectedTabIndex); }
        }

        private EngineViewModel _selectedEngine;

        public EngineViewModel SelectedEngine
        {
            get => _selectedEngine;
            set { SetProperty(ref _selectedEngine, value, () => SelectedEngine);}
        }

        public ObservableCollection<EngineViewModel> Engines { get; set; } = new ObservableCollection<EngineViewModel>();

        public override string ToString()
        {
            return Name;
        }

        public event Action Changed;

        public void OnChanged()
        {
            Changed?.Invoke();
        }

        public void AddEngine(EngineViewModel engine)
        {
            engine.Ip = Ip;
            Engines.Add(engine);
            SelectedEngine = engine;
            OnChanged();
        }

        public void RemoveSelected()
        {
            SelectedEngine.Kill();
            Engines.Remove(SelectedEngine);
            if (Engines.Count != 0)
            {
                SelectedEngine = Engines[0];
            }
            OnChanged();
        }

        #region Proxy

        public override void HandleData(string message)
        {
            SetState(ServerState.Connect);

            try
            {
                if (message == "auth")
                {
                    IsAuth = true;
                }
                else if (message.StartsWith("counter_"))
                {
                    var data = message.Replace("counter_", "");
                    var counter = JsonConvert.DeserializeObject<Counter>(data);

                    SetCpu = Math.Round(counter.CpuFree);
                    SetRamAll = counter.RamAll;
                    SetRamFree = counter.RamFree;
                }
                else if (message=="close")
                {
                    SetState(ServerState.Disconnect);
                    IsAuth = false;
                    Close();
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void SetState(ServerState state)
        {
            SlaveState = state;
        }

        #endregion

        public string GetStringForSave()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Name).Append(";");
            sb.Append(Comment).Append(";");
            sb.Append(Ip).Append(";");
            sb.Append(Port).Append(";");
            sb.Append(Token).Append(";");

            foreach (var engine in Engines)
            {
                sb.Append("#");
                sb.Append(engine.GetStringForSave());
            }

            return sb.ToString();
        }

        public void LoadFromString(string data)
        {
            var all = data.Split("#");

            var list = all[0].Split(";");
            Name = list[0];
            Comment = list[1];
            Ip = list[2];
            Port = list[3];
            Token = list[4];

            for (int i = 1; i < all.Length; i++)
            {
                var engine = new EngineViewModel();
                engine.LoadFromString(all[i]);
                AddEngine(engine);
            }
        }

        #region status

        private void SendInfoMessage(Status oldValue, Status newValue, string system)
        {
            if (newValue != oldValue && newValue != Status.Ok)
            {
                var msg = $"Client - {Name}, system - {system} have status - {newValue}";
                _app.SendAlert(msg, newValue);
            }
        }

        private Status _engineStatus = Status.Ok;

        public Status EngineStatus
        {
            get { return _engineStatus; }
            set
            {
                SendInfoMessage(_engineStatus, value, "OsEngine");
                SetProperty(ref _engineStatus, value, () => EngineStatus);
            }
        }

        private Status _slaveStatus = Status.Ok;

        public Status SlaveStatus
        {
            get { return _slaveStatus; }
            set
            {
                SendInfoMessage(_slaveStatus, value, "Slave");
                SetProperty(ref _slaveStatus, value, () => SlaveStatus);
            }
        }

        private Status _serversStatus = Status.Ok;

        public Status ServersStatus
        {
            get { return _serversStatus; }
            set
            {
                SendInfoMessage(_serversStatus, value, "Servers");
                SetProperty(ref _serversStatus, value, () => ServersStatus);
            }
        }

        private Status _robotsStatus = Status.Ok;

        public Status RobotsStatus
        {
            get { return _robotsStatus; }
            set
            {
                SendInfoMessage(_robotsStatus, value, "Robots");
                SetProperty(ref _robotsStatus, value, () => RobotsStatus);
            }
        }

        private Status _allPositionsStatus = Status.Ok;

        public Status AllPositionsStatus
        {
            get { return _allPositionsStatus; }
            set
            {
                SendInfoMessage(_allPositionsStatus, value, "All positions");
                SetProperty(ref _allPositionsStatus, value, () => AllPositionsStatus);
            }
        }

        private Status _portfolioStatus = Status.Ok;

        public Status PortfolioStatus
        {
            get { return _portfolioStatus; }
            set
            {
                SendInfoMessage(_portfolioStatus, value, "Portfolios");
                SetProperty(ref _portfolioStatus, value, () => PortfolioStatus);
            }
        }

        private Status _ordersStatus = Status.Ok;

        public Status OrdersStatus
        {
            get { return _ordersStatus; }
            set
            {
                SendInfoMessage(_ordersStatus, value, "Orders");
                SetProperty(ref _ordersStatus, value, () => OrdersStatus);
            }
        }

        #endregion
        
        public void CheckAllStatus()
        {
            if (SlaveState == ServerState.Connect)
            {
                SlaveStatus = Status.Ok;
            }
            else
            {
                SlaveStatus = Status.Danger;
            }

            var badEngines = Engines.Where(e => e.State == ServerState.Disconnect);

            if (badEngines.Count() != 0)
            {
                EngineStatus = Status.Error;
            }
            else
            {
                EngineStatus = Status.Ok;
            }

            var badServers = new List<Server>();
            var allDangerRobots = new List<Robot>();
            var allErrorRobots = new List<Robot>();
            var allRobotsWithBadPositions = new List<Robot>();
            var allBadPortfolios = new List<Portfolio>();
            var allBadOrders = new List<Order>();

            foreach (var engineViewModel in Engines)
            {
                var badS = engineViewModel.ServersVm.Servers.Where(s => s.State == ServerState.Disconnect);
                badServers.AddRange(badS);

                var dangerRobots = engineViewModel.RobotsVm.Robots.Where(r => r.Status == Status.Danger);
                allDangerRobots.AddRange(dangerRobots);

                var errorRobots = engineViewModel.RobotsVm.Robots.Where(r => r.Status == Status.Error);
                allErrorRobots.AddRange(errorRobots);

                var robotsWithBadPositions = engineViewModel.RobotsVm.Robots.Where(r => r.PositionsStatus == Status.Error);
                allRobotsWithBadPositions.AddRange(robotsWithBadPositions);

                var badPortfolios = engineViewModel.PortfoliosVm.Portfolios.Where(p => p.ValueBegin <= 0 || p.ValueCurrent <= 0);
                allBadPortfolios.AddRange(badPortfolios);

                var badOrders = engineViewModel.OrdersVm.Orders.Where(o => o.State == "Unknown");
                allBadOrders.AddRange(badOrders);
            }

            if (badServers.Count != 0)
            {
                ServersStatus = Status.Danger;
            }
            else
            {
                ServersStatus = Status.Ok;
            }

            if (allErrorRobots.Count == 0)
            {
                if (allDangerRobots.Count == 0)
                {
                    RobotsStatus = Status.Ok;
                }
                else
                {
                    RobotsStatus = Status.Danger;
                }
            }
            else
            {
                RobotsStatus = Status.Error;
            }

            if (allRobotsWithBadPositions.Count != 0)
            {
                AllPositionsStatus = Status.Error;
            }
            else
            {
                AllPositionsStatus = Status.Ok;
            }

            if (allBadPortfolios.Count != 0 || Engines.Count(e=>e.IsHaveBadPortfolio()) != 0)
            {
                PortfolioStatus = Status.Error;
            }
            else
            {
                PortfolioStatus = Status.Ok;
            }

            if (allBadOrders.Count != 0 && _lastTimeDetectedBadOrder == DateTime.MaxValue)
            {
                _lastTimeDetectedBadOrder = DateTime.Now;
            }
            else
            {
                OrdersStatus = Status.Ok;
            }
            
            if (_lastTimeDetectedBadOrder != DateTime.MaxValue)
            {
                var timeLifeBadOrder = (DateTime.Now - _lastTimeDetectedBadOrder).TotalMinutes;
                if (timeLifeBadOrder >= 1)
                {
                    OrdersStatus = Status.Error;
                }
            }

            SetMainStatus();
        }

        private DateTime _lastTimeDetectedBadOrder = DateTime.MaxValue;

        private void SetMainStatus()
        {
            Status = Status.Ok;

            if (EngineStatus == Status.Danger ||
                SlaveStatus == Status.Danger ||
                ServersStatus == Status.Danger ||
                RobotsStatus == Status.Danger ||
                AllPositionsStatus == Status.Danger ||
                PortfolioStatus == Status.Danger ||
                OrdersStatus == Status.Danger)
            {
                Status = Status.Danger;
            }

            if (EngineStatus == Status.Error ||
                SlaveStatus == Status.Error ||
                ServersStatus == Status.Error ||
                RobotsStatus == Status.Error ||
                AllPositionsStatus == Status.Error ||
                PortfolioStatus == Status.Error ||
                OrdersStatus == Status.Error)
            {
                Status = Status.Error;
            }
        }
    }
}
