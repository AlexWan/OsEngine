/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Market.Connectors
{
    /// <summary>
    /// Interaction logic for ConnectorNewsUi.xaml
    /// </summary>
    public partial class ConnectorNewsUi : Window
    {
        #region Constructor

        public ConnectorNewsUi(ConnectorNews connectorBot)
        {
            try
            {
                InitializeComponent();
                OsEngine.Layout.StickyBorders.Listen(this);
                OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);


                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null)
                {// if connection server to exchange hasn't been created yet / если сервер для подключения к бирже ещё не создан
                    Close();
                    return;
                }

                // save connectors
                // сохраняем коннекторы
                _connectorBot = connectorBot;

                TextBoxCountNewsToSave.Text = _connectorBot.CountNewsToSave.ToString();

                // upload settings to controls
                // загружаем настройки в контролы
                for (int i = 0; i < servers.Count; i++)
                {
                    ComboBoxTypeServer.Items.Add(servers[i].ServerNameAndPrefix);
                }

                if (connectorBot.ServerType != ServerType.None)
                {
                    ComboBoxTypeServer.SelectedItem = connectorBot.ServerFullName;
                    _selectedServerType = connectorBot.ServerType;
                    _selectedServerName = connectorBot.ServerFullName;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerNameAndPrefix;
                    _selectedServerType = servers[0].ServerType;
                    _selectedServerName = servers[0].ServerNameAndPrefix;
                }

                if (connectorBot.StartProgram == StartProgram.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester.ToString();
                    connectorBot.ServerType = ServerType.Tester;
                    _selectedServerType = ServerType.Tester;
                    ComboBoxTypeServer.IsEnabled = false;
                    _selectedServerName = ServerType.Tester.ToString();
                }

                ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

                Title = OsLocalization.Market.TitleConnectorCandle;
                Label1.Content = OsLocalization.Market.Label1;
                ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
                LabelCountNewsToSave.Content = OsLocalization.Market.Label161;

                ComboBoxTypeServer_SelectionChanged(null, null);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Closing += ConnectorCandlesUi_Closing;

            this.Activate();
            this.Focus();
        }

        private ConnectorNews _connectorBot;

        private void ConnectorCandlesUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            try
            {
                ComboBoxTypeServer.SelectionChanged -= ComboBoxTypeServer_SelectionChanged;

            }
            catch
            {
                // ignore
            }

            _connectorBot = null;

        }

        #endregion

        #region Other income events

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(ComboBoxTypeServer.Text == null)
                {
                    return;
                }
                int countNewsToSave = 0;
                try
                {
                    countNewsToSave = Convert.ToInt32(TextBoxCountNewsToSave.Text);
                }
                catch
                {
                    return;
                }


                _connectorBot.ServerType = _selectedServerType;
                _connectorBot.ServerFullName = _selectedServerName;
                _connectorBot.CountNewsToSave = countNewsToSave;
              
                _connectorBot.Save();
                _connectorBot.ReconnectHard();

                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBoxTypeServer.SelectedItem == null)
                {
                    return;
                }

                _selectedServerName = ComboBoxTypeServer.SelectedItem.ToString();

                Enum.TryParse(_selectedServerName.Split('_')[0], 
                    true, out _selectedServerType);

               
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private ServerType _selectedServerType;

        private string _selectedServerName;

        #endregion

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}