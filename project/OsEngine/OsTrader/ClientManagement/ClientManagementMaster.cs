/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using System;
using System.Collections.Generic;

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
                Log = new Log("ServerMaster", StartProgram.IsOsTrader);
                Log.Listen(this);
            }
        }

        public Log Log;

        #endregion

        #region Clients management

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

            TradeClient newClient = new TradeClient(newClientNumber);
            Clients.Add(newClient);

            if (NewClientEvent != null)
            {
                NewClientEvent(newClient);
            }

            return newClient;
        }

        public void RemoveClientAtNumber(int number)
        {


        }

        public event Action<TradeClient> NewClientEvent;

        public event Action<TradeClient> DeleteClientEvent;


        #endregion

        #region Dialogs UI

        private ClientsUi _uiClients;

        public void ShowDialogClients()
        {
            if(_uiClients == null)
            {
                _uiClients = new ClientsUi();
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

        private ClientsConnectorsUi _uiClientConnectors;

        public void ShowDialogClientConnectors(int clientNumber)
        {
            if (_uiClientConnectors == null)
            {
                _uiClientConnectors = new ClientsConnectorsUi();
                _uiClientConnectors.Closed += _uiClientConnectors_Closed;
                _uiClientConnectors.Show();
            }
            else
            {
                if (_uiClientConnectors.WindowState == System.Windows.WindowState.Minimized)
                {
                    _uiClientConnectors.WindowState = System.Windows.WindowState.Normal;
                }

                _uiClientConnectors.Activate();
            }
        }

        private void _uiClientConnectors_Closed(object sender, System.EventArgs e)
        {
            _uiClientConnectors = null;
        }

        private ClientsPortfoliosUi _uiClientsPortfolios;

        public void ShowDialogClientPortfolios(int clientNumber)
        {
            if (_uiClientsPortfolios == null)
            {
                _uiClientsPortfolios = new ClientsPortfoliosUi();
                _uiClientsPortfolios.Closed += _uiClientsPortfolios_Closed;
                _uiClientsPortfolios.Show();
            }
            else
            {
                if (_uiClientsPortfolios.WindowState == System.Windows.WindowState.Minimized)
                {
                    _uiClientsPortfolios.WindowState = System.Windows.WindowState.Normal;
                }

                _uiClientsPortfolios.Activate();
            }
        }

        private void _uiClientsPortfolios_Closed(object sender, System.EventArgs e)
        {
            _uiClientsPortfolios = null;
        }

        private ClientsRobotsUi _uiClientsRobots;

        public void ShowDialogClientRobots(int clientNumber)
        {
            if (_uiClientsRobots == null)
            {
                _uiClientsRobots = new ClientsRobotsUi();
                _uiClientsRobots.Closed += _uiClientsRobots_Closed;
                _uiClientsRobots.Show();
            }
            else
            {
                if (_uiClientsRobots.WindowState == System.Windows.WindowState.Minimized)
                {
                    _uiClientsRobots.WindowState = System.Windows.WindowState.Normal;
                }

                _uiClientsRobots.Activate();
            }
        }

        private void _uiClientsRobots_Closed(object sender, System.EventArgs e)
        {
            _uiClientsRobots = null;
        }

        #endregion
    }
}
