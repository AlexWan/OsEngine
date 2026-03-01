/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.OsTrader.RiskManager
{
    /// <summary>
    /// Risk Manager window
    /// Окно Риск Менеджера
    /// </summary>
    public partial class RiskManagerUi
    {
        /// <summary>
        /// risk manager
        /// риск менеджер
        /// </summary>
        private RiskManager _riskManager;
        public RiskManagerUi(RiskManager riskManager)
        {
            try
            {
                _riskManager = riskManager;
                InitializeComponent();
                OsEngine.Layout.StickyBorders.Listen(this);
                OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
                LoadDateOnForm();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Title = OsLocalization.Trader.Label12;
            LabelMaxRisk.Content = OsLocalization.Trader.Label14;
            LabelMaxLossReactioin.Content = OsLocalization.Trader.Label15;
            CheckBoxIsOn.Content = OsLocalization.Trader.Label16;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            this.Activate();
            this.Focus();

            if (InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass == null
            || InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonRiskManager.Visibility = Visibility.Visible;
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
                            PostGreenRiskManager.Opacity = 1;
                            PostWhiteRiskManager.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            PostGreenRiskManager.Opacity = 0;
                            PostWhiteRiskManager.Opacity = 1;
                        }
                        else
                        {
                            PostGreenRiskManager.Opacity = 1;
                            PostWhiteRiskManager.Opacity = 0;
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

        /// <summary>
        /// upload data to the form
        /// загрузить данные на форму
        /// </summary>
        private void LoadDateOnForm()
        {
            CheckBoxIsOn.IsChecked = _riskManager.IsActive;
            TextBoxOpenMaxDd.Text = _riskManager.MaxDrowDownToDayPersent.ToString(new CultureInfo("ru-RU"));

            ComboBoxReaction.Items.Add(RiskManagerReactionType.CloseAndOff);
            ComboBoxReaction.Items.Add(RiskManagerReactionType.ShowDialog);
            ComboBoxReaction.Items.Add(RiskManagerReactionType.None);

            ComboBoxReaction.Text = _riskManager.ReactionType.ToString();
            
        }

        /// <summary>
        /// clicked accept
        /// нажали кнопку принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Convert.ToDecimal(TextBoxOpenMaxDd.Text);
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }


           _riskManager.IsActive =  CheckBoxIsOn.IsChecked.Value;
           _riskManager.MaxDrowDownToDayPersent = Convert.ToDecimal(TextBoxOpenMaxDd.Text);

           Enum.TryParse(ComboBoxReaction.Text,false,out _riskManager.ReactionType);
           _riskManager.Save();
            Close();
        }

        #region Posts collection

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.BotStationLightPosts.Link2.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion


    }
}
