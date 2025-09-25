/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Layout;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    /// <summary>
    /// Interaction logic for ClientRobotParametersUi.xaml
    /// </summary>
    public partial class ClientRobotParametersUi : Window
    {
        private TradeClientRobot _robot;

        private TradeClient _client;

        public ClientRobotParametersUi(TradeClientRobot robot, TradeClient client)
        {
            InitializeComponent();
            _robot = robot;
            _client = client;

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClientRobot" + robot.Number);

            ComboBoxRobotType.Items.Add("None");
            List<string> scriptsNames = BotFactory.GetScriptsNamesStrategy();

            for(int i = 0; i < scriptsNames.Count; i++)
            {
                ComboBoxRobotType.Items.Add(scriptsNames[i]);
            }

            ComboBoxRobotType.SelectedItem = _robot.BotClassName;
            ComboBoxRobotType.SelectionChanged += ComboBoxRobotType_SelectionChanged;

            this.Closed += ClientRobotParametersUi_Closed;
        }

        private void ClientRobotParametersUi_Closed(object sender, EventArgs e)
        {


            _client = null;
            _robot = null;


        }

        private void ComboBoxRobotType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _robot.BotClassName = ComboBoxRobotType.SelectedItem.ToString();
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        #region Parameters grid







        #endregion
    }
}
