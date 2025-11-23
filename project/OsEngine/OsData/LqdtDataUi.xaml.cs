/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Windows;

namespace OsEngine.OsData
{
    public partial class LqdtDataUi : Window
    {
        private OsDataSet _set;

        private OsDataSetPainter _setPainter;

        public LqdtDataUi(OsDataSet set, OsDataSetPainter setPainter)
        {
            InitializeComponent();

            _set = set;
            _setPainter = setPainter;

            Title = OsLocalization.Data.TitleAddLqdt;
            ExchangeLabel.Content = OsLocalization.Data.Label60;
            CreateButton.Content = OsLocalization.Data.ButtonCreate;

            Activate();
            Focus();

            Closed += LqdtDataUi_Closed;
        }

        private void LqdtDataUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _set = null;
                _setPainter = null;
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ComboBoxExchange.Text == "MOEX")
                {
                    _set.AddLqdtMoex();
                }
                else // NYSE
                {
                    _set.AddLqdtNyse();
                }

                _setPainter.RePaintInterface();

                Close();
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

        }
    }
}

