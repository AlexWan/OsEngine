/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    /// <summary>
    /// Interaction logic for ClientRobotSourcesUi.xaml
    /// </summary>
    public partial class ClientRobotSourcesUi : Window
    {
        private TradeClient _client;
        private TradeClientRobot _robot;
        private TradeClientSourceSettings _source;

        public ClientRobotSourcesUi(TradeClientRobot robot, 
            TradeClient client)
        {
            InitializeComponent();

            _robot = robot;
            _client = client;

            this.Closed += ClientRobotSourcesUi_Closed;

            if(_robot.SourceSettings == null
                || _robot.SourceSettings.Count == 0)
            {
                return;
            }

            TextBoxSelectedRobot.Text = robot.BotClassName;

            for(int i = 0;i < _robot.SourceSettings.Count;i++)
            {
                BotTabType botTab = _robot.SourceSettings[i].BotTabType;

                if(botTab == BotTabType.Simple)
                {
                    ComboBoxSources.Items.Add(i + "#Simple");
                }
                else if (botTab == BotTabType.Screener)
                {
                    ComboBoxSources.Items.Add(i + "#Screener");
                }
                else if (botTab == BotTabType.Index)
                {
                    ComboBoxSources.Items.Add(i + "#Index");
                }
                else if (botTab == BotTabType.Pair)
                {
                    ComboBoxSources.Items.Add(i + "#Pair");
                }
            }

            ComboBoxSources.SelectedIndex = 0;
            ComboBoxSources.SelectionChanged += ComboBoxSources_SelectionChanged;

            _source = _robot.SourceSettings[0];
            SetSourceOnForm(_source);
        }

        private void ClientRobotSourcesUi_Closed(object sender, EventArgs e)
        {
            _robot = null;
            _client = null;

        }

        private void ComboBoxSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int number = ComboBoxSources.SelectedIndex;

            _source = _robot.SourceSettings[number];
            SetSourceOnForm(_source);
        }

        private void SetSourceOnForm(TradeClientSourceSettings source)
        {
            TextBoxServerNum.Text = source.ClientServerNum.ToString();

            ComboBoxCommissionType.Items.Clear();
            ComboBoxCommissionType.Items.Add(CommissionType.None.ToString());
            ComboBoxCommissionType.Items.Add(CommissionType.OneLotFix.ToString());
            ComboBoxCommissionType.Items.Add(CommissionType.Percent.ToString());
            ComboBoxCommissionType.SelectedItem = source.CommissionType.ToString();

            TextBoxCommissionValue.Text = source.CommissionValue.ToString();

            ComboBoxCandleMarketDataType.Items.Clear();
            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.Tick.ToString());
            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.MarketDepth.ToString());
            ComboBoxCandleMarketDataType.SelectedItem = source.CandleMarketDataType.ToString();

            CheckBoxSaveTradesInCandle.IsChecked = source.SaveTradesInCandle;

            ComboBoxTimeFrame.Items.Clear();
            ComboBoxTimeFrame.Items.Add(TimeFrame.Hour2.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Hour1.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min30.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min15.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min10.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min5.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min1.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec30.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec15.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec10.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec5.ToString());




        }
    }
}
