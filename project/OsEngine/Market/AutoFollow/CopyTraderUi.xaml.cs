/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.Market.AutoFollow
{
    /// <summary>
    /// Interaction logic for CopyTraderUi.xaml
    /// </summary>
    public partial class CopyTraderUi : Window
    {
        public CopyTrader CopyTraderClass;

        public int TraderNumber;

        public CopyTraderUi(CopyTrader copyTrader)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "copyTraderUi " + copyTrader.Number);

            CopyTraderClass = copyTrader;
            TraderNumber = copyTrader.Number;
            Title = OsLocalization.Market.Label201 + " # " + CopyTraderClass.Number + " " + CopyTraderClass.Name;

            CopyTraderClass.DeleteEvent += CopyTraderClass_DeleteEvent;

            this.Closed += CopyTraderUi_Closed;

            // 1 Base settings

            ComboBoxIsOn.Items.Add(true.ToString());
            ComboBoxIsOn.Items.Add(false.ToString());
            ComboBoxIsOn.SelectedItem = CopyTraderClass.IsOn.ToString();
            ComboBoxIsOn.SelectionChanged += ComboBoxIsOn_SelectionChanged;

            TextBoxName.Text = copyTrader.Name;
            TextBoxName.TextChanged += TextBoxName_TextChanged;

            ComboBoxWorkType.Items.Add(CopyTraderType.None.ToString());
            ComboBoxWorkType.Items.Add(CopyTraderType.Portfolio.ToString());
            ComboBoxWorkType.Items.Add(CopyTraderType.Robot.ToString());
            ComboBoxWorkType.SelectedItem = copyTrader.WorkType.ToString();
            ComboBoxWorkType.SelectionChanged += ComboBoxWorkType_SelectionChanged;

            CheckEnabledTabs();

            // localization

            LabelIsOn.Content = OsLocalization.Market.Label182; 
            LabelName.Content = OsLocalization.Market.Label70;
            LabelWorkType.Content = OsLocalization.Market.Label200;

            TabItem itemBase = (TabItem)TabControlPrime.Items[0];
            itemBase.Header = OsLocalization.Market.Label202;

            TabItem itemPortfolio = (TabItem)TabControlPrime.Items[1];
            itemPortfolio.Header = OsLocalization.Market.Label203;

            TabItem itemRobots = (TabItem)TabControlPrime.Items[2];
            itemRobots.Header = OsLocalization.Market.Label204;
        }

        private void CopyTraderUi_Closed(object sender, EventArgs e)
        {
            CopyTraderClass.DeleteEvent -= CopyTraderClass_DeleteEvent;
            CopyTraderClass = null;
        }

        private void CopyTraderClass_DeleteEvent()
        {
            Close();
        }

        public event Action NeedToUpdateCopyTradersGridEvent;

        #region Base settings

        private void ComboBoxIsOn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderType type;

                bool isOn = Convert.ToBoolean(ComboBoxIsOn.SelectedItem.ToString());
                CopyTraderClass.IsOn = isOn;
                ServerMaster.SaveCopyMaster();

                if(NeedToUpdateCopyTradersGridEvent != null)
                {
                    NeedToUpdateCopyTradersGridEvent();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxWorkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderType type;

                if(Enum.TryParse(ComboBoxWorkType.SelectedItem.ToString(), out type))
                {
                    CopyTraderClass.WorkType = type;
                    ServerMaster.SaveCopyMaster();
                    CheckEnabledTabs();

                    if (NeedToUpdateCopyTradersGridEvent != null)
                    {
                        NeedToUpdateCopyTradersGridEvent();
                    }
                }              
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxName_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                CopyTraderClass.Name = TextBoxName.Text;
                ServerMaster.SaveCopyMaster();

                Title = OsLocalization.Market.Label201 + " # " + CopyTraderClass.Number + " " + CopyTraderClass.Name;

                if (NeedToUpdateCopyTradersGridEvent != null)
                {
                    NeedToUpdateCopyTradersGridEvent();
                }
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void CheckEnabledTabs()
        {
            TabItem itemPortfolio = (TabItem)TabControlPrime.Items[1];
            TabItem itemRobots = (TabItem)TabControlPrime.Items[2];

            if (CopyTraderClass.WorkType == CopyTraderType.None)
            {
                itemPortfolio.IsEnabled = false;
                itemRobots.IsEnabled = false;
            }
            else if (CopyTraderClass.WorkType == CopyTraderType.Portfolio)
            {
                itemPortfolio.IsEnabled = true;
                itemRobots.IsEnabled = false;
            }
            else if (CopyTraderClass.WorkType == CopyTraderType.Robot)
            {
                itemPortfolio.IsEnabled = false;
                itemRobots.IsEnabled = true;
            }
        }

        #endregion

        #region Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendNewLogMessage(string message, LogMessageType messageType)
        {
            if(LogMessageEvent != null)
            {
                LogMessageEvent.Invoke(message, messageType);
            }
            else
            {
                ServerMaster.SendNewLogMessage(message, messageType);
            }
        }

        #endregion
    }
}
