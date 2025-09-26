using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

namespace OsEngine.OsTrader.ServerAvailability
{
    internal class ServerAvailabilityMaster
    {
        #region External call

        public static void Activate()
        {
            if (_worker == null)
            {
                ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;

                _worker = new Thread(PingAnalysisThread);
                _worker.Name = "PingAnalysisThread";
                _worker.CurrentCulture = new System.Globalization.CultureInfo("ru-Ru");
                _worker.Start();
            }
        }

        public static void ShowDialog()
        {
            try
            {
                if (_ui == null)
                {
                    _ui = new ServerAvailabilityUi();
                    _ui.Closed += _ui_Closed;
                    _ui.Show();
                }
                else
                {
                    if (_ui.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _ui.WindowState = System.Windows.WindowState.Normal;
                    }

                    _ui.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private static void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        #endregion

        #region Main thread

        private static void PingAnalysisThread()
        {
            while (true)
            {
                try
                {
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_nextTimeForCheckPing > DateTime.Now)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (_currentIpConnectors.Count() == 0)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (!_isTrackPing)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    lock (_lockListIpConnectors)
                    {
                        for (int i = 0; i < _currentIpConnectors.Count(); i++)
                        {
                            var item = _currentIpConnectors[i];

                            if (item.IsOn == false)
                            {
                                item.PingValue = "None";
                                continue;
                            }

                            string pingValue = CheckPing(item.CurrentIpAddres);

                            if (pingValue == null)
                            {
                                item.PingValue = "None";
                            }
                            else
                            {
                                item.PingValue = pingValue;
                            }
                        }
                    }

                    _nextTimeForCheckPing = DateTime.Now.AddSeconds(_checkPingPeriod);

                    if (PingChangeEvent != null)
                    {
                        PingChangeEvent();
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private static string CheckPing(string IpAdress)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(IpAdress);

                    if (reply.Status != IPStatus.Success)
                    {
                        return null;
                    }

                    return reply.RoundtripTime.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Fields

        private static DateTime _nextTimeForCheckPing = DateTime.MinValue;

        private static Thread _worker;

        private static ServerAvailabilityUi _ui;

        private static string _lockListIpConnectors = "lockListIpConnectors";

        #endregion

        #region Events

        public static event Action PingChangeEvent;

        private static void ServerMaster_ServerCreateEvent(IServer server)
        {
            try
            {
                IServerPermission permission = ServerMaster.GetServerPermission(server.ServerType);

                if (permission == null) return;

                if (permission.IpAddresServer == null) return;

                if (_currentIpConnectors.Count == 0)
                {
                    IpAdressConnectorService connectorService = new IpAdressConnectorService();
                    connectorService.IpAddresses = permission.IpAddresServer;
                    connectorService.ServerType = server.ServerType.ToString();
                    connectorService.CurrentIpAddres = permission.IpAddresServer[0];
                    connectorService.PingValue = "None";
                    connectorService.IsOn = true;

                    lock (_lockListIpConnectors)
                    {
                        _currentIpConnectors.Add(connectorService);
                    }
                }
                else
                {
                    bool inStock = false;
                    for (int i = 0; i < _currentIpConnectors.Count; i++)
                    {
                        if (_currentIpConnectors[i].ServerType == server.ServerType.ToString())
                        {
                            inStock = true;
                            break;
                        }
                    }

                    if (!inStock)
                    {
                        IpAdressConnectorService connectorService = new IpAdressConnectorService();
                        connectorService.IpAddresses = permission.IpAddresServer;
                        connectorService.ServerType = server.ServerType.ToString();
                        connectorService.CurrentIpAddres = permission.IpAddresServer[0];
                        connectorService.PingValue = "None";
                        connectorService.IsOn = true;

                        lock (_lockListIpConnectors)
                        {
                            _currentIpConnectors.Add(connectorService);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Properties

        private static List<IpAdressConnectorService> _currentIpConnectors = new List<IpAdressConnectorService>();
        public static List<IpAdressConnectorService> CurrentIpConnectors
        {
            get
            {
                lock (_lockListIpConnectors)
                {
                    return _currentIpConnectors;
                }
            }
        }

        private static double _checkPingPeriod = 10;
        public static double CheckPingPeriod
        {
            get
            {
                return _checkPingPeriod;
            }
            set { _checkPingPeriod = value; }
        }

        private static bool _isTrackPing;
        public static bool IsTrackPing
        {
            get
            {
                return _isTrackPing;
            }
            set
            {
                _isTrackPing = value;
            }
        }

        #endregion

        public class IpAdressConnectorService
        {
            public string PingValue;
            public string CurrentIpAddres;
            public bool IsOn;
            public string[] IpAddresses;
            public string ServerType;
        }
    }
}
