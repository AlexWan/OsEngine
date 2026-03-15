/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Interaction logic for PriceAlertCreateUi.xaml
    /// Логика взаимодействия для PriceAlertCreateUi.xaml
    /// </summary>
    public partial class AlertToPriceCreateUi
    {
        public AlertToPriceCreateUi(AlertToPrice alert)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            MyAlert = alert;

            CheckBoxOnOff.IsChecked = MyAlert.IsOn;

            ComboBoxActivationType.Items.Add(PriceAlertTypeActivation.PriceLowerOrEqual);
            ComboBoxActivationType.Items.Add(PriceAlertTypeActivation.PriceHigherOrEqual);
            ComboBoxActivationType.SelectedItem = MyAlert.TypeActivation;

            TextBoxPriceActivation.Text = MyAlert.PriceActivation.ToString(new CultureInfo("RU-ru"));

            ComboBoxSignalType.Items.Add(SignalType.None);
            ComboBoxSignalType.Items.Add(SignalType.Buy);
            ComboBoxSignalType.Items.Add(SignalType.Sell);
            ComboBoxSignalType.Items.Add(SignalType.CloseAll);
            ComboBoxSignalType.Items.Add(SignalType.CloseOne);
            ComboBoxSignalType.Items.Add(SignalType.OpenNew);
            ComboBoxSignalType.Items.Add(SignalType.ReloadProfit);
            ComboBoxSignalType.Items.Add(SignalType.ReloadStop);
            ComboBoxSignalType.SelectedItem = MyAlert.SignalType;

            ComboBoxOrderType.Items.Add(OrderPriceType.Limit);
            ComboBoxOrderType.Items.Add(OrderPriceType.Market);
            ComboBoxOrderType.SelectedItem = MyAlert.OrderPriceType;

            ComboBoxSlippageType.Items.Add(AlertSlippageType.Persent);
            ComboBoxSlippageType.Items.Add(AlertSlippageType.PriceStep);
            ComboBoxSlippageType.Items.Add(AlertSlippageType.Absolute);
            ComboBoxSlippageType.SelectedItem = MyAlert.SlippageType;

            TextBoxVolumeReaction.Text = MyAlert.VolumeReaction.ToString();
            TextBoxSlippage.Text = MyAlert.Slippage.ToString(new CultureInfo("RU-ru"));
            TextBoxClosePosition.Text = MyAlert.NumberClosePosition.ToString();

            CheckBoxWindow.IsChecked = MyAlert.MessageIsOn;
            TextBoxAlertMessage.Text = MyAlert.Message;

            ComboBoxMusic.Items.Add(AlertMusic.Bird);
            ComboBoxMusic.Items.Add(AlertMusic.Duck);
            ComboBoxMusic.Items.Add(AlertMusic.Wolf);
            ComboBoxMusic.SelectedItem = MyAlert.MusicType;

            LabelOsa.MouseDown += LabelOsa_MouseDown;
            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            this.Activate();
            this.Focus();

            if (InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass == null
                || InteractiveInstructions.BotStationLightPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonPostAlertToPriceCreate.Visibility = Visibility.Visible;
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
                            PostGreenAlertToPriceCreate.Opacity = 1;
                            PostWhiteAlertToPriceCreate.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            PostGreenAlertToPriceCreate.Opacity = 0;
                            PostWhiteAlertToPriceCreate.Opacity = 1;
                        }
                        else
                        {
                            PostGreenAlertToPriceCreate.Opacity = 1;
                            PostWhiteAlertToPriceCreate.Opacity = 0;
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

        private void ChangeText()
        {
            Title = OsLocalization.Alerts.TitleAlertToChartCreateUi;
            CheckBoxOnOff.Content = OsLocalization.Alerts.Label1;
            LabelActivation.Content = OsLocalization.Alerts.Label18;

            LabelTrade.Content = OsLocalization.Alerts.Label3;
            LabelReactionType.Content = OsLocalization.Alerts.Label4;
            LabelOrderType.Content = OsLocalization.Alerts.Label5;
            LabelVolume.Content = OsLocalization.Alerts.Label6;
            LabelSlippage.Content = OsLocalization.Alerts.Label7;
            LabelNumClosedPos.Content = OsLocalization.Alerts.Label8;
            LabelFireworks.Content = OsLocalization.Alerts.Label9;
            CheckBoxMusicAlert.Content = OsLocalization.Alerts.Label10;
            CheckBoxWindow.Content = OsLocalization.Alerts.Label16;
            ButtonSave.Content = OsLocalization.Alerts.Label17;
            LabelSlippageType.Content = OsLocalization.Alerts.Label19;
            LabelActivationPrice.Content = OsLocalization.Alerts.Label20;
        }

        void LabelOsa_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("http://o-s-a.net");
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public AlertToPrice MyAlert;

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    TextBoxPriceActivation.Text.ToDecimal();
                    TextBoxVolumeReaction.Text.ToDecimal();
                    TextBoxSlippage.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Alerts.Message3);
                    return;
                }

                if (CheckBoxOnOff.IsChecked.HasValue)
                {
                    MyAlert.IsOn = CheckBoxOnOff.IsChecked.Value;
                }

                Enum.TryParse(ComboBoxActivationType.SelectedItem.ToString(), out MyAlert.TypeActivation);

                MyAlert.PriceActivation = TextBoxPriceActivation.Text.ToDecimal();

                Enum.TryParse(ComboBoxSignalType.SelectedItem.ToString(), out MyAlert.SignalType);

                Enum.TryParse(ComboBoxOrderType.SelectedItem.ToString(), out MyAlert.OrderPriceType);

                MyAlert.VolumeReaction = TextBoxVolumeReaction.Text.ToDecimal();

                MyAlert.Slippage = TextBoxSlippage.Text.ToDecimal();

                int closePosition;

                if (int.TryParse(TextBoxClosePosition.Text, out closePosition))
                {
                    MyAlert.NumberClosePosition = closePosition;
                }

                Enum.TryParse(ComboBoxSlippageType.SelectedItem.ToString(), true, out MyAlert.SlippageType);

                if (CheckBoxWindow.IsChecked.HasValue)
                {
                    MyAlert.MessageIsOn = CheckBoxWindow.IsChecked.Value;
                }

                MyAlert.Message = TextBoxAlertMessage.Text;
                Enum.TryParse(ComboBoxMusic.SelectedItem.ToString(), out MyAlert.MusicType);

                Close();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #region Posts collection

        private void ButtonPostAlertToPriceCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.BotStationLightPosts.Link33.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
