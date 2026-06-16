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

        private DispatcherTimer _blinkTimer;
        private int _blinkCount;
        private bool _isGreenVisible = true;

        public RiskManagerUi(RiskManager riskManager)
        {
            try
            {
                _riskManager = riskManager;
                InitializeComponent();
                Closed += RiskManagerUi_Closed;
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
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                    PostGreenRiskManager.Opacity = 1;
                    PostWhiteRiskManager.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    PostGreenRiskManager.Opacity = 0;
                    PostWhiteRiskManager.Opacity = 1;
                }
                else
                {
                    PostGreenRiskManager.Opacity = 1;
                    PostWhiteRiskManager.Opacity = 0;
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
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }
            }
        }

        private void RiskManagerUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= RiskManagerUi_Closed;

                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }

                _riskManager = null;
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
