using AdminPanel.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdminPanel.ViewModels
{
    public class EngineViewModel : ProxyClient
    {
        public bool IsAuth { get; private set; }

        public EngineViewModel()
        {
            State = ServerState.Disconnect;
            RobotsVm = new RobotsViewModel(PositionsVm);
            ChangeLocal();
            Task.Run(() =>
            {
                while (!IsDisposed)
                {
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
                    }
                    catch (Exception e)
                    {
                        SetState(ServerState.Disconnect);
                    }
                    Thread.Sleep(10000);
                }
            });
        }
        public bool IsDisposed { get; private set; } = false;

        public void Kill()
        {
            IsDisposed = true;
            Close();
        }

        public bool IsHaveBadPortfolio()
        {
            if (ServersVm.Servers.Count(s => s.State == ServerState.Connect) != 0 && PortfoliosVm.Portfolios.Count == 0)
            {
                return true;
            }

            return false;
        }

        public string ProcessId { get; private set; }

        private string _engineName;
        public string EngineName
        {
            get { return _engineName; }
            set { SetProperty(ref _engineName, value, () => EngineName); }
        }

        private string _ip;
        public string Ip
        {
            get { return _ip; }
            set { SetProperty(ref _ip, value, () => Ip); }
        }

        private string _port;
        public string Port
        {
            get { return _port; }
            set { SetProperty(ref _port, value, () => Port); }
        }

        private string _ram;
        public string Ram
        {
            get { return _ram; }
            set { SetProperty(ref _ram, value, () => Ram); }
        }

        private string _cpu;
        public string Cpu
        {
            get { return _cpu; }
            set { SetProperty(ref _cpu, value, () => Cpu); }
        }

        private int _rebootRam;
        public int RebootRam
        {
            get { return _rebootRam; }
            set { SetProperty(ref _rebootRam, value, () => RebootRam); }
        }

        private string _token;

        public string Token
        {
            get { return _token; }
            set { SetProperty(ref _token, value, () => Token); }
        }

        private ServerState _state;

        public ServerState State
        {
            get { return _state; }
            set { SetProperty(ref _state, value, () => State); }
        }

        public ServersViewModel ServersVm = new ServersViewModel();

        public PositionsViewModel PositionsVm = new PositionsViewModel();

        public RobotsViewModel RobotsVm;
        
        public PortfoliosViewModel PortfoliosVm = new PortfoliosViewModel();

        public OrdersViewModel OrdersVm = new OrdersViewModel();
        
        public void ChangeLocal()
        {
            ServersVm.ChangeLocal();
            RobotsVm.ChangeLocal();
            PositionsVm.ChangeLocal();
            PortfoliosVm.ChangeLocal();
            OrdersVm.ChangeLocal();
        }

        public string GetStringForSave()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(EngineName).Append(";");
            sb.Append(Ip).Append(";");
            sb.Append(Port).Append(";");
            sb.Append(Token).Append(";");
            sb.Append(RebootRam).Append(";");

            return sb.ToString();
        }

        public void LoadFromString(string data)
        {
            var list = data.Split(";");
            EngineName = list[0];
            Ip = list[1];
            Port = list[2];
            Token = list[3];
            Int32.TryParse(list[4], out var reboot);
            RebootRam = reboot;
        }

        public DateTime LastTimeReboot = DateTime.Now;

        public bool NeedRebootRam()
        {
            if (RebootRam <= 100 || Ram == null)
            {
                return false;
            }
            if (Convert.ToDecimal(Ram?.Replace(".", ",")) > RebootRam)
            {
                return true;
            }

            return false;
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
                else if (message.StartsWith("processId_"))
                {
                    var data = message.Replace("processId_", "");
                    var jt = JToken.Parse(data);

                    ProcessId = jt["ProcessId"].Value<string>();
                }
                else if(message.StartsWith("allBotsList_"))
                {
                    var data = message.Replace("allBotsList_", "");
                    var jt = JArray.Parse(data);
                    RobotsVm.UpdateBotList(jt);
                }
                else if (message.StartsWith("serverState_"))
                {
                    var data = message.Replace("serverState_", "");
                    var jt = JToken.Parse(data);
                    ServersVm.UpdateServerState(jt);
                }
                else if (message.StartsWith("serverLog_"))
                {
                    var data = message.Replace("serverLog_", "");
                    var jt = JToken.Parse(data);
                    ServersVm.HandleServerLog(jt);
                }
                else if (message.StartsWith("positionsSnapshot_"))
                {
                    var data = message.Replace("positionsSnapshot_", "");
                    var jt = JArray.Parse(data);
                    PositionsVm.UpdateTable(jt);
                }
                else if (message.StartsWith("ordersSnapshot_"))
                {
                    var data = message.Replace("ordersSnapshot_", "");
                    var jt = JArray.Parse(data);
                    OrdersVm.UpdateTable(jt);
                }
                else if (message.StartsWith("portfoliosSnapshot_"))
                {
                    var data = message.Replace("portfoliosSnapshot_", "");
                    var jt = JArray.Parse(data);
                    PortfoliosVm.UpdateTable(jt);
                }
                else if (message.StartsWith("botState_"))
                {
                    var data = message.Replace("botState_", "");
                    var jt = JToken.Parse(data);
                    RobotsVm.UpdateTable(jt);
                }
                else if (message.StartsWith("botLog_"))
                {
                    var data = message.Replace("botLog_", "");
                    var jt = JToken.Parse(data);
                    RobotsVm.UpdateBotLog(jt);
                }
                else if (message.StartsWith("botParams_"))
                {
                    var data = message.Replace("botParams_", "");
                    var jt = JToken.Parse(data);
                    RobotsVm.UpdateBotParams(jt);
                }
                else if (message.StartsWith("osCounter_"))
                {
                    var data = message.Replace("osCounter_", "");
                    var jt = JToken.Parse(data);

                    Ram = jt["Ram"].Value<string>();
                    Cpu = jt["Cpu"].Value<string>();
                }
                else if (message == "close")
                {
                    SetState(ServerState.Disconnect);
                    IsAuth = false;
                    Close();
                }
            }
            catch (Exception e)
            {
                return;
            }
        }
        
        private void SetState(ServerState state)
        {
            State = state;
        }

        #endregion
    }
}
