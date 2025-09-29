/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.ClientManagement.Gui;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsTrader.ClientManagement
{
    public class ClientManagementMaster: ILogItem
    {
        #region Static part

        public static ClientManagementMaster Instance { get; set; }

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
