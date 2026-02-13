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

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            PostGreenDataLqdt.Opacity = 1;
                            PostWhiteDataLqdt.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            PostGreenDataLqdt.Opacity = 0;
                            PostWhiteDataLqdt.Opacity = 1;
                        }
                        else
                        {
                            PostGreenDataLqdt.Opacity = 1;
                            PostWhiteDataLqdt.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
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

