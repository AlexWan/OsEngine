/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Windows;
using System.Windows.Threading;

namespace OsEngine.OsData
{
    public partial class LqdtDataUi : Window
    {
        private OsDataSet _set;

        private OsDataSetPainter _setPainter;

        public LqdtDataUi(OsDataSet set, OsDataSetPainter setPainter)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _set = set;
            _setPainter = setPainter;

            Title = OsLocalization.Data.TitleAddLqdt;
            ExchangeLabel.Content = OsLocalization.Data.Label60;
            CreateButton.Content = OsLocalization.Data.ButtonCreate;

            Activate();
            Focus();

            Closed += LqdtDataUi_Closed;

            if (InteractiveInstructions.Data.AllInstructionsInClass == null
             || InteractiveInstructions.Data.AllInstructionsInClass.Count == 0)
            {
                ButtonDataLqdt.Visibility = Visibility.Visible;
            }

            StartButtonBlinkAnimation();
        }

        private void LqdtDataUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }

                CreateButton.Click -= CreateButton_Click;
                ButtonDataLqdt.Click -= ButtonDataLqdt_Click;

                _set = null;
                _setPainter = null;

                Closed -= LqdtDataUi_Closed;
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DispatcherTimer _blinkTimer;

        private int _blinkCount;

        private bool _isGreenVisible = true;

        private void StartButtonBlinkAnimation()
        {
            try
            {
                _blinkTimer = new DispatcherTimer();
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
                _blinkTimer.Tick += _blinkTimer_Tick;
                _blinkTimer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _blinkTimer_Tick(object sender, EventArgs e)
        {
            if (_blinkTimer == null)
            {
                return;
            }

            try
            {
                if (_blinkCount >= 20)
                {
                    _blinkTimer.Stop();
                    PostGreenDataLqdt.Opacity = 1;
                    PostWhiteDataLqdt.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    PostGreenDataLqdt.Opacity = 0;
                    PostWhiteDataLqdt.Opacity = 1;
                }
                else
                {
                    PostGreenDataLqdt.Opacity = 1;
                    PostWhiteDataLqdt.Opacity = 0;
                }

                _isGreenVisible = !_isGreenVisible;
                _blinkCount++;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                }
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

        #region Posts collection

        private void ButtonDataLqdt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.Data.Link7.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}

