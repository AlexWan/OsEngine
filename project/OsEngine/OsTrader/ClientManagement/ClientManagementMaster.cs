/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.ClientManagement.Gui;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsTrader.ClientManagement
{
    public class ClientManagementMaster: ILogItem
    {
        #region Static part

        public static ClientManagementMaster Instance { get; set; }

        public static void Activate()
        {
            Instance = new ClientManagementMaster();
        }

        #endregion

        #region Service

        public ClientManagementMaster()
        {
            Instance = this;

            if (Log == null)
            {
                Log = new Log("ClientManagementMaster", StartProgram.IsOsTrader);
                Log.Listen(this);
            }

            LoadClients();

            ServerMaster.ShowClientManagerDialogEvent += ServerMaster_ShowClientManagerDialogEvent;
        }

        private void ServerMaster_ShowClientManagerDialogEvent()
        {
            this.ShowDialogClientsMaster();
        }

        public Log Log;

        #endregion

        #region Clients management

        private void LoadClients()
        {
            string dir = Directory.GetCurrentDirectory();
            dir += "\\Engine\\ClientManagement\\";

            if (Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            string[] files = Directory.GetFiles(dir);

            for(int i = 0;i < files.Length;i++)
            {
                string currentFile = files[i];

                if(currentFile.EndsWith("TradeClient.txt") == false)
                {
                    continue;
                }

                TradeClient newClient = new TradeClient();
                newClient.LogMessageEvent += SendNewLogMessage;
                newClient.NameChangeEvent += NewClient_NameChangeEvent;
                newClient.LoadFromFile(currentFile);
                Clients.Add(newClient);
            }
        }
         
        private void NewClient_NameChangeEvent()
        {
           if(ClientChangeNameEvent != null)
            {
                ClientChangeNameEvent();
            }
        }

        public List<TradeClient> Clients = new List<TradeClient>();

        public TradeClient AddNewClient()
        {
            int newClientNumber = 0;

            for(int i = 0;i < Clients.Count;i++)
            {
                if(Clients[i].Number >= newClientNumber)
                {
                    newClientNumber = Clients[i].Number + 1;
                }
            }

            TradeClient newClient = new TradeClient();

            Guid uid = Guid.NewGuid();
            newClient.ClientUid = uid.ToString();

            newClient.LogMessageEvent += SendNewLogMessage;
            newClient.NameChangeEvent += NewClient_NameChangeEvent;
            newClient.Number = newClientNumber;
            Clients.Add(newClient);

            newClient.Save();

            if (NewClientEvent != null)
            {
                NewClientEvent(newClient);
            }

            return newClient;
        }

        public void RemoveClientAtNumber(int number)
        {
            TradeClient clientToRemove = null;

            for (int i = 0; i < Clients.Count; i++)
            {
                if (Clients[i].Number == number)
                {
                    clientToRemove = Clients[i];
                    Clients.RemoveAt(i);
                    break;
                }
            }

            if(clientToRemove != null)
            {
                clientToRemove.LogMessageEvent -= SendNewLogMessage;
                clientToRemove.NameChangeEvent -= NewClient_NameChangeEvent;
                clientToRemove.Delete();

                if(DeleteClientEvent != null)
                {
                    DeleteClientEvent(clientToRemove); 
                }
            }
        }

        public event Action<TradeClient> NewClientEvent;

        public event Action<TradeClient> DeleteClientEvent;

        public event Action ClientChangeNameEvent;

        #endregion

        #region Synchronize

        public void Synchronize()
        {
            try
            {
                // 1 сворачиваем не работающие сервера
                CollapseDontUseServers();

                // 2 сворачиваем не принадлежащие клиентам роботов
                CollapseDontUseRobots();

                // 3 развернуть все необходимые коннекторы
                DeployAllConnectors();

                // 4 развернуть все необходимые роботы
                DeployAllRobots();

            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        public void CollapseDontUseServers()
        {
            List<AServer> servers = ServerMaster.GetAServers();

            if(servers.Count == 0)
            {
                return;
            }

            AServer[] serversArray = servers.ToArray();

            for(int i = 0;i < serversArray.Length;i++)
            {
                if (ServerIsUsedInClients(serversArray[i]) == false)
                {
                    ServerMaster.DeleteServer(serversArray[i].ServerType, serversArray[i].ServerNum);
                }
            }
        }

        private bool ServerIsUsedInClients(AServer server)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                TradeClient client = Clients[i];

                if (client.IsMyServer(server) == true)
                {
                    return true;
                }
            }
            return false;
        }

        public void CollapseDontUseRobots()
        {
            List<BotPanel> robots = OsTraderMaster.Master.PanelsArray;

            if(robots.Count == 0)
            {
                return;
            }

            BotPanel[] robotsArray = robots.ToArray();

            for(int i = 0;i < robotsArray.Length;i++)
            {
                BotPanel bot = robotsArray[i];

                if(BotIsUsedClients(bot) == false)
                {
                    bot.OnOffEventsInTabs = false;
                    OsTraderMaster.Master.DeleteRobotByInstance(bot);
                }
            }
        }

        private bool BotIsUsedClients(BotPanel bot)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                TradeClient client = Clients[i];

                if (client.IsMyRobot(bot)== true)
                {
                    return true;
                }
            }
            return false;
        }

        public void DeployAllConnectors()
        {
            for(int i = 0;i < Clients.Count;i++)
            {
                TradeClient client = Clients[i];

                client.DeployAndConnectAllServers();
            }
        }

        public void DeployAllRobots()
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                TradeClient client = Clients[i];

                client.DeployAllRobots();
            }
        }

        #endregion

        #region Dialogs UI

        private ClientsMasterUi _uiClients;

        public void ShowDialogClientsMaster()
        {
            if(_uiClients == null)
            {
                _uiClients = new ClientsMasterUi();
                _uiClients.Closed += _uiClients_Closed;
                _uiClients.Show();
            }
            else
            {
                if(_uiClients.WindowState == System.Windows.WindowState.Minimized)
                {
                    _uiClients.WindowState = System.Windows.WindowState.Normal;
                }

                _uiClients.Activate();
            }
        }

        private void _uiClients_Closed(object sender, System.EventArgs e)
        {
            _uiClients = null;
        }

        private List<ClientUi> _uiClient = new List<ClientUi>();

        public void ShowDialogClient(int clientNumber)
        {
            TradeClient client = null;

            for(int i = 0;i < Clients.Count;i++)
            {
                if (Clients[i].Number == clientNumber)
                {
                    client = Clients[i]; 
                    break;
                }
            }

            if(client == null)
            {
                return;
            }

            ClientUi clientUi = null;

            for(int i = 0;i < _uiClient.Count;i++)
            {
                if (_uiClient[i].ClientNumber == clientNumber)
                {
                    clientUi = _uiClient[i];
                    break;
                }
            }
            if(clientUi == null)
            {
                clientUi = new ClientUi(client);
                clientUi.Closed += _uiClientConnectors_Closed;
                clientUi.Show();
                _uiClient.Add(clientUi);
            }
            else
            {
                if (clientUi.WindowState == System.Windows.WindowState.Minimized)
                {
                    clientUi.WindowState = System.Windows.WindowState.Normal;
                }

                clientUi.Activate();
            }
        }

        private void _uiClientConnectors_Closed(object sender, System.EventArgs e)
        {
            ClientUi clientUi = (ClientUi)sender;

            for (int i = 0; i < _uiClient.Count; i++)
            {
                if (_uiClient[i].ClientNumber == clientUi.ClientNumber)
                {
                    _uiClient[i].Closed -= _uiClientConnectors_Closed;
                    _uiClient.RemoveAt(i);
                    break;
                }
            }
        }

        #endregion
    }
}
